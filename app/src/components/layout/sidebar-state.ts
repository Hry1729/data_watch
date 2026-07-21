export const SIDEBAR_STORAGE_KEY = "data-watch-sidebar-collapsed";

interface ReadableStorage {
  getItem(key: string): string | null;
}

interface WritableStorage {
  setItem(key: string, value: string): void;
}

export function readSidebarCollapsed(storage: ReadableStorage | null): boolean {
  return storage?.getItem(SIDEBAR_STORAGE_KEY) === "true";
}

export function writeSidebarCollapsed(
  storage: WritableStorage,
  collapsed: boolean,
): void {
  storage.setItem(SIDEBAR_STORAGE_KEY, String(collapsed));
}
