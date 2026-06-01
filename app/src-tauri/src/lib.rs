mod http_proxy;
mod tray;

#[cfg_attr(mobile, tauri::mobile_entry_point)]
pub fn run() {
    tauri::Builder::default()
        .plugin(tauri_plugin_opener::init())
        .invoke_handler(tauri::generate_handler![
            http_proxy::agent_request,
            tray::show_window,
            tray::hide_to_tray,
        ])
        .run(tauri::generate_context!())
        .expect("error while running tauri application");
}
