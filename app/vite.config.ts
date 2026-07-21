import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import tailwindcss from "@tailwindcss/vite";
import path from "path";

// @ts-expect-error process is a nodejs global
const host = process.env.TAURI_DEV_HOST;

// https://vite.dev/config/
export default defineConfig(async () => ({
  plugins: [react(), tailwindcss()],

  resolve: {
    alias: {
      "@": path.resolve(__dirname, "./src"),
    },
  },

  // 针对 Tauri 开发定制的 Vite 选项，仅在 `tauri dev` 或 `tauri build` 时应用
  //
  // 1. 防止 Vite 掩盖 Rust 错误
  clearScreen: false,
  // 2. Tauri 期望固定端口，如果端口不可用则失败
  server: {
    port: 1420,
    strictPort: true,
    host: host || false,
    hmr: host
      ? {
          protocol: "ws",
          host,
          port: 1421,
        }
      : undefined,
    watch: {
      // 3. 告诉 Vite 忽略监视 `src-tauri`
      ignored: ["**/src-tauri/**"],
    },
  },
}));
