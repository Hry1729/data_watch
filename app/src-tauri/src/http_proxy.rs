use reqwest::Client;

#[tauri::command]
pub async fn agent_request(
    method: String,
    path: String,
    body: Option<String>,
    token: Option<String>,
) -> Result<(u16, String), String> {
    let url = format!("http://127.0.0.1:7070{}", path);
    let client = Client::new();

    let mut req = match method.as_str() {
        "GET"    => client.get(&url),
        "POST"   => client.post(&url),
        "PUT"    => client.put(&url),
        "DELETE" => client.delete(&url),
        m        => return Err(format!("unsupported method: {}", m)),
    };

    if let Some(t) = token {
        // 不 log token 值
        req = req.header("Authorization", format!("Bearer {}", t));
    }
    if let Some(b) = body {
        // 不 log body 中的 password 字段
        req = req.header("Content-Type", "application/json").body(b);
    }

    let resp = req.send().await
        .map_err(|e| format!("agent_unreachable: {}", e))?;
    let status = resp.status().as_u16();
    let text   = resp.text().await
        .map_err(|e| format!("read body failed: {}", e))?;

    Ok((status, text))
}
