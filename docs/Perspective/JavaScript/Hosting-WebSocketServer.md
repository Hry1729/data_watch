# Hosting a WebSocket Server in Node.js

For exceptionally large datasets, a `Client` can be bound to a `perspective.table()` instance running in Node.js/Python/Rust remotely, rather than creating one in a Web Worker and downloading the entire data set.

## Trade-offs

This trades off:
- Network bandwidth and server resource requirements
- For a smaller browser memory and CPU footprint

---

## Node.js Server Example

```javascript
const { WebSocketServer, table } = require("@perspective-dev/client");
const fs = require("fs");

// Start a WS/HTTP host on port 8080. The `assets` property allows
// the `WebSocketServer()` to also serves the file structure rooted in this
// module's directory.
const host = new WebSocketServer({ assets: [__dirname], port: 8080 });

// Read an arrow file from the file system and host it as a named table.
const arr = fs.readFileSync(__dirname + "/superstore.lz4.arrow");
await table(arr, { name: "table_one" });
```

---

## Browser Client Implementation

```javascript
const elem = document.getElementsByTagName("perspective-viewer")[0];

// Bind to the server's worker instead of instantiating a Web Worker.
const websocket = await perspective.websocket(
  window.location.origin.replace("http", "ws"),
);

// Create a virtual `Table` to the preloaded data source. `table` and `view`
// objects live on the server.
const server_table = await websocket.open_table("table_one");
```

---

## Architecture

This is a **Server-only** architecture where:

- Tables and Views exist on the server
- The browser client only renders results
- All queries are executed on the server
- Only the data needed for the current view is transferred