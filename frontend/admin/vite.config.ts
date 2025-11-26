import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import federation from "@originjs/vite-plugin-federation";

export default defineConfig({
  plugins: [
    react(),
    federation({
      name: "admin",
      filename: "remoteEntry.js",
      exposes: {
        "./AdminApp": "./src/AdminApp.tsx"
      },
      remotes: {
        shop: "http://localhost:5173/assets/remoteEntry.js"
      },
      shared: ["react", "react-dom", "react-router-dom"]
    })
  ],
  build: {
    target: "esnext",
    modulePreload: false,
    minify: false
  }
});
