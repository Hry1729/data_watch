import { useCallback, useEffect, useRef, useState, type ReactNode } from "react";
import { cn } from "@/lib/utils";

/** 指示条最小长度（px），内容极长时也保证可抓取 */
const THUMB_MIN = 28;

/** 量取视口与内容的滚动几何 */
function measure(el: HTMLElement) {
  const { clientHeight, scrollHeight, scrollTop } = el;
  const scrollable = scrollHeight - clientHeight;
  if (scrollable <= 1) return { top: 0, size: 0, maxTop: 0, scrollable: 0 };
  const size = Math.max(THUMB_MIN, (clientHeight / scrollHeight) * clientHeight);
  const maxTop = clientHeight - size;
  return { top: (scrollTop / scrollable) * maxTop, size, maxTop, scrollable };
}

/**
 * 悬浮滚动容器：隐藏原生滚动条，右侧浮出一条细指示条。
 * 指针进入容器或拖动时淡入，其余时间隐藏，避免常驻遮挡内容。
 */
export function OverlayScrollArea({
  className,
  viewportClassName,
  thumbClassName,
  onViewportScroll,
  children,
}: {
  className?: string;
  /** 附加到滚动视口上（如顶部内边距：内容起点的视觉留白，同时让上方区域可被穿过） */
  viewportClassName?: string;
  /** 指示条的水平位置，默认贴容器右侧内缘；容器外侧留有余量时可把它推进那道余量里 */
  thumbClassName?: string;
  /** 视口滚动时上报 scrollTop（壳层据此判断内容是否已滚到顶栏下面） */
  onViewportScroll?: (scrollTop: number) => void;
  children: ReactNode;
}) {
  const viewportRef = useRef<HTMLDivElement>(null);
  const [bar, setBar] = useState({ top: 0, size: 0 });
  const [hovering, setHovering] = useState(false);
  const [dragging, setDragging] = useState(false);

  const sync = useCallback(() => {
    const el = viewportRef.current;
    if (!el) return;
    const { top, size } = measure(el);
    setBar({ top, size });
  }, []);

  // 视口或内容尺寸变化（数据加载、折叠展开、窗口缩放）后重新量取
  useEffect(() => {
    const el = viewportRef.current;
    if (!el) return;
    sync();
    const observer = new ResizeObserver(sync);
    observer.observe(el);
    const content = el.firstElementChild;
    if (content) observer.observe(content);
    return () => observer.disconnect();
  }, [sync]);

  function handleScroll() {
    const el = viewportRef.current;
    sync();
    if (el) onViewportScroll?.(el.scrollTop);
  }

  // 拖动指示条：指针在轨道上的比例映射到 scrollTop
  useEffect(() => {
    if (!dragging) return;

    function handleMove(event: PointerEvent) {
      const el = viewportRef.current;
      if (!el) return;
      const { size, maxTop, scrollable } = measure(el);
      if (maxTop <= 0) return;
      const rect = el.getBoundingClientRect();
      const ratio = (event.clientY - rect.top - size / 2) / maxTop;
      el.scrollTop = Math.min(Math.max(ratio, 0), 1) * scrollable;
    }

    function handleUp() {
      setDragging(false);
    }

    window.addEventListener("pointermove", handleMove);
    window.addEventListener("pointerup", handleUp);
    return () => {
      window.removeEventListener("pointermove", handleMove);
      window.removeEventListener("pointerup", handleUp);
    };
  }, [dragging]);

  return (
    <div className={cn("relative", className)}>
      <div
        ref={viewportRef}
        onScroll={handleScroll}
        onPointerEnter={() => setHovering(true)}
        onPointerLeave={() => setHovering((value) => (dragging ? value : false))}
        className={cn(
          "h-full overflow-y-auto overflow-x-hidden [scrollbar-width:none] [&::-webkit-scrollbar]:hidden",
          viewportClassName,
        )}
      >
        {children}
      </div>

      {bar.size > 0 && (
        <div
          onPointerDown={(event) => {
            event.preventDefault();
            setDragging(true);
          }}
          className={cn(
            "absolute w-1.5 cursor-grab rounded-full bg-foreground/25 transition-opacity duration-200 hover:bg-foreground/40 active:cursor-grabbing",
            thumbClassName ?? "right-1",
            hovering || dragging ? "opacity-100" : "opacity-0",
          )}
          style={{ top: bar.top, height: bar.size }}
        />
      )}
    </div>
  );
}
