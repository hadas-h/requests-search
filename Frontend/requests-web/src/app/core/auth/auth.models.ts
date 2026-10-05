/** Authenticated user identity resolved from the JWT on login/register. */
export interface AuthUser {
  id: number;
  username: string;
  isAdministrator: boolean;
}

/** Response shape returned by the auth endpoints. */
export interface AuthResult {
  token: string;
  user: AuthUser;
}
