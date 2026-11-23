create table if not exists catalog_products (
  sku text primary key,
  name text not null
);

create table if not exists orders (
  id uuid primary key,
  customer_name text not null,
  sku text not null references catalog_products(sku),
  quantity integer not null check (quantity between 1 and 100),
  status text not null check (status in ('Pending', 'Confirmed', 'Rejected')),
  created_at timestamptz not null
);

create table if not exists inbox (
  event_id uuid primary key,
  received_at timestamptz not null default now()
);

create table if not exists outbox (
  event_id uuid primary key,
  routing_key text not null,
  payload jsonb not null,
  created_at timestamptz not null default now(),
  published_at timestamptz
);

create index if not exists ix_orders_created_at on orders (created_at desc);
create index if not exists ix_outbox_unpublished on outbox (created_at) where published_at is null;

insert into catalog_products (sku, name) values
  ('ABC-01', 'Wireless Keyboard'),
  ('ABC-02', 'Wireless Mouse'),
  ('ABC-03', 'USB-C Hub')
on conflict (sku) do nothing;
