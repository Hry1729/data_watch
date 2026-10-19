import { ToggleGroup, ToggleGroupItem } from "@/components/ui/toggle-group";

export type LogView = "realtime" | "history";

const ITEM_CLASS =
  "h-[22px] rounded-full px-[11px] text-xs font-medium text-muted-foreground transition-colors hover:bg-transparent data-[state=on]:bg-background data-[state=on]:text-foreground data-[state=on]:shadow-[0_1px_2px_rgba(0,0,0,0.1)] dark:data-[state=on]:bg-input";

/** 日志卡头部的 实时 / 历史 胶囊分段开关 */
export function LogViewToggle({
  value,
  onChange,
}: {
  value: LogView;
  onChange: (value: LogView) => void;
}) {
  return (
    <ToggleGroup
      type="single"
      value={value}
      onValueChange={(next) => {
        if (next) onChange(next as LogView);
      }}
      spacing={1}
      className="h-[26px] rounded-full bg-muted p-0.5"
    >
      <ToggleGroupItem value="realtime" className={ITEM_CLASS}>
        实时
      </ToggleGroupItem>
      <ToggleGroupItem value="history" className={ITEM_CLASS}>
        历史
      </ToggleGroupItem>
    </ToggleGroup>
  );
}
