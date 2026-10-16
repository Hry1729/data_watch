export interface SegmentedPillOption {
  value: string;
  label: string;
}

/** 二级菜单胶囊切换条：白底轨道 + 选中项品牌蓝填充 */
export function SegmentedPills({
  value,
  options,
  onChange,
  ariaLabel,
}: {
  value: string;
  options: readonly SegmentedPillOption[];
  onChange: (value: string) => void;
  ariaLabel: string;
}) {
  if (options.length === 0) return null;

  return (
    <div
      role="tablist"
      aria-label={ariaLabel}
      className="flex max-w-full min-w-0 items-center gap-1 overflow-x-auto rounded-3xl bg-white p-1.5 shadow-card dark:bg-sidebar"
    >
      {options.map((option) => {
        const isActive = option.value === value;
        return (
          <button
            key={option.value}
            type="button"
            role="tab"
            aria-selected={isActive}
            onClick={() => onChange(option.value)}
            className={[
              "flex h-8 shrink-0 items-center rounded-3xl px-3 whitespace-nowrap text-xs transition-colors",
              isActive
                ? "bg-brand font-medium text-brand-foreground"
                : "text-muted-foreground hover:bg-muted hover:text-foreground",
            ].join(" ")}
          >
            {option.label}
          </button>
        );
      })}
    </div>
  );
}
