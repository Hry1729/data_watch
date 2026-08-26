/** User creation/reset are server-generated-password flows. */
export function stripClientPassword<T extends object>(data: T): Omit<T, "password"> {
  return Object.fromEntries(Object.entries(data).filter(([key]) => key !== "password")) as Omit<T, "password">;
}

export function buildPasswordResetRequest(): Record<string, never> {
  return {};
}
