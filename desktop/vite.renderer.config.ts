import { defineConfig } from "vite";
import path from "node:path";

// https://vitejs.dev/config
export default defineConfig(async () => {
  const { default: tailwindcss } = await import("@tailwindcss/vite");
  return {
    plugins: [tailwindcss()],
    publicDir: path.resolve(__dirname, "assets"),
    resolve: {
      alias: {
        "@": path.resolve(__dirname, "./src/renderer/src"),
      },
    },
  };
});
