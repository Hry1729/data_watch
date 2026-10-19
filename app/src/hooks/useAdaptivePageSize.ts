import { useLayoutEffect, useRef, useState } from "react";

const INITIAL_PAGE_SIZE = 10;
const ROW_HEIGHT = 40;

type FitOptions = {
  /** 数据行高（px） */
  rowHeight: number;
  /** 表头高度（px），从视口高度里扣除 */
  headerHeight?: number;
  /** 至少显示的行数 */
  minRows?: number;
};

/**
 * 根据表格视口高度计算每页行数。
 * - 不传参数：保持旧行为（首次高度为 10 行基准，之后按 40px 增减）。
 * - 传 { rowHeight, headerHeight }：视口需由布局撑满（flex-1 min-h-0），每页行数 = 可用高度 / 行高，正好铺满不滚动。
 */
export function useAdaptivePageSize(fit?: FitOptions) {
  const tableViewportRef = useRef<HTMLDivElement>(null);
  const baseHeightRef = useRef<number | null>(null);
  const [pageSize, setPageSize] = useState(INITIAL_PAGE_SIZE);
  const rowHeight = fit?.rowHeight;
  const headerHeight = fit?.headerHeight ?? 40;
  const minRows = fit?.minRows ?? 5;

  useLayoutEffect(() => {
    const viewport = tableViewportRef.current;
    if (!viewport) return;

    const updatePageSize = (height: number) => {
      if (rowHeight) {
        if (height <= 0) return;
        setPageSize(Math.max(minRows, Math.floor((height - headerHeight) / rowHeight)));
        return;
      }
      if (baseHeightRef.current === null) {
        baseHeightRef.current = height;
        setPageSize(INITIAL_PAGE_SIZE);
        return;
      }

      const extraRows = Math.max(0, Math.floor((height - baseHeightRef.current) / ROW_HEIGHT));
      setPageSize(INITIAL_PAGE_SIZE + extraRows);
    };

    const observer = new ResizeObserver(([entry]) => updatePageSize(entry.contentRect.height));
    observer.observe(viewport);
    return () => observer.disconnect();
  }, [headerHeight, minRows, rowHeight]);

  return { pageSize, tableViewportRef };
}
