import { Construction } from "lucide-react";

export function PlaceholderPage({ title }: { title: string }) {
  return (
    <div className="flex min-h-full items-center justify-center px-6 py-12">
      <div className="flex max-w-sm flex-col items-center text-center">
        <div className="mb-4 flex h-11 w-11 items-center justify-center rounded-md border border-border bg-card text-muted-foreground">
          <Construction className="h-5 w-5" />
        </div>
        <h1 className="text-lg font-semibold text-foreground">{title}</h1>
        <p className="mt-1 text-sm text-muted-foreground">页面待复刻</p>
      </div>
    </div>
  );
}
