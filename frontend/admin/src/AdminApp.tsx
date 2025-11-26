import { FormEvent, useEffect, useState } from "react";
import { createProduct, deleteProduct, listProducts, login, logout, register, updateProduct } from "./api";
import { ProductDto } from "./types";

const blank: Omit<ProductDto, "id"> = { name: "", description: "", price: 0, stock: 0, tags: [] };

export default function AdminApp() {
  const [products, setProducts] = useState<ProductDto[]>([]);
  const [draft, setDraft] = useState<Omit<ProductDto, "id">>(blank);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [creds, setCreds] = useState({ email: "admin@example.com", password: "Admin123!", displayName: "Admin" });

  const refresh = () => listProducts().then(setProducts).catch((err) => setMessage(err.message));

  useEffect(() => {
    refresh();
  }, []);

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    try {
      if (editingId) {
        await updateProduct(editingId, draft);
        setMessage("Updated product");
      } else {
        await createProduct(draft);
        setMessage("Created product");
      }
      setDraft(blank);
      setEditingId(null);
      await refresh();
    } catch (err) {
      setMessage(err instanceof Error ? err.message : String(err));
    }
  };

  const startEdit = (product: ProductDto) => {
    setEditingId(product.id);
    setDraft({ ...product, tags: product.tags });
  };

  const handleAuth = async (mode: "login" | "register") => {
    setMessage(null);
    try {
      if (mode === "login") {
        await login({ email: creds.email, password: creds.password });
      } else {
        await register({ email: creds.email, password: creds.password, displayName: creds.displayName, isAdmin: true });
      }
      setMessage("Authenticated as admin");
    } catch (err) {
      setMessage(err instanceof Error ? err.message : String(err));
    }
  };

  return (
    <div className="container py-4">
      <header className="d-flex justify-content-between align-items-center mb-3">
        <div>
          <div className="fw-bold fs-4">Amigurumi Admin</div>
          <div className="text-muted">Manage catalog and monitor orders</div>
        </div>
        <div className="d-flex gap-2">
          <button className="btn btn-outline-primary" onClick={() => handleAuth("login")}>Login</button>
          <button className="btn btn-outline-secondary" onClick={() => handleAuth("register")}>Seed Admin</button>
          <button className="btn btn-outline-danger" onClick={() => logout()}>Logout</button>
        </div>
      </header>

      {message && <div className="alert alert-info">{message}</div>}

      <div className="row g-4">
        <div className="col-md-4">
          <div className="card shadow-sm">
            <div className="card-header">{editingId ? "Edit product" : "New product"}</div>
            <div className="card-body">
              <form className="d-grid gap-2" onSubmit={handleSubmit}>
                <input
                  className="form-control"
                  placeholder="Name"
                  value={draft.name}
                  onChange={(e) => setDraft({ ...draft, name: e.target.value })}
                  required
                />
                <textarea
                  className="form-control"
                  placeholder="Description"
                  value={draft.description}
                  onChange={(e) => setDraft({ ...draft, description: e.target.value })}
                  required
                />
                <input
                  type="number"
                  className="form-control"
                  placeholder="Price"
                  value={draft.price}
                  onChange={(e) => setDraft({ ...draft, price: parseFloat(e.target.value) })}
                  required
                />
                <input
                  type="number"
                  className="form-control"
                  placeholder="Stock"
                  value={draft.stock}
                  onChange={(e) => setDraft({ ...draft, stock: parseInt(e.target.value) || 0 })}
                  required
                />
                <input
                  className="form-control"
                  placeholder="Tags comma separated"
                  value={draft.tags.join(",")}
                  onChange={(e) => setDraft({ ...draft, tags: e.target.value.split(",").map((t) => t.trim()).filter(Boolean) })}
                />
                <button className="btn btn-primary" type="submit">{editingId ? "Save" : "Create"}</button>
                {editingId && (
                  <button className="btn btn-outline-secondary" type="button" onClick={() => { setEditingId(null); setDraft(blank); }}>
                    Cancel
                  </button>
                )}
              </form>
            </div>
          </div>
        </div>
        <div className="col-md-8">
          <div className="card shadow-sm">
            <div className="card-header d-flex justify-content-between align-items-center">
              <span>Catalog</span>
              <button className="btn btn-sm btn-outline-secondary" onClick={refresh}>Refresh</button>
            </div>
            <div className="table-responsive">
              <table className="table table-hover align-middle mb-0">
                <thead className="table-light">
                  <tr>
                    <th>Name</th>
                    <th>Price</th>
                    <th>Stock</th>
                    <th>Tags</th>
                    <th></th>
                  </tr>
                </thead>
                <tbody>
                  {products.map((p) => (
                    <tr key={p.id}>
                      <td>
                        <div className="fw-semibold">{p.name}</div>
                        <div className="small text-muted">{p.description}</div>
                      </td>
                      <td>${p.price.toFixed(2)}</td>
                      <td>{p.stock}</td>
                      <td>{p.tags.join(", ")}</td>
                      <td className="text-end">
                        <div className="btn-group btn-group-sm">
                          <button className="btn btn-outline-primary" onClick={() => startEdit(p)}>Edit</button>
                          <button
                            className="btn btn-outline-danger"
                            onClick={async () => {
                              await deleteProduct(p.id);
                              setMessage("Deleted product");
                              refresh();
                            }}
                          >
                            Delete
                          </button>
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
