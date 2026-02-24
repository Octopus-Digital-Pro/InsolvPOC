/**
 * Downloads a file from an authenticated API endpoint.
 * Reads the JWT token and tenant ID from localStorage and sends them as headers.
 */
export async function downloadAuthFile(relativeUrl: string, fileName: string): Promise<void> {
  const token = localStorage.getItem("authToken");
  const tenantId = localStorage.getItem("selectedTenantId");
  const baseUrl = (import.meta.env.VITE_API_URL as string | undefined) ?? "/api";
  const url = `${baseUrl}${relativeUrl}`;

  const headers: Record<string, string> = {};
  if (token) headers["Authorization"] = `Bearer ${token}`;
  if (tenantId) headers["X-Tenant-Id"] = tenantId;

  const response = await fetch(url, { headers });

  if (!response.ok) {
    throw new Error(`Download failed: ${response.status} ${response.statusText}`);
  }

  const blob = await response.blob();
  const objectUrl = URL.createObjectURL(blob);
  const anchor = document.createElement("a");
  anchor.href = objectUrl;
  anchor.download = fileName;
  document.body.appendChild(anchor);
  anchor.click();
  document.body.removeChild(anchor);
  URL.revokeObjectURL(objectUrl);
}
