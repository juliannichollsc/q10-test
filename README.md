# OrderFlow

[English](#english) · [Español](#español)

---

## English

### What I built

An operator creates an order in a web panel. The **Orders API** (.NET 10) saves it as `Pending` and publishes `OrderCreated` to **RabbitMQ**. The **Inventory Worker** (.NET 10) tries to reserve stock and answers `StockReserved` or `StockRejected`. The order ends up `Confirmed` or `Rejected`, and the **React** panel shows the change live through SignalR, falling back to polling. Data lives in **PostgreSQL**, with one database per service.

The decisions I'd defend first:

- **Outbox on both services.** The order and its event are written in the same transaction, so there is never an order without an event, or an event without an order.
- **Inbox for idempotency.** Each consumer stores the event ID under a unique key, in the same transaction as its side effect. If RabbitMQ redelivers a message, the stock is not decremented twice. Delivery is at-least-once; the effect is applied once.
- **Guards in the database.** Stock is decremented with `... where available >= @quantity`. An order only changes status while it is `Pending`, so a late or duplicated result can't overwrite it.
- **No MassTransit or EF Core.** The interesting part of this exercise is how messaging and consistency are handled. Plain RabbitMQ.Client and SQL keep that visible, in a small shared library (`OrderFlow.Messaging`).
- **Proportional scope.** No authentication, CI/CD or elaborate design, as the exercise asks.

**When things fail**

- If Inventory is down, the order stays `Pending` and the event waits in a durable queue. It completes when the worker comes back.
- If RabbitMQ is down, the API still answers `201`: the event is safe in the outbox and is published once the broker returns.
- Invalid messages go to a dead-letter queue.

### How to run and test it

You only need Docker.

```bash
docker compose up --build            # panel: http://localhost:8088
docker compose run --rm --build backend-tests   # 5 test methods, 16 cases
./scripts/demo-idempotency.sh        # re-sends the same event twice; stock doesn't change
docker compose down -v               # wipe the data and start over from the seed
```

The seeded stock is `ABC-01` = 100, `ABC-02` = 75 and `ABC-03` = 5. In the panel:

1. Order 2 × `ABC-01`. It goes from **Pending** to **Confirmed**.
2. Order 6 × `ABC-03`. It goes to **Rejected**, because there are only 5.
3. Submit an empty form, a quantity of 101, or the SKU `XYZ-99`. Each error shows under its field.
4. Run `docker compose stop inventory-worker`, create an order (it stays **Pending**), then `docker compose start inventory-worker`. It confirms without reloading the page.

The API is at `http://localhost:8080` (`/orders`, `/products`, `/openapi/v1.json`). The RabbitMQ management UI is at `http://localhost:15672` (`orderflow` / `orderflow_local_only`).

### Trade-offs I accepted

- **At-least-once, not exactly-once.** A crash right after publishing can send an event again. The inbox makes that harmless, so I chose that over chasing exactly-once delivery.
- **PostgreSQL, one container with two databases.** The outbox and the inbox only work with real transactions, and the data has to survive restarts. Each service owns its own database. Sharing one container keeps the local setup light; in production they would be separate instances.
- **The SKU catalog lives in both databases.** That lets the API reject an unknown SKU right away with a `400`, as the spec asks. The cost is that the seed is duplicated.
- **No timeout for `Pending`.** If an event never reaches Inventory, the order stays `Pending` until it does. I'd rather show that honestly than invent a business rule.
- **Retries are unbounded** for transient errors. That's fine for a demo; in production I'd cap them.

### Kubernetes

`k8s/` has a Deployment and a ConfigMap for each backend service, with resource limits. The API also has a Service and `/healthz` and `/livez` probes. To deploy the whole system I would:

1. Publish the three images with a version tag.
2. Use managed PostgreSQL and RabbitMQ, and run `backend/database/*.sql` once as a Job.
3. Create the `orderflow-runtime` Secret with the connection strings and RabbitMQ credentials, then `kubectl apply -f k8s/`.
4. Expose the frontend behind an Ingress, with `/api` routed to the API and WebSockets enabled.

The worker scales horizontally as it is. The API needs a SignalR backplane (Redis) before it can run more than one replica.

### What I'd do with more time

- A timeout for orders that stay `Pending` too long.
- Bounded retries through quorum queues.
- Cleanup of old outbox and inbox rows.
- OpenTelemetry tracing that follows one order across both services.
- A SignalR backplane, so the API can run more than one replica.
- One end-to-end test of the full HTTP → broker → worker path.

---

## Español

### Qué construí

Un operador crea un pedido desde un panel web. La **Orders API** (.NET 10) lo guarda como `Pending` y publica `OrderCreated` en **RabbitMQ**. El **Inventory Worker** (.NET 10) intenta reservar stock y responde `StockReserved` o `StockRejected`. El pedido termina en `Confirmed` o `Rejected`, y el panel en **React** lo muestra en vivo con SignalR, o con polling si se cae la conexión. Los datos viven en **PostgreSQL**, con una base de datos por servicio.

Las decisiones que defendería primero:

- **Outbox en ambos servicios.** El pedido y su evento se guardan en la misma transacción, así que nunca hay un pedido sin evento ni un evento sin pedido.
- **Inbox para la idempotencia.** Cada consumidor guarda el ID del evento con una clave única, en la misma transacción que su efecto. Si RabbitMQ reentrega un mensaje, el stock no se descuenta dos veces. La entrega es *at-least-once*; el efecto se aplica una sola vez.
- **Reglas en la base de datos.** El stock se descuenta con `... where available >= @quantity`. Un pedido solo cambia de estado si está `Pending`, así que un resultado tardío o duplicado no lo puede sobrescribir.
- **Sin MassTransit ni EF Core.** Lo interesante de la prueba es cómo se manejan la mensajería y la consistencia. Con RabbitMQ.Client y SQL directo eso queda a la vista, en una librería compartida pequeña (`OrderFlow.Messaging`).
- **Alcance proporcional.** Sin autenticación, CI/CD ni diseño elaborado, como pide la prueba.

**Cuando algo falla**

- Si Inventory está caído, el pedido queda en `Pending` y el evento espera en una cola durable. Se completa cuando el worker vuelve.
- Si RabbitMQ está caído, la API igual responde `201`: el evento queda seguro en el outbox y se publica cuando vuelve el broker.
- Los mensajes inválidos van a una cola de *dead-letter*.

### Cómo correrlo y probarlo

Solo necesitas Docker.

```bash
docker compose up --build            # panel: http://localhost:8088
docker compose run --rm --build backend-tests   # 5 métodos de test, 16 casos
./scripts/demo-idempotency.sh        # reenvía dos veces el mismo evento; el stock no cambia
docker compose down -v               # borra los datos y vuelve al seed inicial
```

El stock inicial es `ABC-01` = 100, `ABC-02` = 75 y `ABC-03` = 5. En el panel:

1. Pide 2 × `ABC-01`. Pasa de **Pending** a **Confirmed**.
2. Pide 6 × `ABC-03`. Pasa a **Rejected**, porque solo hay 5.
3. Envía el formulario vacío, una cantidad de 101 o el SKU `XYZ-99`. Cada error aparece debajo de su campo.
4. Ejecuta `docker compose stop inventory-worker`, crea un pedido (se queda en **Pending**) y luego `docker compose start inventory-worker`. Se confirma sin recargar la página.

La API está en `http://localhost:8080` (`/orders`, `/products`, `/openapi/v1.json`). La consola de RabbitMQ está en `http://localhost:15672` (`orderflow` / `orderflow_local_only`).

### Trade-offs que asumí

- **At-least-once, no exactly-once.** Si algo se cae justo después de publicar, un evento puede volver a enviarse. El inbox hace que eso no tenga efecto, y preferí eso a perseguir una entrega exactly-once.
- **PostgreSQL, un contenedor con dos bases de datos.** El outbox y el inbox solo funcionan con transacciones reales, y los datos tienen que sobrevivir a un reinicio. Cada servicio es dueño de su base. Compartir el contenedor mantiene ligero el entorno local; en producción serían instancias separadas.
- **El catálogo de SKUs está en las dos bases.** Así la API rechaza un SKU inexistente al momento con un `400`, como pide el enunciado. El costo es que el seed está duplicado.
- **Sin timeout para `Pending`.** Si un evento nunca llega a Inventory, el pedido se queda en `Pending` hasta que llegue. Prefiero mostrarlo con honestidad que inventar una regla de negocio.
- **Reintentos sin límite** ante errores transitorios. Para una demo está bien; en producción les pondría un tope.

### Kubernetes

En `k8s/` hay un Deployment y un ConfigMap para cada servicio de backend, con límites de recursos. La API además tiene un Service y probes en `/healthz` y `/livez`. Para desplegar el sistema completo:

1. Publicaría las tres imágenes con un tag de versión.
2. Usaría PostgreSQL y RabbitMQ gestionados, y correría `backend/database/*.sql` una vez como Job.
3. Crearía el Secret `orderflow-runtime` con las cadenas de conexión y las credenciales de RabbitMQ, y luego `kubectl apply -f k8s/`.
4. Expondría el frontend detrás de un Ingress, con `/api` hacia la API y WebSockets habilitados.

El worker escala horizontalmente tal como está. La API necesita un backplane de SignalR (Redis) antes de poder correr más de una réplica.

### Qué haría con más tiempo

- Un timeout para pedidos que se queden demasiado tiempo en `Pending`.
- Reintentos con límite mediante quorum queues.
- Limpieza de las filas antiguas de outbox e inbox.
- Trazas con OpenTelemetry que sigan un pedido a través de los dos servicios.
- Un backplane para SignalR, para poder correr más de una réplica de la API.
- Un test end-to-end del flujo completo HTTP → broker → worker.
