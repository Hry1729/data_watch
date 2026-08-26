use std::sync::Mutex;
use std::time::Duration;

use reqwest::{header::CONTENT_DISPOSITION, redirect::Policy, Client, Url};
use tauri::State;

pub const MAX_BACKEND_RESPONSE_BYTES: usize = 50 * 1024 * 1024;
const MAX_BACKEND_REQUEST_BYTES: usize = 4 * 1024 * 1024;

#[derive(Default)]
pub struct BackendOriginState(pub Mutex<Option<String>>);

fn build_client(timeout: Duration) -> Result<Client, String> {
    Client::builder()
        .connect_timeout(Duration::from_secs(3))
        .timeout(timeout)
        .redirect(Policy::none())
        .build()
        .map_err(|e| format!("http client initialization failed: {}", e))
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
    let url = format!("http://127.0.0.1:{}{}", port, path);
    let timeout = Duration::from_secs(timeout_secs.unwrap_or(5).max(1));
    let client = build_client(timeout)?;

    let mut req = match method.as_str() {
        "GET" => client.get(&url),
        "POST" => client.post(&url),
        "PUT" => client.put(&url),
        "DELETE" => client.delete(&url),
        m => return Err(format!("unsupported method: {}", m)),
    };

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

    let client = build_client(Duration::from_secs(60))?;
    let mut req = client.get(format!("{}{}", base, path));
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
    let client = build_client(Duration::from_secs(10))?;

    let mut req = match method.as_str() {
        "GET" => client.get(&url),
        "POST" => client.post(&url),
        "PUT" => client.put(&url),
        "PATCH" => client.patch(&url),
        "DELETE" => client.delete(&url),
        _ => unreachable!("method validated above"),
    };

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
        normalize_backend_origin, validate_backend_method, validate_backend_path,
        validate_request_body, MAX_BACKEND_REQUEST_BYTES, MAX_BACKEND_RESPONSE_BYTES,
    };

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
}
