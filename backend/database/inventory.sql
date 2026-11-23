create table if not exists stock (
  sku text primary key,
  available integer not null check (available >= 0)
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

create index if not exists ix_outbox_unpublished on outbox (created_at) where published_at is null;

insert into stock (sku, available) values
  ('ABC-01', 100),
  ('ABC-02', 75),
  ('ABC-03', 5)
on conflict (sku) do nothing;
