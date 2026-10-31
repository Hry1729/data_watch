import { useEffect, useState } from "react";
import { getCurrentWindow } from "@tauri-apps/api/window";
import { isTauri } from "@tauri-apps/api/core";
import { Copy, Minus, Square, X } from "lucide-react";
import { BrandMark } from "@/components/BrandMark";

export const TITLEBAR_SLOT_ID = "dw-titlebar-slot";
export const TITLEBAR_STATUS_SLOT_ID = "dw-titlebar-status-slot";

const appWindow = isTauri() ? getCurrentWindow() : null;

export function TitleBar() {
  const [maximized, setMaximized] = useState(false);

  useEffect(() => {
    if (!appWindow) return;
    const currentWindow = appWindow;
    let active = true;
    let unlisten: (() => void) | undefined;
    const updateMaximized = () => {
      void currentWindow.isMaximized()
        .then(value => {
          if (!active) return;
          setMaximized(value);
          // 最大化时取消外壳圆角(见 index.css)，否则四角会露出桌面
          document.documentElement.toggleAttribute("data-window-maximized", value);
        })
        .catch(() => undefined);
    };
    updateMaximized();
    currentWindow
      .onResized(updateMaximized)
      .then((fn) => {
        if (active) unlisten = fn;
        else fn();
      }).catch(() => undefined);
    return () => { active = false; unlisten?.(); };
  }, []);

  return (
    <div
      data-tauri-drag-region
      className="flex h-9 shrink-0 select-none items-center justify-between bg-sidebar pl-3 text-xs text-muted-foreground"
    >
      <div data-tauri-drag-region className="flex items-center gap-2">
        <BrandMark className="size-[18px]" title="Data Watch" />
        {/* AppLayout 通过 portal 把「折叠导航栏 / 刷新」按钮放进这里 */}
        <div id={TITLEBAR_SLOT_ID} className="ml-2 flex items-center gap-1.5" />
      </div>
      <div className="flex h-full items-center">
        {/* AppLayout 通过 portal 把数据库连接状态放进这里 */}
        <div id={TITLEBAR_STATUS_SLOT_ID} className="mr-3 flex items-center" />
        <button
          type="button"
          disabled={!appWindow}
          onClick={() => appWindow?.minimize()}
          aria-label="最小化"
          className="flex h-full w-11 items-center justify-center transition-colors hover:bg-accent"
        >
          <Minus className="h-4 w-4" />
        </button>
        <button
          type="button"
          disabled={!appWindow}
          onClick={() => appWindow?.toggleMaximize()}
          aria-label="最大化"
          className="flex h-full w-11 items-center justify-center transition-colors hover:bg-accent"
        >
          {maximized ? (
            <Copy className="h-3.5 w-3.5" />
          ) : (
            <Square className="h-3.5 w-3.5" />
          )}
        </button>
        <button
          type="button"
          disabled={!appWindow}
          onClick={() => appWindow?.close()}
          aria-label="关闭"
          className="flex h-full w-11 items-center justify-center transition-colors hover:bg-destructive hover:text-white"
        >
          <X className="h-4 w-4" />
        </button>
      </div>
    </div>
  );
}
