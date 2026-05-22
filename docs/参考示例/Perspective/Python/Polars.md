# Polars Virtual Server (Python)

Perspective provides a built-in virtual server for Polars, allowing `<perspective-viewer>` clients to query in-memory Polars DataFrames over WebSocket.

---

## Installation

```bash
pip install perspective-python polars
```

---

## Usage

Create a server that exposes Polars DataFrames to browser clients:

```python
import polars as pl
import tornado.web
import tornado.ioloop
from perspective.virtual_servers.polars import PolarsVirtualServer
from perspective.handlers.tornado import PerspectiveTornadoHandler

# Load data into Polars DataFrames
df = pl.read_parquet("data.parquet")

# Create virtual server backed by Polars (dict of name -> DataFrame)
server = PolarsVirtualServer({"my_table": df})

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

- [Python Polars example](https://github.com/finos/perspective/tree/master/examples/python-polars)