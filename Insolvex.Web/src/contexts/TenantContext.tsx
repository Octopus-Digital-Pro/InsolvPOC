import React, { createContext, useContext, useState, useEffect, useCallback } from "react";
import { useAuth } from "./AuthContext";
import { tenantsApi } from "@/services/api";
import type { TenantDto } from "@/services/api/types";

interface TenantContextValue {
  selectedTenant: TenantDto | null;
  availableTenants: TenantDto[];
  selectTenant: (tenant: TenantDto) => void;
  refreshTenants: () => Promise<void>;
  needsTenantSelection: boolean;
  loading: boolean;
}

const TenantContext = createContext<TenantContextValue | null>(null);

export function TenantProvider({ children }: { children: React.ReactNode }) {
  const { user, isGlobalAdmin, isAuthenticated } = useAuth();
  const [selectedTenant, setSelectedTenant] = useState<TenantDto | null>(null);
  const [availableTenants, setAvailableTenants] = useState<TenantDto[]>([]);
  const [loading, setLoading] = useState(false);

  const loadTenants = useCallback(async () => {
    if (!isAuthenticated) return;
    setLoading(true);
    try {
      if (isGlobalAdmin) {
        const res = await tenantsApi.getAll();
        setAvailableTenants(res.data);
        // Auto-select: localStorage > current selection > first tenant
        const savedId = localStorage.getItem("selectedTenantId");
        const current = selectedTenant?.id;
        const matched =
          res.data.find((t) => t.id === (savedId || current)) ??
          (res.data.length > 0 ? res.data[0] : null);
        if (matched) {
          setSelectedTenant(matched);
          localStorage.setItem("selectedTenantId", matched.id);
        }
      } else {
        // Regular users: their tenant is immutable from JWT
        setSelectedTenant({
          id: user!.tenantId,
          name: "",
          domain: null,
          isActive: true,
          subscriptionExpiry: null,
          planName: null,
        });
        setAvailableTenants([]);
      }
    } catch (err) {
      console.error("Failed to load tenants:", err);
    } finally {
      setLoading(false);
    }
  }, [isAuthenticated, isGlobalAdmin, user]); // eslint-disable-line react-hooks/exhaustive-deps

  useEffect(() => {
    loadTenants();
  }, [loadTenants]);

  const selectTenant = useCallback(
    (tenant: TenantDto) => {
      setSelectedTenant(tenant);
      localStorage.setItem("selectedTenantId", tenant.id);
      // Force full page reload to flush all cached data for the previous tenant.
      // This is the nuclear option but it guarantees zero cross-tenant data leakage.
      window.location.reload();
    },
    []
  );

  const refreshTenants = useCallback(async () => {
    await loadTenants();
  }, [loadTenants]);

  const value: TenantContextValue = {
    selectedTenant,
    availableTenants,
    selectTenant,
    refreshTenants,
    needsTenantSelection: isGlobalAdmin && !selectedTenant,
    loading,
  };

  return <TenantContext.Provider value={value}>{children}</TenantContext.Provider>;
}

export function useTenant() {
  const ctx = useContext(TenantContext);
  if (!ctx) throw new Error("useTenant must be used within TenantProvider");
  return ctx;
}
