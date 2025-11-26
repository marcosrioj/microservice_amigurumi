export type ProductDto = {
  id: string;
  name: string;
  description: string;
  price: number;
  stock: number;
  tags: string[];
};

export type AuthResponse = {
  accessToken: string;
  refreshToken: string;
  expiresAtUtc: string;
};

export type LoginRequest = {
  email: string;
  password: string;
};

export type RegisterRequest = LoginRequest & { displayName: string; isAdmin?: boolean };

export type CheckoutRequest = {
  items: { productId: string; quantity: number; unitPrice: number }[];
  paymentMethod: string;
  shippingAddress: string;
};

export type OrderDto = {
  id: string;
  userId: string;
  items: CheckoutRequest["items"];
  total: number;
  status: string;
  createdAtUtc: string;
};
