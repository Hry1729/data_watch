export interface BackendTokens {
  token: string;
  refreshToken: string;
  mustChangePassword?: boolean;
}

interface Session {
  tokens: BackendTokens;
  knownTokens: Set<string>;
  controller: AbortController;
  onRefresh: (tokens: BackendTokens) => void;
  pending: Promise<void> | null;
}

let session: Session | null = null;
const aborted = () => new DOMException("Session or request is no longer active", "AbortError");
const normalize = (token: string) => token.replace(/^Bearer\s+/i, "");

export function clearBackendSession() {
  session?.controller.abort();
  session = null;
}

export function setBackendSession(tokens: BackendTokens, onRefresh: Session["onRefresh"]) {
  clearBackendSession();
  if (!tokens.token || !tokens.refreshToken) return;
  session = { tokens, onRefresh, knownTokens: new Set([normalize(tokens.token)]), controller: new AbortController(), pending: null };
}

function waitForRefresh(pending: Promise<void>, signal?: AbortSignal): Promise<void> {
  if (!signal) return pending;
  if (signal.aborted) return Promise.reject(aborted());
  return new Promise((resolve, reject) => {
    const onAbort = () => reject(aborted());
    signal.addEventListener("abort", onAbort, { once: true });
    pending.then(resolve, reject).finally(() => signal.removeEventListener("abort", onAbort));
  });
}

/** A session refresh is shared; a caller can stop waiting without cancelling other callers. */
export async function withBackendSession<T>(
  token: string | undefined,
  request: (token: string | undefined) => Promise<T>,
  refresh: (refreshToken: string, signal: AbortSignal) => Promise<BackendTokens>,
  signal?: AbortSignal,
): Promise<T> {
  const current = token && session?.knownTokens.has(normalize(token)) ? session : null;
  const checkActive = () => {
    if (signal?.aborted || (current && session !== current)) throw aborted();
  };
  checkActive();
  const sentToken = current?.tokens.token ?? token;
  try {
    const result = await request(sentToken);
    checkActive();
    return result;
  } catch (error) {
    checkActive();
    if (!current || (error as { type?: string })?.type !== "unauthorized") throw error;
    // Another request may already have renewed this exact expired access token.
    if (current.tokens.token === sentToken) {
      if (!current.pending) {
        current.pending = (async () => {
          try {
            const next = await refresh(current.tokens.refreshToken, current.controller.signal);
            if (session !== current) throw aborted();
            if (!next.token || !next.refreshToken) throw { type: "unauthorized" };
            current.tokens = next;
            current.knownTokens.add(normalize(next.token));
            if (current.knownTokens.size > 8) current.knownTokens.delete(current.knownTokens.values().next().value!);
            current.onRefresh(next);
          } catch (reason) {
            if (session !== current) throw aborted();
            // Disabled accounts/removed roles cannot be revived by repeated refreshes.
            if ((reason as { status?: number })?.status === 403) throw { type: "unauthorized" };
            throw reason;
          } finally {
            current.pending = null;
          }
        })();
      }
      await waitForRefresh(current.pending, signal);
    }
    checkActive();
    const result = await request(current.tokens.token);
    checkActive();
    return result;
  }
}
