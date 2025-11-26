import { AuthResponse, CheckoutRequest, LoginRequest, OrderDto, ProductDto, RegisterRequest } from "./types";

const API_BASE = import.meta.env.VITE_API_BASE ?? "http://localhost:8080/api";

async function request<T>(path: string, options: RequestInit = {}): Promise<T> {
  const res = await fetch(`${API_BASE}${path}`, {
    ...options,
    headers: {
      "Content-Type": "application/json",
      ...(options.headers || {})
    }
  });

  if (!res.ok) {
    throw new Error(`Request failed: ${res.status} ${res.statusText}`);
  }

  if (res.status === 204) {
    return undefined as T;
  }

  return (await res.json()) as T;
}

export function setAuth(token: string | null): void {
  if (!token) {
    localStorage.removeItem("access_token");
    return;
  }
  localStorage.setItem("access_token", token);
}

export function authHeader(): string | null {
  return localStorage.getItem("access_token");
}

export async function login(body: LoginRequest): Promise<AuthResponse> {
  const result = await request<AuthResponse>("/auth/login", { method: "POST", body: JSON.stringify(body) });
  setAuth(result.accessToken);
  return result;
}

export async function register(body: RegisterRequest): Promise<AuthResponse> {
  const result = await request<AuthResponse>("/auth/register", { method: "POST", body: JSON.stringify(body) });
  setAuth(result.accessToken);
  return result;
}

export function logout(): void {
  setAuth(null);
}

export async function fetchCatalog(): Promise<ProductDto[]> {
  return request<ProductDto[]>("/catalog");
}

export async function checkout(payload: CheckoutRequest): Promise<OrderDto> {
  return request<OrderDto>("/orders/checkout", {
    method: "POST",
    body: JSON.stringify(payload),
    headers: authHeader() ? { Authorization: `Bearer ${authHeader()}` } : {}
  });
}

export async function getOrders(): Promise<OrderDto[]> {
  return request<OrderDto[]>("/orders", {
    headers: authHeader() ? { Authorization: `Bearer ${authHeader()}` } : {}
  });
}
