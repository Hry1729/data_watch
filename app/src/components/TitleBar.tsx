import { useEffect, useState } from "react";
import { getCurrentWindow } from "@tauri-apps/api/window";
import { Copy, Minus, Square, X } from "lucide-react";
import appIconUrl from "../../src-tauri/icons/128x128.png";

const appWindow = getCurrentWindow();

export function TitleBar() {
  const [maximized, setMaximized] = useState(false);

  useEffect(() => {
    let unlisten: (() => void) | undefined;
    appWindow.isMaximized().then(setMaximized);
    appWindow
      .onResized(() => appWindow.isMaximized().then(setMaximized))
      .then((fn) => {
        unlisten = fn;
      });
    return () => unlisten?.();
  }, []);

  return (
    <div
      data-tauri-drag-region
      className="flex h-8 shrink-0 select-none items-center justify-between border-b border-border bg-white pl-3 text-xs text-muted-foreground dark:bg-card"
    >
      <div data-tauri-drag-region className="flex items-center gap-2">
        <img
          src={appIconUrl}
          alt=""
          className="h-4 w-4 shrink-0 rounded-sm object-cover"
        />
        <span className="font-medium">Data Watch Console</span>
      </div>
      <div className="flex h-full">
        <button
          type="button"
          onClick={() => appWindow.minimize()}
          aria-label="最小化"
          className="flex h-full w-11 items-center justify-center transition-colors hover:bg-accent"
        >
          <Minus className="h-4 w-4" />
        </button>
        <button
          type="button"
          onClick={() => appWindow.toggleMaximize()}
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
          onClick={() => appWindow.close()}
          aria-label="关闭"
          className="flex h-full w-11 items-center justify-center transition-colors hover:bg-destructive hover:text-white"
        >
          <X className="h-4 w-4" />
        </button>
      </div>
    </div>
  );
}
