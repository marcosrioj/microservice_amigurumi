import { AuthResponse, LoginRequest, ProductDto, RegisterRequest } from "./types";

const API_BASE = import.meta.env.VITE_API_BASE ?? "http://localhost:8080/api";

const auth = {
  set(token: string | null) {
    if (!token) {
      localStorage.removeItem("access_token");
    } else {
      localStorage.setItem("access_token", token);
    }
  },
  get(): string | null {
    return localStorage.getItem("access_token");
  }
};

async function request<T>(path: string, options: RequestInit = {}): Promise<T> {
  const authHeader = auth.get();
  const res = await fetch(`${API_BASE}${path}`, {
    ...options,
    headers: {
      "Content-Type": "application/json",
      ...(authHeader ? { Authorization: `Bearer ${authHeader}` } : {}),
      ...(options.headers || {})
    }
  });

  if (!res.ok) {
    throw new Error(`Request failed: ${res.status}`);
  }

  if (res.status === 204) {
    return undefined as T;
  }

  return (await res.json()) as T;
}

export async function login(body: LoginRequest): Promise<AuthResponse> {
  const res = await request<AuthResponse>("/auth/login", { method: "POST", body: JSON.stringify(body) });
  auth.set(res.accessToken);
  return res;
}

export async function register(body: RegisterRequest): Promise<AuthResponse> {
  const res = await request<AuthResponse>("/auth/register", { method: "POST", body: JSON.stringify(body) });
  auth.set(res.accessToken);
  return res;
}

export function logout(): void {
  auth.set(null);
}

export async function listProducts(): Promise<ProductDto[]> {
  return request<ProductDto[]>("/catalog");
}

export async function createProduct(payload: Omit<ProductDto, "id">): Promise<ProductDto> {
  return request<ProductDto>("/catalog", { method: "POST", body: JSON.stringify(payload) });
}

export async function updateProduct(id: string, payload: Omit<ProductDto, "id">): Promise<ProductDto> {
  return request<ProductDto>(`/catalog/${id}`, { method: "PUT", body: JSON.stringify(payload) });
}

export async function deleteProduct(id: string): Promise<void> {
  await request<void>(`/catalog/${id}`, { method: "DELETE" });
}
