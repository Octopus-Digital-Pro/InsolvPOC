import axios from "axios";

const client = axios.create({
  baseURL: import.meta.env.VITE_API_URL || "/api",
  headers: {
    "Content-Type": "application/json",
  },
});

// Request interceptor: add auth token and tenant header
client.interceptors.request.use((config) => {
  const token = localStorage.getItem("authToken");
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }

  const selectedTenantId = localStorage.getItem("selectedTenantId");
  if (selectedTenantId) {
    config.headers["X-Tenant-Id"] = selectedTenantId;
  }

  // For FormData uploads, remove the default application/json Content-Type so
  // the browser can set multipart/form-data with the correct boundary parameter.
  if (config.data instanceof FormData) {
    delete config.headers["Content-Type"];
  }

  return config;
});

// Response interceptor: handle 401
client.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response?.status === 401) {
      localStorage.removeItem("authToken");
      window.location.href = "/login";
    }
    return Promise.reject(error);
  }
);

export default client;
