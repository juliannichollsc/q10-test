#!/bin/sh
set -eu

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" -c "create database orderflow_inventory"
psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" -f /orderflow/orders.sql
psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname orderflow_inventory -f /orderflow/inventory.sql
