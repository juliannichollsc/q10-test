import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useForm } from "react-hook-form";
import { ApiError } from "../../shared/api/http-client";
import { TextField } from "../../shared/ui/TextField";
import { ordersApi, ordersQueryKey, productsApi, productsQueryKey } from "./api";
import { createOrderSchema, type CreateOrderInput } from "./order-schema";

const formFields = ["customerName", "sku", "quantity"] as const;

function isFormField(name: string): name is (typeof formFields)[number] {
  return (formFields as readonly string[]).includes(name);
}

export function OrderForm() {
  const queryClient = useQueryClient();
  const products = useQuery({ queryKey: productsQueryKey, queryFn: productsApi.list, staleTime: Infinity });
  const form = useForm<CreateOrderInput>({
    resolver: zodResolver(createOrderSchema),
    defaultValues: { customerName: "", sku: "", quantity: 1 },
  });
  const { errors } = form.formState;
  const selectedSku = form.watch("sku");

  const createOrder = useMutation({
    mutationFn: ordersApi.create,
    onSuccess: () => {
      form.reset();
      return queryClient.invalidateQueries({ queryKey: ordersQueryKey });
    },
    onError: (error) => {
      if (!(error instanceof ApiError)) return;
      for (const [field, messages] of Object.entries(error.fieldErrors)) {
        if (isFormField(field)) form.setError(field, { type: "server", message: messages[0] });
      }
    },
  });

  const submit = form.handleSubmit((input) => createOrder.mutate(input));

  return (
    <section className="panel" aria-labelledby="new-order-title">
      <div className="panel-heading">
        <div>
          <p className="eyebrow">Order entry</p>
          <h2 id="new-order-title">Create an order</h2>
        </div>
      </div>
      <form className="order-form" onSubmit={submit} noValidate>
        <TextField
          id="customerName"
          label="Customer name"
          autoComplete="name"
          error={errors.customerName?.message}
          {...form.register("customerName")}
        />
        <TextField
          id="sku"
          label="Product SKU"
          autoComplete="off"
          placeholder="Type or pick a product"
          list="product-options"
          error={errors.sku?.message}
          {...form.register("sku")}
        />
        <datalist id="product-options">
          {products.data?.map((product) => (
            <option key={product.sku} value={product.sku}>
              {product.name}
            </option>
          ))}
        </datalist>
        {products.data && products.data.length > 0 && (
          <div className="sku-options" role="group" aria-label="Available products">
            {products.data.map((product) => (
              <button
                key={product.sku}
                type="button"
                className="sku-option"
                aria-pressed={selectedSku === product.sku}
                onClick={() => form.setValue("sku", product.sku, { shouldValidate: true })}
              >
                <strong>{product.sku}</strong> {product.name}
              </button>
            ))}
          </div>
        )}
        <TextField
          id="quantity"
          label="Quantity"
          type="number"
          min={1}
          max={100}
          error={errors.quantity?.message}
          {...form.register("quantity", { valueAsNumber: true })}
        />
        {createOrder.isError && (
          <div className="alert alert-error" role="alert">
            {createOrder.error.message}
          </div>
        )}
        {createOrder.isSuccess && (
          <div className="alert alert-success" role="status">
            Order {createOrder.data.id.slice(0, 8)} created. Inventory is being checked.
          </div>
        )}
        <button className="primary-button" type="submit" disabled={createOrder.isPending}>
          {createOrder.isPending ? "Creating order…" : "Create order"}
        </button>
      </form>
    </section>
  );
}
