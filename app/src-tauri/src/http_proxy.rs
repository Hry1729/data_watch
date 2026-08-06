use std::time::Duration;

use reqwest::{header::CONTENT_DISPOSITION, Client};

fn build_client(timeout: Duration) -> Result<Client, String> {
    Client::builder()
        .connect_timeout(Duration::from_secs(3))
        .timeout(timeout)
        .build()
        .map_err(|e| format!("http client initialization failed: {}", e))
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
) -> Result<(u16, Vec<u8>, Option<String>), String> {
    let base = base_url.trim_end_matches('/');
    if base.is_empty() {
        return Err("backend_base_url is empty".to_string());
    }

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
    let bytes = resp
        .bytes()
        .await
        .map_err(|e| format!("read body failed: {}", e))?
        .to_vec();

    Ok((status, bytes, disposition))
}

#[tauri::command]
pub async fn backend_request(
    base_url: String,
    method: String,
    path: String,
    body: Option<String>,
    authorization: Option<String>,
) -> Result<(u16, String), String> {
    let base = base_url.trim_end_matches('/');
    if base.is_empty() {
        return Err("backend_base_url is empty".to_string());
    }
    let url = format!("{}{}", base, path);
    let client = build_client(Duration::from_secs(10))?;

    let mut req = match method.as_str() {
        "GET" => client.get(&url),
        "POST" => client.post(&url),
        "PUT" => client.put(&url),
        "PATCH" => client.patch(&url),
        "DELETE" => client.delete(&url),
        m => return Err(format!("unsupported method: {}", m)),
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
    let text = resp
        .text()
        .await
        .map_err(|e| format!("read body failed: {}", e))?;

    Ok((status, text))
}
