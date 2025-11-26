# Microservice Amigurumi Store

Self-contained microservice + micro-frontend demo for an amigurumi store. The stack uses:

- **.NET 9** minimal APIs (closest available to requested .NET 10)
- **React 19 RC + Bootstrap 5.3** micro-frontends (shop + admin) with Vite module federation
- **Podman Compose** (drop-in Docker alternative) to orchestrate microservices, frontends, and Postgres

> Note: .NET 10 and React 19 stable are not released at the time of scaffolding. Projects target .NET 9.0 and React 19.0.0-rc.0 so you can upgrade in-place once final versions ship.

## Layout

- `backend/Amigurumi.IdentityService` — SSO-style auth issuing JWTs (register, login, refresh, me)
- `backend/Amigurumi.ProductService` — Product catalog CRUD (admin protected)
- `backend/Amigurumi.OrderService` — Checkout + order history
- `backend/Amigurumi.Gateway` — BFF proxy that fronts the other services for the frontends
- `backend/Amigurumi.Contracts` — Shared DTOs/roles
- `frontend/shop` — Customer-facing micro-frontend (catalog, cart, checkout, orders, auth)
- `frontend/admin` — Admin micro-frontend (seed admin, manage catalog)
- `docker-compose.yml` — Builds/starts everything, including Postgres

## Quick start

1) Prereqs: Podman + Podman Compose; Node 20+ if you want to run the frontends locally without containers.  
2) Build/run everything: `podman compose up --build` (works with the existing `docker-compose.yml` file)  
3) Browse:
   - Gateway API Swagger: `http://localhost:8080/swagger`
   - Shop UI: `http://localhost:5173`
   - Admin UI: `http://localhost:5174`

### Local dev (without Docker)

- Backend: `cd backend && dotnet build` then run services individually (`dotnet run --project Amigurumi.IdentityService`, etc.).  
- Frontend shop: `cd frontend/shop && npm install && npm run dev`.  
- Frontend admin: `cd frontend/admin && npm install && npm run dev`.

## Service endpoints (through gateway)

- `POST /api/auth/register` — Create user (pass `isAdmin: true` to seed an admin)
- `POST /api/auth/login` — Get JWT + refresh
- `POST /api/auth/refresh` — Exchange refresh for new access token
- `GET /api/catalog` — Public catalog
- `GET /api/catalog/{id}` — Product by id
- `POST /api/catalog` — Create (admin)
- `PUT /api/catalog/{id}` — Update (admin)
- `DELETE /api/catalog/{id}` — Delete (admin)
- `POST /api/orders/checkout` — Create order from cart (auth required)
- `GET /api/orders` — Orders for current user (auth required)
- `GET /api/orders/{id}` — Single order (owner or admin)

## How it works

- Identity service keeps users + refresh tokens in memory for the demo and issues JWTs with role claims. Swap the in-memory store with EF Core + Postgres when you connect real persistence.
- Product + Order services validate JWTs (shared secret in `appsettings.json`) and host Swagger for quick testing.
- Gateway exposes a stable API surface to the frontends and fans out to downstream services with `HttpClient`.
- Micro-frontends use Vite + module federation: each exposes its app (shop exposes `./ShopApp`, admin exposes `./AdminApp`). You can compose them in a host shell later if desired.

## Next steps you can take

1) Replace in-memory stores with Postgres via EF Core or Dapper (connection string already provided in compose).  
2) Add persistence migrations + health checks.  
3) Harden JWT secrets and move them to environment variables or a secret store.  
4) Add CI to build and run container smoke tests.  
5) Create a shell host for module federation if you want dynamic runtime composition of the micro-frontends.
