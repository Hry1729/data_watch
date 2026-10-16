import { readFileSync } from "node:fs";
import { runInNewContext } from "node:vm";
import ts from "typescript";

export const settle = () => new Promise(resolve => setImmediate(resolve));
export function deferred() {
  let resolve, reject;
  const promise = new Promise((yes, no) => { resolve = yes; reject = no; });
  return { promise, resolve, reject };
}

// Execute the real hooks with deterministic effects, visibility and timers.
export function hookHarness(file, exportName, mocks = {}, args = []) {
  const slots = [], effects = [], timers = new Map(), listeners = new Map();
  let cursor = 0, queued = false, disposed = false, value;
  const changed = (old, next) => !old || next.some((item, index) => !Object.is(item, old[index]));
  function schedule() {
    if (queued || disposed) return;
    queued = true;
    queueMicrotask(() => { queued = false; if (!disposed) render(); });
  }
  const react = {
    useState(initial) {
      const index = cursor++;
      slots[index] ??= { value: typeof initial === "function" ? initial() : initial };
      return [slots[index].value, next => {
        const result = typeof next === "function" ? next(slots[index].value) : next;
        if (!Object.is(result, slots[index].value)) { slots[index].value = result; schedule(); }
      }];
    },
    useRef(initial) { const index = cursor++; return slots[index] ??= { current: initial }; },
    useCallback(fn, deps) {
      const index = cursor++;
      if (changed(slots[index]?.deps, deps)) slots[index] = { value: fn, deps };
      return slots[index].value;
    },
    useEffect(fn, deps) {
      const index = cursor++;
      if (changed(slots[index]?.deps, deps)) {
        effects.push(() => {
          slots[index]?.cleanup?.();
          slots[index] = { deps, cleanup: fn() };
        });
      }
    },
  };
  const document = {
    hidden: false,
    addEventListener(name, fn) { if (!listeners.has(name)) listeners.set(name, new Set()); listeners.get(name).add(fn); },
    removeEventListener(name, fn) { listeners.get(name)?.delete(fn); },
  };
  const clock = {
    setInterval(fn) { const id = {}; timers.set(id, fn); return id; },
    clearInterval(id) { timers.delete(id); },
  };
  const modules = {
    react,
    "@/contexts/AuthContext": { useAuth: () => ({ auth: { backendToken: "test" }, logout() {} }) },
    "@/contexts/PageRefreshContext": { usePageRefresh: () => ({ refreshVersion: 0, refreshCurrentPage() {} }) },
    "@/config/build-role": { BUILD_ROLE: "centralized" },
    ...mocks,
  };
  function load(path) {
    const exports = {};
    const source = readFileSync(new URL(`../../src/${path}`, import.meta.url), "utf8");
    const code = ts.transpileModule(source, { compilerOptions: { target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.CommonJS } }).outputText;
    runInNewContext(code, {
      exports, document, AbortController, ...clock,
      window: { ...clock, addEventListener: document.addEventListener, removeEventListener: document.removeEventListener },
      require(name) {
        if (modules[name]) return modules[name];
        if (name.startsWith("@/hooks/")) return modules[name] = load(name.slice(2) + ".ts");
        throw new Error(`Unmocked module: ${name}`);
      },
    });
    return exports;
  }
  const hook = load(file)[exportName];
  function render() { cursor = 0; value = hook(...args); while (effects.length) effects.shift()(); }
  render();
  return {
    get value() { return value; },
    render,
    tick() { for (const fn of [...timers.values()]) fn(); },
    visibility(hidden) { document.hidden = hidden; for (const fn of listeners.get("visibilitychange") ?? []) fn(); },
    unmount() { disposed = true; for (const slot of slots) slot?.cleanup?.(); },
  };
}
