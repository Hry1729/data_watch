# DuckDB Virtual Server (Python)

Perspective provides a built-in virtual server for DuckDB, allowing `<perspective-viewer>` clients to query a server-side DuckDB database over WebSocket.

For browser-only usage via DuckDB-WASM, see the [JavaScript DuckDB guide](../JavaScript/DuckDB.md).

---

## Installation

```bash
pip install perspective-python duckdb
```

---

## Usage

Create a server that exposes a DuckDB database to browser clients:

```python
import duckdb
import tornado.web
import tornado.ioloop
from perspective import DuckDBVirtualServer
from perspective.handlers.tornado import PerspectiveTornadoHandler

# Create DuckDB connection and load data
conn = duckdb.connect()
conn.execute("CREATE TABLE my_table AS SELECT * FROM 'data.parquet'")

# Create virtual server backed by DuckDB
server = DuckDBVirtualServer(conn)

# Serve over WebSocket
app = tornado.web.Application([
  (r"/websocket", PerspectiveTornadoHandler, {"perspective_server": server}),
])

app.listen(8080)
tornado.ioloop.IOLoop.current().start()
```

---

## Browser Client Connection

Connect from the browser:

```javascript
const websocket = await perspective.websocket("ws://localhost:8080/websocket");
const table = await websocket.open_table("my_table");
document.getElementById("viewer").load(table);
```

---

## Examples

- [Python DuckDB example](https://github.com/finos/perspective/tree/master/examples/python-duckdb)