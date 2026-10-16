mod http_proxy;
mod tray;

#[cfg_attr(mobile, tauri::mobile_entry_point)]
pub fn run() {
    tauri::Builder::default()
        .plugin(tauri_plugin_opener::init())
        .manage(http_proxy::BackendOriginState::default())
        .manage(http_proxy::HttpRequestState::default())
        .invoke_handler(tauri::generate_handler![
            http_proxy::agent_request,
            http_proxy::set_backend_origin,
            http_proxy::backend_request,
            http_proxy::backend_download,
            http_proxy::register_http_request,
            http_proxy::cancel_http_request,
            tray::show_window,
            tray::hide_to_tray,
        ])
        .run(tauri::generate_context!())
        .expect("error while running tauri application");
}
