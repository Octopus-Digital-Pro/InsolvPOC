import client from "./client";
import type { LoginRequest, LoginResponse, UserDto } from "./types";

export const authApi = {
  login: (data: LoginRequest) =>
    client.post<LoginResponse>("/auth/login", data),

  getCurrentUser: (signal?: AbortSignal) =>
    client.get<UserDto>("/auth/me", { signal }),

  changePassword: (data: { currentPassword: string; newPassword: string }) =>
    client.post("/auth/change-password", data),
};
