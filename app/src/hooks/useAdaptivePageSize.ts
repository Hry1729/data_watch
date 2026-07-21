import { useLayoutEffect, useRef, useState } from "react";

const INITIAL_PAGE_SIZE = 10;
const ROW_HEIGHT = 40;

export function useAdaptivePageSize() {
  const tableViewportRef = useRef<HTMLDivElement>(null);
  const baseHeightRef = useRef<number | null>(null);
  const [pageSize, setPageSize] = useState(INITIAL_PAGE_SIZE);

  useLayoutEffect(() => {
    const viewport = tableViewportRef.current;
    if (!viewport) return;

    const updatePageSize = (height: number) => {
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
  }, []);

  return { pageSize, tableViewportRef };
}
