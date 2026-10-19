import { ToggleGroup, ToggleGroupItem } from "@/components/ui/toggle-group";

export type LogView = "realtime" | "history";

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
      className="rounded-full bg-muted p-0.5"
    >
      <ToggleGroupItem
        value="realtime"
        className="h-6 rounded-full px-2.5 text-xs text-muted-foreground data-[state=on]:bg-card data-[state=on]:text-foreground data-[state=on]:shadow-sm"
      >
        实时
      </ToggleGroupItem>
      <ToggleGroupItem
        value="history"
        className="h-6 rounded-full px-2.5 text-xs text-muted-foreground data-[state=on]:bg-card data-[state=on]:text-foreground data-[state=on]:shadow-sm"
      >
        历史
      </ToggleGroupItem>
    </ToggleGroup>
  );
}
