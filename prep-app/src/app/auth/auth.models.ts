/** What the API returns for a user: never a password or hash. */
export interface AuthUser {
  name: string;
  email: string;
}

export interface SignupPayload {
  name: string;
  email: string;
  password: string;
}

export interface LoginPayload {
  email: string;
  password: string;
}
