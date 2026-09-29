export function settleDashboardModules(loaders: Array<() => Promise<unknown>>) {
  return Promise.allSettled(loaders.map((load) => load()))
}
