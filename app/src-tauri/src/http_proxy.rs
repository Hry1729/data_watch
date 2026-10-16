use std::collections::HashMap;
use std::future::{poll_fn, Future};
use std::pin::pin;
use std::sync::{Arc, Mutex, OnceLock};
use std::task::{Poll, Waker};
use std::time::Duration;

use reqwest::{header::CONTENT_DISPOSITION, redirect::Policy, Client, Url};
use tauri::State;

pub const MAX_BACKEND_RESPONSE_BYTES: usize = 50 * 1024 * 1024;
const MAX_BACKEND_REQUEST_BYTES: usize = 4 * 1024 * 1024;
const MAX_AGENT_DRIVER_REQUEST_BYTES: usize = 4 * ((50 * 1024 * 1024 + 2) / 3) + 4096;

#[derive(Default)]
pub struct BackendOriginState(pub Mutex<Option<String>>);

#[derive(Default)]
pub struct HttpRequestState(Mutex<HashMap<String, Arc<RequestCancellation>>>);

#[derive(Default)]
struct RequestCancellation(Mutex<(bool, Option<Waker>)>);

#[tauri::command]
pub fn register_http_request(request_id: String, state: State<'_, HttpRequestState>) -> Result<(), String> {
    if request_id.is_empty() || request_id.len() > 128 {
        return Err("invalid request id".to_string());
    }
    let mut requests = state.0.lock().map_err(|_| "request state is unavailable".to_string())?;
    if requests.contains_key(&request_id) {
        return Err("request id is already registered".to_string());
    }
    requests.insert(request_id, Arc::new(RequestCancellation::default()));
    Ok(())
}

fn cancel_request(request_id: &str, state: &HttpRequestState) -> Result<(), String> {
    let request = state.0.lock().map_err(|_| "request state is unavailable".to_string())?.remove(request_id);
    if let Some(request) = request {
        let mut cancellation = request.0.lock().map_err(|_| "request state is unavailable".to_string())?;
        cancellation.0 = true;
        if let Some(waker) = cancellation.1.take() { waker.wake(); }
    }
    Ok(())
}

#[tauri::command]
pub fn cancel_http_request(request_id: String, state: State<'_, HttpRequestState>) -> Result<(), String> {
    cancel_request(&request_id, &state)
}

struct RequestRegistration<'a> {
    id: String,
    cancellation: Arc<RequestCancellation>,
    state: &'a HttpRequestState,
}

impl Drop for RequestRegistration<'_> {
    fn drop(&mut self) {
        if let Ok(mut requests) = self.state.0.lock() {
            if requests.get(&self.id).is_some_and(|value| Arc::ptr_eq(value, &self.cancellation)) {
                requests.remove(&self.id);
            }
        }
    }
}

async fn run_cancellable<T>(
    request_id: Option<String>,
    state: &HttpRequestState,
    request: impl Future<Output = Result<T, String>>,
) -> Result<T, String> {
    let Some(id) = request_id else { return request.await; };
    let cancellation = state.0.lock().map_err(|_| "request state is unavailable".to_string())?
        .get(&id).cloned().ok_or_else(|| "request_cancelled".to_string())?;
    let registration = RequestRegistration { id, cancellation, state };
    let mut request = pin!(request);
    poll_fn(|cx| {
        {
            let mut cancellation = registration.cancellation.0.lock().unwrap();
            if cancellation.0 { return Poll::Ready(Err("request_cancelled".to_string())); }
            cancellation.1 = Some(cx.waker().clone());
        }
        // Dropping this future on cancellation aborts both sending and streaming the body.
        request.as_mut().poll(cx)
    }).await
}

static HTTP_CLIENT: OnceLock<Result<Client, String>> = OnceLock::new();

fn shared_client() -> Result<&'static Client, String> {
    HTTP_CLIENT.get_or_init(|| Client::builder()
        .connect_timeout(Duration::from_secs(3))
        .redirect(Policy::none())
        .build()
        .map_err(|e| format!("http client initialization failed: {}", e)))
        .as_ref().map_err(Clone::clone)
}

fn normalize_backend_origin(value: &str) -> Result<String, String> {
    let parsed = Url::parse(value.trim()).map_err(|_| "backend_base_url is invalid".to_string())?;
    if !matches!(parsed.scheme(), "http" | "https") {
        return Err("backend_base_url must use http or https".to_string());
    }
    if parsed.host_str().is_none() || !parsed.username().is_empty() || parsed.password().is_some() {
        return Err("backend_base_url must contain only an origin".to_string());
    }
    if parsed.path() != "" && parsed.path() != "/" || parsed.query().is_some() || parsed.fragment().is_some() {
        return Err("backend_base_url must not contain a path, query, or fragment".to_string());
    }

    let host = parsed.host_str().unwrap_or_default();
    let host = if host.contains(':') {
        format!("[{}]", host)
    } else {
        host.to_string()
    };
    let port = parsed.port().map(|value| format!(":{}", value)).unwrap_or_default();
    Ok(format!("{}://{}{}", parsed.scheme(), host, port))
}

fn validate_backend_path(path: &str) -> Result<(), String> {
    if path.len() > 4096 || !path.starts_with("/api/") || path.starts_with("//") {
        return Err("backend path is not allowed".to_string());
    }
    if path.contains('\\') || path.contains('\0') {
        return Err("backend path is not allowed".to_string());
    }
    let path_only = path.split_once('?').map_or(path, |(value, _)| value);
    if path_only.split('/').any(|segment| segment == "..") {
        return Err("backend path is not allowed".to_string());
    }
    Ok(())
}

fn validate_backend_method(method: &str) -> Result<(), String> {
    match method {
        "GET" | "POST" | "PUT" | "PATCH" | "DELETE" => Ok(()),
        _ => Err(format!("unsupported method: {}", method)),
    }
}

/// 校验 Agent 代理路径，并把请求目标锚定在 127.0.0.1:<port>。
///
/// 仅做字符串前缀拼接是不够的：`path = "@evil.com/x"` 会让
/// `format!("http://127.0.0.1:{}{}", port, path)` 解析为
/// `http://127.0.0.1:7070@evil.com/x` —— 前者成为 URL 的 **userinfo**，真实 host 变成
/// `evil.com`，随后注入的 Bearer 令牌就会被发往该主机。因此必须在拼好之后用 URL 解析
/// 结果反向核对 host/port，并显式拒绝 userinfo 与协议相对路径。
fn build_local_agent_url(port: u16, path: &str) -> Result<Url, String> {
    const MAX_PATH_LEN: usize = 4096;
    if path.is_empty() || path.len() > MAX_PATH_LEN || !path.starts_with('/') || path.starts_with("//") {
        return Err("agent path must be a single in-site absolute path".to_string());
    }
    if path.contains('\\') || path.contains('\0') {
        return Err("agent path is not allowed".to_string());
    }
    let path_only = path.split_once('?').map_or(path, |(value, _)| value);
    if path_only.split('/').any(|segment| segment == "..") {
        return Err("agent path is not allowed".to_string());
    }

    let candidate = Url::parse(&format!("http://127.0.0.1:{}{}", port, path))
        .map_err(|_| "agent path is not a valid URL path".to_string())?;
    if candidate.host_str() != Some("127.0.0.1") || candidate.port() != Some(port) {
        return Err("agent path must not change the request target".to_string());
    }
    if !candidate.username().is_empty() || candidate.password().is_some() {
        return Err("agent path must not contain userinfo".to_string());
    }
    Ok(candidate)
}

fn ensure_backend_origin(base_url: &str, state: &State<'_, BackendOriginState>) -> Result<String, String> {
    let origin = normalize_backend_origin(base_url)?;
    let configured = state.0.lock().map_err(|_| "backend origin state is unavailable".to_string())?;
    match configured.as_ref() {
        Some(expected) if expected == &origin => Ok(origin),
        Some(_) => Err("backend_base_url does not match the configured backend origin".to_string()),
        None => Err("backend origin is not configured".to_string()),
    }
}

fn validate_request_body(body: Option<&String>) -> Result<(), String> {
    if body.map_or(0, String::len) > MAX_BACKEND_REQUEST_BYTES {
        return Err("backend request body is too large".to_string());
    }
    Ok(())
}

fn validate_agent_body(method: &str, path: &str, body: Option<&String>) -> Result<(), String> {
    let path = path.split('?').next().unwrap_or(path);
    let limit = if method == "POST" && matches!(path, "/admin/drivers" | "/admin/drivers/preview") {
        MAX_AGENT_DRIVER_REQUEST_BYTES
    } else {
        MAX_BACKEND_REQUEST_BYTES
    };
    if body.map_or(0, String::len) > limit {
        return Err("agent_request_too_large".to_string());
    }
    Ok(())
}

async fn read_limited_bytes(mut response: reqwest::Response, limit: usize) -> Result<Vec<u8>, String> {
    if response
        .content_length()
        .is_some_and(|length| length > limit as u64)
    {
        return Err("response_body_too_large".to_string());
    }

    let mut bytes = Vec::new();
    while let Some(chunk) = response.chunk().await.map_err(|e| format!("read body failed: {}", e))? {
        if bytes.len().saturating_add(chunk.len()) > limit {
            return Err("response_body_too_large".to_string());
        }
        bytes.extend_from_slice(&chunk);
    }
    Ok(bytes)
}

async fn read_limited_text(response: reqwest::Response) -> Result<String, String> {
    String::from_utf8(read_limited_bytes(response, MAX_BACKEND_RESPONSE_BYTES).await?)
        .map_err(|_| "backend response is not valid UTF-8".to_string())
}

#[tauri::command]
pub fn set_backend_origin(
    base_url: String,
    state: State<'_, BackendOriginState>,
) -> Result<(), String> {
    let origin = normalize_backend_origin(&base_url)?;
    let mut configured = state.0.lock().map_err(|_| "backend origin state is unavailable".to_string())?;
    if let Some(expected) = configured.as_ref() {
        if expected != &origin {
            return Err("backend origin is already configured; restart the app to change it".to_string());
        }
    } else {
        *configured = Some(origin);
    }
    Ok(())
}

#[tauri::command]
pub async fn agent_request(
    method: String,
    path: String,
    port: u16,
    body: Option<String>,
    token: Option<String>,
    timeout_secs: Option<u64>,
    request_id: Option<String>,
    requests: State<'_, HttpRequestState>,
) -> Result<(u16, String), String> {
    run_cancellable(request_id, &requests, async {
    let url = build_local_agent_url(port, &path)?;
    validate_agent_body(&method, &path, body.as_ref())?;
    let timeout = Duration::from_secs(timeout_secs.unwrap_or(5).max(1));
    let client = shared_client()?;

    let mut req = match method.as_str() {
        "GET" => client.get(url),
        "POST" => client.post(url),
        "PUT" => client.put(url),
        "DELETE" => client.delete(url),
        m => return Err(format!("unsupported method: {}", m)),
    };

    req = req.timeout(timeout);
    if let Some(t) = token {
        // 不 log token 值
        req = req.header("Authorization", format!("Bearer {}", t));
    }
    if let Some(b) = body {
        // 不 log body 中的 password 字段
        req = req.header("Content-Type", "application/json").body(b);
    }

    let resp = req
        .send()
        .await
        .map_err(|e| format!("agent_unreachable: {}", e))?;
    let status = resp.status().as_u16();
    let text = String::from_utf8(read_limited_bytes(resp, MAX_BACKEND_RESPONSE_BYTES).await?)
        .map_err(|_| "agent_response_invalid_utf8".to_string())?;

    Ok((status, text))
    }).await
}

#[tauri::command]
pub async fn backend_download(
    base_url: String,
    path: String,
    authorization: Option<String>,
    state: State<'_, BackendOriginState>,
    request_id: Option<String>,
    requests: State<'_, HttpRequestState>,
) -> Result<(u16, Vec<u8>, Option<String>), String> {
    run_cancellable(request_id, &requests, async {
    let base = ensure_backend_origin(&base_url, &state)?;
    validate_backend_path(&path)?;

    let client = shared_client()?;
    let mut req = client.get(format!("{}{}", base, path)).timeout(Duration::from_secs(60));
    if let Some(a) = authorization {
        req = req.header("Authorization", a);
    }

    let resp = req
        .send()
        .await
        .map_err(|e| format!("backend_unreachable: {}", e))?;
    let status = resp.status().as_u16();
    let disposition = resp
        .headers()
        .get(CONTENT_DISPOSITION)
        .and_then(|value| value.to_str().ok())
        .map(str::to_owned);
    let bytes = read_limited_bytes(resp, MAX_BACKEND_RESPONSE_BYTES).await?;

    Ok((status, bytes, disposition))
    }).await
}

#[tauri::command]
pub async fn backend_request(
    base_url: String,
    method: String,
    path: String,
    body: Option<String>,
    authorization: Option<String>,
    state: State<'_, BackendOriginState>,
    request_id: Option<String>,
    requests: State<'_, HttpRequestState>,
) -> Result<(u16, String), String> {
    run_cancellable(request_id, &requests, async {
    let base = ensure_backend_origin(&base_url, &state)?;
    validate_backend_method(&method)?;
    validate_backend_path(&path)?;
    validate_request_body(body.as_ref())?;
    let url = format!("{}{}", base, path);
    let client = shared_client()?;

    let mut req = match method.as_str() {
        "GET" => client.get(&url),
        "POST" => client.post(&url),
        "PUT" => client.put(&url),
        "PATCH" => client.patch(&url),
        "DELETE" => client.delete(&url),
        _ => unreachable!("method validated above"),
    };

    req = req.timeout(Duration::from_secs(10));
    if let Some(a) = authorization {
        req = req.header("Authorization", a);
    }
    if let Some(b) = body {
        req = req.header("Content-Type", "application/json").body(b);
    }

    let resp = req
        .send()
        .await
        .map_err(|e| format!("backend_unreachable: {}", e))?;
    let status = resp.status().as_u16();
    let text = read_limited_text(resp).await?;

    Ok((status, text))
    }).await
}

#[cfg(test)]
mod tests {
    use super::{
        build_local_agent_url, normalize_backend_origin, validate_backend_method,
        validate_backend_path, validate_request_body, MAX_BACKEND_REQUEST_BYTES,
        MAX_BACKEND_RESPONSE_BYTES,
    };

    #[test]
    fn cancellation_before_dispatch_does_not_start_the_request_or_affect_another_request() {
        use std::sync::{atomic::{AtomicBool, Ordering}, Arc};
        let state = super::HttpRequestState::default();
        for id in ["cancelled", "normal"] {
            state.0.lock().unwrap().insert(id.to_string(), Arc::new(super::RequestCancellation::default()));
        }
        super::cancel_request("cancelled", &state).unwrap();
        let started = AtomicBool::new(false);
        tauri::async_runtime::block_on(async {
            let error = super::run_cancellable(Some("cancelled".into()), &state, async {
                started.store(true, Ordering::SeqCst);
                Ok(())
            }).await.unwrap_err();
            assert_eq!(error, "request_cancelled");
            assert!(!started.load(Ordering::SeqCst));
            assert_eq!(super::run_cancellable(Some("normal".into()), &state, async { Ok(42) }).await, Ok(42));
        });
        assert!(state.0.lock().unwrap().is_empty());
    }

    #[test]
    fn cancellation_closes_the_http_connection_while_waiting_for_headers_or_body() {
        use std::io::{ErrorKind, Read, Write};
        use std::sync::{mpsc, Arc};
        use std::time::Duration;
        for body_started in [false, true] {
            let listener = std::net::TcpListener::bind("127.0.0.1:0").unwrap();
            let address = listener.local_addr().unwrap();
            let (ready, waiting) = mpsc::channel();
            let server = std::thread::spawn(move || {
                let (mut socket, _) = listener.accept().unwrap();
                socket.set_read_timeout(Some(Duration::from_secs(3))).unwrap();
                let mut request = [0u8; 4096];
                socket.read(&mut request).unwrap();
                if body_started {
                    socket.write_all(b"HTTP/1.1 200 OK\r\nContent-Length: 1000\r\n\r\nx").unwrap();
                }
                ready.send(()).unwrap();
                match socket.read(&mut request) {
                    Ok(0) => (),
                    Err(error) if matches!(error.kind(), ErrorKind::ConnectionReset | ErrorKind::ConnectionAborted) => (),
                    result => panic!("cancelled HTTP connection remained open: {result:?}"),
                }
            });
            let state = Arc::new(super::HttpRequestState::default());
            state.0.lock().unwrap().insert("slow".into(), Arc::new(super::RequestCancellation::default()));
            let cancellation_state = state.clone();
            let cancel = std::thread::spawn(move || {
                waiting.recv_timeout(Duration::from_secs(3)).unwrap();
                super::cancel_request("slow", &cancellation_state).unwrap();
            });
            tauri::async_runtime::block_on(async {
                let result = super::run_cancellable(Some("slow".into()), &state, async {
                    let response = super::shared_client().unwrap().get(format!("http://{address}/"))
                        .timeout(Duration::from_secs(10)).send().await.map_err(|error| error.to_string())?;
                    super::read_limited_text(response).await
                }).await;
                assert_eq!(result, Err("request_cancelled".into()));
            });
            cancel.join().unwrap();
            server.join().unwrap();
            assert!(state.0.lock().unwrap().is_empty());
        }
    }

    #[test]
    fn shared_pool_keeps_timeouts_per_request() {
        use std::time::Duration;
        let client = super::shared_client().unwrap();
        assert!(std::ptr::eq(client, super::shared_client().unwrap()));
        let fast = client.get("http://127.0.0.1:7070/health").timeout(Duration::from_secs(5)).build().unwrap();
        let stop = client.get("http://127.0.0.1:7070/health").timeout(Duration::from_secs(60)).build().unwrap();
        assert_eq!(fast.timeout(), Some(&Duration::from_secs(5)));
        assert_eq!(stop.timeout(), Some(&Duration::from_secs(60)));
    }

    #[test]
    fn origin_policy_accepts_only_a_normalized_http_origin() {
        assert_eq!(normalize_backend_origin("https://backend.example:8443/").unwrap(), "https://backend.example:8443");
        assert!(normalize_backend_origin("file:///tmp/backend").is_err());
        assert!(normalize_backend_origin("https://backend.example/api").is_err());
        assert!(normalize_backend_origin("https://user:secret@backend.example").is_err());
    }

    #[test]
    fn backend_path_and_method_policy_rejects_non_api_requests() {
        assert!(validate_backend_path("/api/auth/login").is_ok());
        assert!(validate_backend_path("/admin/secrets").is_err());
        assert!(validate_backend_path("/api/../admin").is_err());
        assert!(validate_backend_path("//api/auth/login").is_err());
        assert!(validate_backend_method("GET").is_ok());
        assert!(validate_backend_method("OPTIONS").is_err());
    }

    #[test]
    fn request_and_response_limits_are_byte_limits() {
        assert!(validate_request_body(Some(&"x".repeat(MAX_BACKEND_REQUEST_BYTES))).is_ok());
        assert!(validate_request_body(Some(&"x".repeat(MAX_BACKEND_REQUEST_BYTES + 1))).is_err());
        assert_eq!(MAX_BACKEND_RESPONSE_BYTES, 50 * 1024 * 1024);
    }

    #[test]
    fn agent_limits_allow_supported_driver_archives_only_on_upload_routes() {
        let large = "x".repeat(super::MAX_AGENT_DRIVER_REQUEST_BYTES);
        assert!(super::validate_agent_body("POST", "/admin/drivers", Some(&large)).is_ok());
        assert!(super::validate_agent_body("POST", "/admin/drivers/preview?check=1", Some(&large)).is_ok());
        assert!(super::validate_agent_body("POST", "/admin/config", Some(&large)).is_err());
        assert!(super::validate_agent_body("PUT", "/admin/drivers", Some(&large)).is_err());
        assert!(super::validate_agent_body("POST", "/admin/drivers", Some(&(large + "x"))).is_err());
        let utf8 = "中".repeat(MAX_BACKEND_REQUEST_BYTES / 3 + 1);
        assert!(super::validate_agent_body("POST", "/admin/config", Some(&utf8)).is_err());
    }

    #[test]
    fn response_limits_reject_chunked_bodies_without_content_length() {
        use std::io::{Read, Write};
        use std::time::Duration;
        let listener = std::net::TcpListener::bind("127.0.0.1:0").unwrap();
        let address = listener.local_addr().unwrap();
        let server = std::thread::spawn(move || {
            let (mut socket, _) = listener.accept().unwrap();
            let mut request = [0u8; 4096];
            socket.read(&mut request).unwrap();
            socket.write_all(b"HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\nConnection: close\r\n\r\n81\r\n").unwrap();
            socket.write_all(&[b'x'; 129]).unwrap();
            socket.write_all(b"\r\n0\r\n\r\n").unwrap();
        });
        tauri::async_runtime::block_on(async {
            let response = super::shared_client().unwrap().get(format!("http://{address}/"))
                .timeout(Duration::from_secs(5)).send().await.unwrap();
            assert_eq!(response.content_length(), None);
            assert_eq!(super::read_limited_bytes(response, 128).await.unwrap_err(), "response_body_too_large");
        });
        server.join().unwrap();
    }

    // --- B14：Agent 代理目标必须被锚定在 127.0.0.1:<port> -----------------------

    #[test]
    fn agent_url_rejects_userinfo_host_hijack() {
        // 关键反例（原始问题）：字符串拼接会把它变成
        // http://127.0.0.1:7070@evil.com/v1/status，真实 host 落到 evil.com ——
        // Bearer 令牌会随之外泄。注意它**没有前导斜杠**。
        assert!(build_local_agent_url(7070, "@evil.com/v1/status").is_err());
        // 对照：带前导斜杠时 '@' 位于路径内部，不构成 userinfo —— 这是安全的，
        // 必须放行且 host 不变（此前复核报告反驳的正是这一形式，它反驳不了上面的原例）。
        let harmless = build_local_agent_url(7070, "/@evil.com/v1/status").expect("path-internal @");
        assert_eq!(harmless.host_str(), Some("127.0.0.1"));
        assert_eq!(harmless.port(), Some(7070));
    }

    #[test]
    fn agent_url_rejects_scheme_relative_and_absolute_targets() {
        assert!(build_local_agent_url(7070, "//evil.com/v1/status").is_err());
        assert!(build_local_agent_url(7070, "http://evil.com/v1/status").is_err());
        assert!(build_local_agent_url(7070, "v1/status").is_err());
        assert!(build_local_agent_url(7070, "").is_err());
    }

    #[test]
    fn agent_url_rejects_traversal_and_separators() {
        assert!(build_local_agent_url(7070, "/v1/../admin").is_err());
        assert!(build_local_agent_url(7070, "/v1\\..\\admin").is_err());
        assert!(build_local_agent_url(7070, "/v1/status\0").is_err());
        assert!(build_local_agent_url(7070, &format!("/{}", "a".repeat(5000))).is_err());
    }

    #[test]
    fn agent_url_keeps_legitimate_paths_on_loopback() {
        let url = build_local_agent_url(7070, "/v1/status?verbose=1").expect("valid path");
        assert_eq!(url.host_str(), Some("127.0.0.1"));
        assert_eq!(url.port(), Some(7070));
        assert!(url.username().is_empty());
        // 编码后的 @ 留在路径里，不构成 userinfo，也不改变 host。
        let encoded = build_local_agent_url(7070, "/v1/%40evil.com").expect("valid path");
        assert_eq!(encoded.host_str(), Some("127.0.0.1"));
        assert_eq!(encoded.path(), "/v1/%40evil.com");
    }

    #[test]
    fn agent_url_respects_requested_port() {
        let url = build_local_agent_url(7072, "/v1/status").expect("valid path");
        assert_eq!(url.port(), Some(7072));
    }
}
