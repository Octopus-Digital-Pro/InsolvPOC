import client from "./client";
import type { LoginRequest, LoginResponse, UserDto } from "./types";

export const authApi = {
  login: (data: LoginRequest) =>
    client.post<LoginResponse>("/auth/login", data),

  getCurrentUser: () =>
    client.get<UserDto>("/auth/me"),

  changePassword: (data: { currentPassword: string; newPassword: string }) =>
    client.post("/auth/change-password", data),
};
