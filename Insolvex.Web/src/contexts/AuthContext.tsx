import React, { createContext, useContext, useState, useEffect, useCallback } from "react";
import { authApi } from "@/services/api";
import type { UserDto } from "@/services/api/types";

interface AuthContextValue {
  user: UserDto | null;
  loading: boolean;
  login: (email: string, password: string) => Promise<void>;
  logout: () => void;
  isAuthenticated: boolean;
  isGlobalAdmin: boolean;
  isTenantAdmin: boolean;
  isPractitioner: boolean;
}

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [user, setUser] = useState<UserDto | null>(null);
const [loading, setLoading] = useState(true);

  const checkAuth = useCallback(async () => {
    const token = localStorage.getItem("authToken");
    if (!token) {
      setLoading(false);
 return;
    }
    try {
      const res = await authApi.getCurrentUser();
      setUser(res.data);
    } catch {
      localStorage.removeItem("authToken");
      setUser(null);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
  checkAuth();
  }, [checkAuth]);

  const login = useCallback(async (email: string, password: string) => {
    const res = await authApi.login({ email, password });
    localStorage.setItem("authToken", res.data.token);
    setUser(res.data.user);
  }, []);

  const logout = useCallback(() => {
localStorage.removeItem("authToken");
    localStorage.removeItem("selectedTenantId");
    setUser(null);
  }, []);

  const value: AuthContextValue = {
    user,
    loading,
    login,
    logout,
    isAuthenticated: !!user,
  isGlobalAdmin: user?.role === "globalAdmin",
    isTenantAdmin: user?.role === "tenantAdmin",
 isPractitioner: user?.role === "practitioner",
};

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth must be used within AuthProvider");
  return ctx;
}
