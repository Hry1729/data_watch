// PUT /admin/config 是逐字段 partial update（Agent apply_db_patch：缺失的键保留原值）。
// 设置页只提交用户相对「已加载配置」真正改过的字段，避免整段回写把未改动的
// host/port/user/database 覆盖成表单里的旧值或默认值（H6）。

/** 返回 current 中与 baseline 不同的字段；password 只在 includePassword 为 true 时提交。 */
export function changedFields<T extends object>(
  baseline: T,
  current: T,
  includePassword = false,
): Partial<T> {
  const patch: Partial<T> = {};
  for (const key of Object.keys(current) as Array<keyof T>) {
    if (key === "password") continue;
    if (current[key] !== baseline[key]) patch[key] = current[key];
  }
  if (includePassword && "password" in current) {
    (patch as Record<string, unknown>).password = (current as Record<string, unknown>).password;
  }
  return patch;
}

/** 按段组装请求体：没有改动的段整段省略。 */
export function buildConfigPatch(
  sections: Record<string, { baseline: object; current: object; includePassword?: boolean }>,
): Record<string, object> {
  const payload: Record<string, object> = {};
  for (const [name, { baseline, current, includePassword }] of Object.entries(sections)) {
    const patch = changedFields(baseline, current, includePassword ?? false);
    if (Object.keys(patch).length > 0) payload[name] = patch;
  }
  return payload;
}
