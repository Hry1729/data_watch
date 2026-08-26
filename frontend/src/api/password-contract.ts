/** The reset endpoint generates the password; clients must never send one. */
export function buildPasswordResetRequest(): Record<string, never> {
  return {}
}

/** User creation also generates a one-time password; strip stale UI fields. */
export function stripClientPassword<T extends object>(data: T): Omit<T, 'password'> {
  return Object.fromEntries(Object.entries(data).filter(([key]) => key !== 'password')) as Omit<T, 'password'>
}

export interface PasswordResetResponse {
  id: number
  userId: number
  temporaryPassword: string
  mustChangePassword: true
  tokenVersion: number
}
