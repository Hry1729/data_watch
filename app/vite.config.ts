import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import tailwindcss from "@tailwindcss/vite";
import path from "path";
import roleManifest from "../installer/roles.json";

// @ts-expect-error process is a nodejs global
const host = process.env.TAURI_DEV_HOST;
// @ts-expect-error process is a nodejs global
const buildRole = process.env.VITE_DATAWATCH_ROLE || "centralized";
const allowedRoles = new Set(["centralized", "edge", "distributed-center"]);
if (!allowedRoles.has(buildRole)) {
  throw new Error("Unsupported VITE_DATAWATCH_ROLE: " + buildRole);
}

const role = buildRole as keyof typeof roleManifest.roles;
const agentPort = roleManifest.roles[role].agentPort;

// https://vite.dev/config/
export default defineConfig(async () => ({
  define: {
    __DATAWATCH_ROLE__: JSON.stringify(buildRole),
    __DATAWATCH_AGENT_PORT__: JSON.stringify(agentPort),
  },
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
