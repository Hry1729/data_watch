# Server only

For extremely large datasets with a small number of concurrent users.

## Overview

The dataset is instantiated in-memory with a Python or Node.js server, and web applications connect virtually.

### Advantages

- **Very good initial load performance**: Since no data is downloaded
- **Column-parallel operations**: Group-by and other operations will run column-parallel if configured
- **Automatic persistence**: Changes like edits and updates are automatically reflected to all clients and persist across browser refresh

### Limitations

- **Poor interactive performance**: Every user interaction must page the server to render
- **Not responsive**: Operations like scrolling are not as responsive and can be impacted by network latency
- **Always connected requirement**: Web applications must be "always connected" to the server via WebSocket
- **No WebAssembly**: Disconnecting will prevent any interaction, scrolling, etc. of the UI

## Scalability Considerations

Each connected browser will impact server performance as long as the connection is open, which in turn impacts interactive performance of every client. This ultimately limits the horizontal scalability of this architecture.

## Architecture Diagram

```
Python Server
└── PerspectiveManager - Thread 2
    └── table(df)
        └── view({group_by: ["State"]})

Browser
├── WebWorker 2
└── <html>
    └── table(...)
        └── view({group_by: ["Category"], filter: [["State","==","Texas"]]})
            └── <perspective-viewer view="heatmap" row-pivots='["State"]' column-pivots='["Segment"]'>
```

## Usage

Using the same Python server as the previous design, we can simply skip the intermediate WebAssembly Table and pass the virtual table directly to `load()`:

```javascript
const websocket = await perspective.websocket("ws://localhost:8080");
const server_table = await websocket.open_table("my_table");

const viewer = document.createElement("perspective-viewer");
document.body.appendChild(viewer);
await viewer.load(server_table);
```

## When to Use

Server-only architecture is ideal for:
- Extremely large datasets that cannot fit in browser memory
- Small number of concurrent users
- When network latency is acceptable
- When you need server-side data processing without client replication