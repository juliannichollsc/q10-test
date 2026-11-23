import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { OrderForm } from "./OrderForm";

const products = [
  { sku: "ABC-01", name: "Wireless Keyboard" },
  { sku: "ABC-02", name: "Wireless Mouse" },
  { sku: "ABC-03", name: "USB-C Hub" },
];

function stubApi(createOrder?: () => Response) {
  const fetchMock = vi.fn((_url: string, init?: RequestInit) =>
    Promise.resolve(init?.method === "POST" && createOrder ? createOrder() : Response.json(products)),
  );
  vi.stubGlobal("fetch", fetchMock);
  return fetchMock;
}

function postCalls(fetchMock: ReturnType<typeof stubApi>) {
  return fetchMock.mock.calls.filter(([, init]) => init?.method === "POST");
}

function renderForm() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <OrderForm />
    </QueryClientProvider>,
  );
}

async function fillAndSubmit(customerName: string, sku: string, quantity: string) {
  const user = userEvent.setup();
  if (customerName) await user.type(screen.getByLabelText("Customer name"), customerName);
  if (sku) await user.type(screen.getByLabelText("Product SKU"), sku);
  await user.clear(screen.getByLabelText("Quantity"));
  if (quantity) await user.type(screen.getByLabelText("Quantity"), quantity);
  await user.click(screen.getByRole("button", { name: "Create order" }));
}

describe("OrderForm", () => {
  it("shows the catalog products and fills the SKU when one is picked", async () => {
    stubApi();
    const { container } = renderForm();

    const products = await screen.findByRole("group", { name: "Available products" });
    await waitFor(() => expect(container.querySelectorAll("#product-options option")).toHaveLength(3));
    await userEvent.setup().click(within(products).getByRole("button", { name: /ABC-03/ }));

    expect(screen.getByLabelText("Product SKU")).toHaveValue("ABC-03");
    expect(within(products).getByRole("button", { name: /ABC-03/ })).toHaveAttribute("aria-pressed", "true");
  });

  it("shows client-side validation errors next to each field without creating an order", async () => {
    const fetchMock = stubApi();
    renderForm();

    await fillAndSubmit("", "", "101");

    expect(await screen.findByText("Customer name is required.")).toBeInTheDocument();
    expect(screen.getByText("SKU is required.")).toBeInTheDocument();
    expect(screen.getByLabelText("Quantity")).toHaveAccessibleDescription("Quantity cannot exceed 100.");
    expect(postCalls(fetchMock)).toHaveLength(0);
  });

  it("maps server validation errors onto the matching field", async () => {
    stubApi(() => Response.json(
      {
        title: "One or more validation errors occurred.",
        errors: { sku: ["SKU 'XYZ-99' does not exist in the product catalog."] },
      },
      { status: 400 },
    ));
    renderForm();

    await fillAndSubmit("Ada Lovelace", "XYZ-99", "2");

    expect(await screen.findByText("SKU 'XYZ-99' does not exist in the product catalog.")).toBeInTheDocument();
    expect(screen.getByLabelText("Product SKU")).toHaveAttribute("aria-invalid", "true");
    expect(screen.getByRole("alert")).toHaveTextContent("One or more validation errors occurred.");
  });

  it("confirms a created order and resets the form", async () => {
    stubApi(() => Response.json(
      {
        id: "3f2b8c1e-0000-0000-0000-000000000000",
        customerName: "Ada Lovelace",
        sku: "ABC-01",
        quantity: 2,
        status: "Pending",
        createdAt: new Date().toISOString(),
      },
      { status: 201 },
    ));
    renderForm();

    await fillAndSubmit("Ada Lovelace", "ABC-01", "2");

    expect(await screen.findByRole("status")).toHaveTextContent("Order 3f2b8c1e created.");
    expect(screen.getByLabelText("Customer name")).toHaveValue("");
  });
});
