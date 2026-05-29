import { defineConfig } from "vite";
import path from "node:path";

// https://vitejs.dev/config
export default defineConfig(async () => {
  const { default: react } = await import("@vitejs/plugin-react");
  const { default: tailwindcss } = await import("@tailwindcss/vite");
  return {
    plugins: [react(), tailwindcss()],
    publicDir: path.resolve(__dirname, "./assets"),
    resolve: {
      alias: {
        "@": path.resolve(__dirname, "./src"),
      },
    },
    build: {
      rollupOptions: {
        input: {
          main: path.resolve(__dirname, "index.html"),
          overlay: path.resolve(__dirname, "overlay.html"),
        },
      },
    },
  };
});
