# A Tornado Server in Python

Perspective ships with a pre-built Tornado handler that makes integration with `tornado.websockets` extremely easy. This allows you to run an instance of Perspective on a server using Python, open a websocket to a `Table`, and access the `Table` in JavaScript and through `<perspective-viewer>`. All instructions sent to the `Table` are processed in Python, which executes the commands, and returns its output through the websocket back to JavaScript.

---

## Python Setup

### Install Dependencies

Make sure Perspective and Tornado are installed:

```bash
pip install perspective-python tornado
```

### Create the Server

To use the handler, we need to first have a `Server`, a `Client` and an instance of a `Table`:

```python
import perspective

SERVER = perspective.Server()
CLIENT = SERVER.new_local_client()
TABLE = client.table(data, name="data_source_one")
```

### Host the Table

Once the server has been created, create a `Table` instance with a name. The name that you host the table under is important — it acts as a unique accessor on the JavaScript side, which will look for a `Table` hosted at the websocket with the name you specify.

### Create the WebSocket Endpoint

After the server and table setup is complete, create a websocket endpoint and provide it a reference to `PerspectiveTornadoHandler`:

```python
from perspective.handlers.tornado import PerspectiveTornadoHandler

app = tornado.web.Application([
  # ... other handlers ...
  # Create a websocket endpoint that the client JavaScript can access
  (r"/websocket", PerspectiveTornadoHandler, {"perspective_server": SERVER, "check_origin": True})
])
```

### Configuration Options

Optionally, the configuration object can also include:

- `check_origin` — a boolean that determines whether the websocket accepts requests from origins other than where the server is hosted

See [Tornado docs](https://www.tornadoweb.org/en/stable/websocket.html) for more details.

---

## JavaScript Setup

Once the server is up and running, you can access the `Table` you just hosted using `perspective.websocket` and `open_table()`.

### Create the Client

First, create a client that expects a Perspective server to accept connections at the specified URL:

```javascript
import "@perspective-dev/viewer";
import "@perspective-dev/viewer-datagrid";
import perspective from "@perspective-dev/client";

const websocket = await perspective.websocket("ws://localhost:8888/websocket");
```

### Open the Table

Next, open the `Table` we created on the server by name:

```javascript
const table = await websocket.open_table("data_source_one");
```

### Use with perspective-viewer

`table` is a proxy for the `Table` we created on the server. All operations that are possible through the JavaScript API are possible on the Python API as well.

```javascript
const viewer = document.createElement("perspective-viewer");
viewer.style.height = "500px";
document.body.appendChild(viewer);
await viewer.load(table);
```

Calling `view()`, `schema()`, `update()` etc. on `const table` will pass those operations to the Python `Table`, execute the commands, and return the result back to JavaScript.

Similarly, providing this table to a `<perspective-viewer>` instance will allow virtual rendering.

---

## Benefits

This provides for great flexibility:

- Perspective.js is full of features
- Browser WebAssembly runtimes currently have some performance restrictions on memory and CPU feature utilization
- The architecture suffers when the dataset itself is too large to download to the client in full

### Python Runtime Advantages

The Python runtime does not suffer from memory limitations:

- Utilizes Apache Arrow internal threadpools for threading and parallel processing
- Generates architecture optimized code
- Currently makes it more suitable as a server-side runtime than Node.js