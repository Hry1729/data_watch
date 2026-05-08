# Customizing perspective.worker()

`perspective.worker()` creates a `Client` that connects to a Perspective data engine. By default it spins up a dedicated Worker running the built-in WebAssembly engine, but you can pass an argument to change this behavior.

## What You Can Pass

- A `Worker`, `SharedWorker`, or `ServiceWorker` — runs the built-in engine in a different worker context
- A `MessagePort` from `createMessageHandler()` — connects to a Virtual Server instead of the built-in engine

---

## Built-in Engine with a Custom Worker

Pass a `Worker`, `SharedWorker`, or `ServiceWorker` that loads the worker script distributed at `"@perspective-dev/client/dist/cdn/perspective-server.worker.js"`.

> **Note:** `SharedWorker` and `ServiceWorker` have more complicated behavior compared to a dedicated `Worker`, and will need special consideration to integrate (or debug).

### Dedicated Worker

```javascript
const worker = await perspective.worker(new Worker(url));
```

### SharedWorker

```javascript
const worker = await perspective.worker(new SharedWorker(url));
```

### ServiceWorker

```javascript
const registration = await navigator.serviceWorker.register(url, {
  scope: "", // Your scope here
});

const worker = await perspective.worker(registration.active);
```

---

## Virtual Server

Instead of the built-in WebAssembly engine, `perspective.worker()` can connect to a Virtual Server — an adapter that translates Perspective queries into operations on an external data source such as DuckDB or ClickHouse.

### Usage

Use `perspective.createMessageHandler()` with a `VirtualServerHandler` to create a `MessagePort`, then pass it to `worker()`:

```javascript
import perspective from "@perspective-dev/client";

const handler = {
  /* VirtualServerHandler implementation */
};

const server = perspective.createMessageHandler(handler);
const client = await perspective.worker(server);
const table = await client.open_table("my_table");
```

### How It Works

The returned `Client` works identically to one backed by the built-in engine — you can:

- Pass it to `<perspective-viewer>.load()`
- Call `open_table()`
- Use all standard Client methods

The difference is that queries are fulfilled by your handler rather than the WASM engine.

For the full `VirtualServerHandler` interface and a worked example, see [Implementing a Custom Virtual Server](./Custom-Virtual-Server.md).