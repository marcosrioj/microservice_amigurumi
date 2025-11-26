import { useEffect, useMemo, useState } from "react";
import { checkout, fetchCatalog, getOrders, login, logout, register } from "./api";
import { CheckoutRequest, ProductDto, OrderDto } from "./types";

function useAsync<T>(fn: () => Promise<T>, deps: unknown[] = []) {
  const [data, setData] = useState<T | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    setLoading(true);
    fn()
      .then(setData)
      .catch((err) => setError(err instanceof Error ? err.message : String(err)))
      .finally(() => setLoading(false));
  }, deps);

  return { data, error, loading } as const;
}

function ProductCard({ product, onAdd }: { product: ProductDto; onAdd: (p: ProductDto) => void }) {
  return (
    <div className="card h-100 shadow-sm">
      <div className="card-body d-flex flex-column">
        <h5 className="card-title">{product.name}</h5>
        <p className="card-text text-muted small">{product.description}</p>
        <div className="mb-2">
          {product.tags.map((tag) => (
            <span key={tag} className="badge text-bg-secondary me-1">
              {tag}
            </span>
          ))}
        </div>
        <div className="d-flex justify-content-between align-items-center mt-auto">
          <strong>${product.price.toFixed(2)}</strong>
          <button className="btn btn-primary btn-sm" onClick={() => onAdd(product)}>
            Add to cart
          </button>
        </div>
      </div>
    </div>
  );
}

function Cart({
  items,
  onCheckout
}: {
  items: Map<string, { product: ProductDto; quantity: number }>;
  onCheckout: (payload: CheckoutRequest) => Promise<void>;
}) {
  const total = useMemo(
    () => Array.from(items.values()).reduce((acc, item) => acc + item.product.price * item.quantity, 0),
    [items]
  );

  const [loading, setLoading] = useState(false);

  const handleCheckout = async () => {
    setLoading(true);
    try {
      const payload: CheckoutRequest = {
        items: Array.from(items.values()).map((i) => ({
          productId: i.product.id,
          quantity: i.quantity,
          unitPrice: i.product.price
        })),
        paymentMethod: "card",
        shippingAddress: "123 Demo Street"
      };
      await onCheckout(payload);
    } finally {
      setLoading(false);
    }
  };

  if (!items.size) {
    return <p className="text-muted">Cart is empty.</p>;
  }

  return (
    <div className="border rounded p-3 bg-white shadow-sm">
      <h5 className="mb-3">Cart</h5>
      {Array.from(items.values()).map(({ product, quantity }) => (
        <div key={product.id} className="d-flex justify-content-between align-items-center border-bottom py-2">
          <div>
            <strong>{product.name}</strong>
            <div className="small text-muted">${product.price.toFixed(2)} x {quantity}</div>
          </div>
          <span className="fw-bold">${(product.price * quantity).toFixed(2)}</span>
        </div>
      ))}
      <div className="d-flex justify-content-between mt-3">
        <span className="fw-semibold">Total</span>
        <span className="fw-bold">${total.toFixed(2)}</span>
      </div>
      <button className="btn btn-success w-100 mt-3" onClick={handleCheckout} disabled={loading}>
        {loading ? "Processing..." : "Checkout"}
      </button>
    </div>
  );
}

function AuthPanel({ onLoggedIn }: { onLoggedIn: () => void }) {
  const [mode, setMode] = useState<"login" | "register">("login");
  const [email, setEmail] = useState("demo@example.com");
  const [password, setPassword] = useState("P@ssword123");
  const [displayName, setDisplayName] = useState("Demo User");
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    try {
      if (mode === "login") {
        await login({ email, password });
      } else {
        await register({ email, password, displayName });
      }
      onLoggedIn();
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    }
  };

  return (
    <div className="border rounded p-3 bg-white shadow-sm">
      <div className="d-flex justify-content-between align-items-center mb-2">
        <h5 className="mb-0">{mode === "login" ? "Login" : "Register"}</h5>
        <button className="btn btn-link btn-sm" onClick={() => setMode(mode === "login" ? "register" : "login")}>
          Switch to {mode === "login" ? "register" : "login"}
        </button>
      </div>
      <form onSubmit={handleSubmit} className="d-grid gap-2">
        <input className="form-control" placeholder="Email" value={email} onChange={(e) => setEmail(e.target.value)} />
        {mode === "register" && (
          <input
            className="form-control"
            placeholder="Display name"
            value={displayName}
            onChange={(e) => setDisplayName(e.target.value)}
          />
        )}
        <input
          className="form-control"
          placeholder="Password"
          type="password"
          value={password}
          onChange={(e) => setPassword(e.target.value)}
        />
        <button className="btn btn-primary" type="submit">
          {mode === "login" ? "Login" : "Register"}
        </button>
        {error && <div className="text-danger small">{error}</div>}
      </form>
    </div>
  );
}

function Orders({ orders }: { orders: OrderDto[] }) {
  if (!orders.length) {
    return <p className="text-muted">No orders yet.</p>;
  }
  return (
    <div className="bg-white border rounded p-3 shadow-sm">
      <h5>Recent orders</h5>
      {orders.map((order) => (
        <div key={order.id} className="py-2 border-bottom">
          <div className="d-flex justify-content-between">
            <span className="fw-semibold">{new Date(order.createdAtUtc).toLocaleString()}</span>
            <span className="badge text-bg-primary">{order.status}</span>
          </div>
          <div className="small text-muted">
            {order.items.length} items • Total ${order.total.toFixed(2)}
          </div>
        </div>
      ))}
    </div>
  );
}

export default function ShopApp() {
  const { data: products, loading, error } = useAsync(fetchCatalog, []);
  const [cart, setCart] = useState<Map<string, { product: ProductDto; quantity: number }>>(new Map());
  const [orders, setOrders] = useState<OrderDto[]>([]);

  const addToCart = (product: ProductDto) => {
    setCart((prev) => {
      const next = new Map(prev);
      const existing = next.get(product.id);
      next.set(product.id, { product, quantity: existing ? existing.quantity + 1 : 1 });
      return next;
    });
  };

  const handleCheckout = async (payload: CheckoutRequest) => {
    await checkout(payload);
    setCart(new Map());
    const fresh = await getOrders();
    setOrders(fresh);
  };

  useEffect(() => {
    getOrders().then(setOrders).catch(() => {});
  }, []);

  return (
    <div className="container py-4">
      <header className="d-flex justify-content-between align-items-center mb-4">
        <div>
          <div className="fw-bold fs-4">Amigurumi Store</div>
          <div className="text-muted">Micro-frontend shopper app</div>
        </div>
        <button className="btn btn-outline-secondary" onClick={() => logout()}>
          Logout
        </button>
      </header>

      <div className="row g-4">
        <div className="col-md-8">
          {loading && <p>Loading catalog...</p>}
          {error && <p className="text-danger">{error}</p>}
          <div className="row row-cols-1 row-cols-md-2 g-3">
            {products?.map((product) => (
              <div className="col" key={product.id}>
                <ProductCard product={product} onAdd={addToCart} />
              </div>
            ))}
          </div>
        </div>
        <div className="col-md-4 d-flex flex-column gap-3">
          <Cart items={cart} onCheckout={handleCheckout} />
          <AuthPanel onLoggedIn={() => getOrders().then(setOrders)} />
          <Orders orders={orders} />
        </div>
      </div>
    </div>
  );
}
