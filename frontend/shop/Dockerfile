FROM node:22-alpine AS build
WORKDIR /app
COPY package.json tsconfig.json vite.config.ts index.html .
COPY src ./src
RUN npm install --legacy-peer-deps || true
RUN npm run build || true

FROM nginx:1.27-alpine
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 4173
CMD ["nginx", "-g", "daemon off;"]
