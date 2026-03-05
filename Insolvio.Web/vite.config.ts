import path from "node:path";
import { fileURLToPath } from "node:url";
import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import tailwindcss from "@tailwindcss/vite";

const __dirname = path.dirname(fileURLToPath(import.meta.url));

export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: { "@": path.resolve(__dirname, "src") },
  },
  server: {
    proxy: {
      "/api": {
        target: "http://localhost:5000",
        changeOrigin: true,
        proxyTimeout: 1_200_000, // 20 minutes — 600 MB upload + server-side processing
        timeout: 1_200_000,
        configure: (proxy) => {
          proxy.on("error", (err: NodeJS.ErrnoException, _req, res) => {
            if (err.code === "ECONNREFUSED") {
              // API not up yet — return 503 silently instead of crashing the log
              if ("writeHead" in res) {
                (res as import("http").ServerResponse).writeHead(503, {
                  "Content-Type": "application/json",
                });
                (res as import("http").ServerResponse).end(
                  JSON.stringify({ error: "API server starting, please retry" })
                );
              }
            }
          });
        },
      },
    },
  },
});
