# Loading Data from a Table

Data can be loaded into `<perspective-viewer>` in the form of a `Table()` or a `Promise<Table>` via the `load()` method.

## Basic Loading

```javascript
// Create a new worker, then a new table promise on that worker.
const worker = await perspective.worker();
const table = await worker.table(data);

// Bind a viewer element to this table.
await viewer.load(table);
```

---

## Sharing a Table Between Multiple Viewers

Multiple `<perspective-viewer>`s can share a `table()` by passing the `table()` into the `load()` method of each viewer.

### Behavior

- Each `perspective-viewer` will update when the underlying `table()` is updated
- `table.delete()` will fail until all `perspective-viewer` instances referencing it are also deleted

```javascript
const viewer1 = document.getElementById("viewer1");
const viewer2 = document.getElementById("viewer2");

// Create a new WebWorker
const worker = await perspective.worker();
// Create a table in this worker
const table = await worker.table(data);

// Load the same table in 2 different <perspective-viewer> elements
await viewer1.load(table);
await viewer2.load(table);

// Both `viewer1` and `viewer2` will reflect this update
await table.update([{ x: 5, y: "e", z: true }]);
```

---

## Loading from a Virtual Table

Loading a virtual (server-only) Table works just like loading a local/Web Worker Table — just pass the virtual Table to `viewer.load()`.

### In the Browser

```javascript
const elem = document.getElementsByTagName("perspective-viewer")[0];

// Bind to the server's worker instead of instantiating a Web Worker.
const websocket = await perspective.websocket(
  window.location.origin.replace("http", "ws")
);

// Bind the viewer to the preloaded data source. `table` and `view` objects
// live on the server.
const server_table = await websocket.open_table("table_one");
await elem.load(server_table);
```

---

## Client/Server Replicated Mode

Alternatively, data can be cloned from a server-side virtual Table into a client-side WebAssembly Table.

The browser clone will be synced via delta updates transferred via Apache Arrow IPC format, but local `View`s created will be calculated locally on the client browser.

```javascript
const worker = await perspective.worker();
const server_view = await server_table.view();
const client_table = worker.table(server_view);

await elem.load(client_table);
```

`<perspective-viewer>` instances bound in this way are otherwise no different than `<perspective-viewer>`s which rely on a Web Worker, and can even share a host application with Web Worker-bound `table()`s.

The same promise-based API is used to communicate with the server-instantiated `view()`, only in this case it is over a websocket.