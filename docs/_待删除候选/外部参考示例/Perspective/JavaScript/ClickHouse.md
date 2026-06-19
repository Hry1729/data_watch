# ClickHouse Virtual Server (JavaScript)

Perspective provides a built-in virtual server for ClickHouse, allowing `<perspective-viewer>` to query ClickHouse tables directly from the browser.

For server-side Python usage, see the [Python ClickHouse guide](../Python/ClickHouse.md).

---

## Installation

```bash
npm install @perspective-dev/client @perspective-dev/viewer @clickhouse/client-web
```

---

## Usage

Connect to a ClickHouse instance and bind it to a Perspective viewer:

```javascript
import perspective from "@perspective-dev/client";
import "@perspective-dev/viewer";
import { createClient } from "@clickhouse/client-web";

// Connect to ClickHouse
const clickhouseClient = createClient({
  url: "http://localhost:8123",
  database: "default",
});

// Create a Perspective virtual server backed by ClickHouse
const handler = perspective.ClickhouseHandler(clickhouseClient);
const messageHandler = perspective.createMessageHandler(handler);

// Connect a viewer
const client = await perspective.worker(messageHandler);
const table = await client.open_table("my_table");
document.getElementById("viewer").load(table);
```

---

## How It Works

- Perspective translates its query operations into ClickHouse SQL queries
- Queries are executed directly on the ClickHouse server
- Only the data needed for the current view is transferred over HTTP
- Leverages ClickHouse's native query optimizations for high performance

---

## Examples

- [Browser ClickHouse example](https://github.com/finos/perspective/tree/master/examples/browser-clickhouse)