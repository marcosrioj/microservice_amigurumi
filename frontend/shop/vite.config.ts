import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import federation from "@originjs/vite-plugin-federation";

export default defineConfig({
  plugins: [
    react(),
    federation({
      name: "shop",
      filename: "remoteEntry.js",
      exposes: {
        "./ShopApp": "./src/ShopApp.tsx"
      },
      remotes: {
        admin: "http://localhost:5174/assets/remoteEntry.js"
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
