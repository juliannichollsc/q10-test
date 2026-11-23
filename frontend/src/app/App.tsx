import { OrderForm } from "../features/orders/OrderForm";
import { OrderList } from "../features/orders/OrderList";

export function App() {
  return (
    <div className="page-shell">
      <header className="topbar">
        <span className="brand">
          <span className="brand-mark" aria-hidden="true">O</span>
          OrderFlow
        </span>
        <span className="environment-tag">Operations</span>
      </header>
      <main>
        <section className="intro">
          <p className="eyebrow">Order management</p>
          <h1>Keep every order moving.</h1>
          <p className="intro-copy">Register orders and follow inventory decisions as they happen.</p>
        </section>
        <div className="content-grid">
          <OrderForm />
          <OrderList />
        </div>
      </main>
    </div>
  );
}
