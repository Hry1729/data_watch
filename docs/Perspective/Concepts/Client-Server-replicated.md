# Client/Server replicated

For medium-sized, real-time, synchronized and/or editable data sets with many concurrent users.

## Overview

The dataset is instantiated in-memory with a Python or Node.js Perspective server, and web applications create duplicates of these tables in a local WebAssembly client in the browser, synchronized efficiently to the server via Apache Arrow.

### Advantages

- **Good interactive performance on the client**: Identical to client-only architecture
- **Efficient synchronization**: Updates and edits are seamlessly synchronized across clients via their virtual server counterparts using websockets and Apache Arrow
- **Scalable**: This design scales well with additional concurrent users, as browsers only need to download the initial data set and subsequent update deltas
- **Client-side operations**: Operations like scrolling, pivots, sorting, etc. are performed on the client

### Python Server Benefits

Python servers can make especially good use of additional threads, as Perspective will release the GIL for almost all operations.

## Architecture Diagram

```
Python Server
└── PerspectiveManager - Thread 1
    └── table(arrow)
        └── view({group_by: ["State"], split_by: ["Segment"]})

Browser
└── <html>
    └── <perspective-viewer view="heatmap" row-pivots='["State"]' column-pivots='["Segment"]'>
```

## Usage

### Python and Tornado Server

```python
from perspective import Server, PerspectiveTornadoHandler

server = Server()
client = server.new_local_client()
client.table(csv, name="my_table")

routes = [(
    r"/websocket",
    perspective.handlers.tornado.PerspectiveTornadoHandler,
    {"perspective_server": server},
)]

app = tornado.web.Application(routes)
app.listen(8080)

loop = tornado.ioloop.IOLoop.current()
loop.start()
```

### Javascript Client

Perspective's websocket client interfaces with the Python server, then replicates the server-side Table.

```javascript
const websocket = await perspective.websocket("ws://localhost:8080");
const server_table = await websocket.open_table("my_table");
const server_view = await server_table.view();

const worker = await perspective.worker();
const client_table = await worker.table(server_view);

const viewer = document.createElement("perspective-viewer");
document.body.appendChild(viewer);
await viewer.load(client_table);
```

## When to Use

Client/Server replicated architecture is ideal for:
- Medium-sized datasets
- Real-time synchronized data
- Editable datasets
- Applications with many concurrent users
- When you need both server-side data management and client-side interactivity