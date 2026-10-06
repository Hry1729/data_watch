use std::sync::{Mutex, OnceLock};
use std::time::Duration;

use reqwest::{header::CONTENT_DISPOSITION, redirect::Policy, Client, Url};
use tauri::State;

pub const MAX_BACKEND_RESPONSE_BYTES: usize = 50 * 1024 * 1024;
const MAX_BACKEND_REQUEST_BYTES: usize = 4 * 1024 * 1024;

#[derive(Default)]
pub struct BackendOriginState(pub Mutex<Option<String>>);

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

async fn read_limited_bytes(mut response: reqwest::Response) -> Result<Vec<u8>, String> {
    if response
        .content_length()
        .is_some_and(|length| length > MAX_BACKEND_RESPONSE_BYTES as u64)
    {
        return Err("backend response is too large".to_string());
    }

    let mut bytes = Vec::new();
    while let Some(chunk) = response.chunk().await.map_err(|e| format!("read body failed: {}", e))? {
        if bytes.len().saturating_add(chunk.len()) > MAX_BACKEND_RESPONSE_BYTES {
            return Err("backend response is too large".to_string());
        }
        bytes.extend_from_slice(&chunk);
    }
    Ok(bytes)
}

async fn read_limited_text(response: reqwest::Response) -> Result<String, String> {
    String::from_utf8(read_limited_bytes(response).await?)
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
) -> Result<(u16, String), String> {
    let url = build_local_agent_url(port, &path)?;
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
    let text = resp
        .text()
        .await
        .map_err(|e| format!("read body failed: {}", e))?;

    Ok((status, text))
}

#[tauri::command]
pub async fn backend_download(
    base_url: String,
    path: String,
    authorization: Option<String>,
    state: State<'_, BackendOriginState>,
) -> Result<(u16, Vec<u8>, Option<String>), String> {
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
    let bytes = read_limited_bytes(resp).await?;

    Ok((status, bytes, disposition))
}

#[tauri::command]
pub async fn backend_request(
    base_url: String,
    method: String,
    path: String,
    body: Option<String>,
    authorization: Option<String>,
    state: State<'_, BackendOriginState>,
) -> Result<(u16, String), String> {
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
}

#[cfg(test)]
mod tests {
    use super::{
        build_local_agent_url, normalize_backend_origin, validate_backend_method,
        validate_backend_path, validate_request_body, MAX_BACKEND_REQUEST_BYTES,
        MAX_BACKEND_RESPONSE_BYTES,
    };

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
