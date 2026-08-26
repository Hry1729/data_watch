export interface PasswordChangeCompletion {
  clearSession: true
  redirectToLogin: true
}

/** Changing a password rotates tokenVersion; the old access/refresh tokens are unusable. */
export function passwordChangeCompletion(): PasswordChangeCompletion {
  return { clearSession: true, redirectToLogin: true }
}
