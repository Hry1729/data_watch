import { invoke } from "@tauri-apps/api/core";

export function isAbortError(error: unknown): boolean {
  return error instanceof Error && error.name === "AbortError";
}

/** Register before dispatch so cancellation also wins while the HTTP command is queued. */
export async function invokeHttp<T>(
  command: string,
  args: Record<string, unknown>,
  signal?: AbortSignal,
): Promise<T> {
  if (!signal) return invoke<T>(command, args);
  signal.throwIfAborted();
  const requestId = crypto.randomUUID();
  await invoke<void>("register_http_request", { requestId });
  const cancel = () => { void invoke<void>("cancel_http_request", { requestId }).catch(() => undefined); };
  signal.addEventListener("abort", cancel, { once: true });
  try {
    if (signal.aborted) cancel();
    const result = await invoke<T>(command, { ...args, requestId });
    signal.throwIfAborted();
    return result;
  } catch (error) {
    // Also release registrations if dispatch itself fails before Rust takes ownership.
    cancel();
    if (signal.aborted) throw signal.reason ?? new DOMException("Request cancelled", "AbortError");
    throw error;
  } finally {
    signal.removeEventListener("abort", cancel);
  }
}
