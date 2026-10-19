import { Button } from "@/components/ui/button";

type Props = {
  total: number;
  page: number;
  pageSize: number;
  loading: boolean;
  onPageChange: (page: number) => void;
};

export function DeviceCatalogPagination({ total, page, pageSize, loading, onPageChange }: Props) {
  const pages = Math.max(1, Math.ceil(total / pageSize));
  return (
    <div className="flex items-center justify-between gap-2 text-xs text-muted-foreground">
      <span className="tabular-nums">共 {total} 台设备 · 第 {page} / {pages} 页</span>
      <div className="flex gap-2">
        <Button type="button" variant="outline" size="sm" className="h-7 px-2.5 text-xs" disabled={loading || page <= 1} onClick={() => onPageChange(page - 1)}>上一页</Button>
        <Button type="button" variant="outline" size="sm" className="h-7 px-2.5 text-xs" disabled={loading || page >= pages} onClick={() => onPageChange(page + 1)}>下一页</Button>
      </div>
    </div>
  );
}
