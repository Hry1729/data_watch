# Perspective Data Engine Library

## Accessing the Perspective Engine via a Client Instance

An instance of a `Client` is needed to talk to a Perspective `Server`, of which there are a few varieties available in JavaScript.

---

## Web Worker (Browser)

Perspective's Web Worker client is actually a `Client` and `Server` rolled into one.

### Characteristics

- Instantiating this `Client` will also create a dedicated Perspective `Server` in a Web Worker process
- You'll need to instantiate a Web Worker perspective engine via the `worker()` method
- This will create a new Web Worker (browser) and load the WebAssembly binary
- All calculation and data accumulation will occur in this separate process

### Usage

The worker symbol will expose the full perspective API for one managed Web Worker process.

You are free to create as many as your browser supports, but be sure to keep track of the worker instances themselves, as you'll need them to interact with your data in each instance.

```javascript
const client = await perspective.worker();
```

---

## WebSocket (Browser)

Alternatively, with a Perspective server running in Node.js, Python or Rust, you can create a virtual `Client` via the `websocket()` method.

### Usage

```javascript
const client = await perspective.websocket("http://localhost:8080/");
```

---

## Node.js

The Node.js runtime for the `@perspective-dev/client` module runs in-process by default and does not implement a child_process interface.

### Key Points

- No need to call the `.worker()` factory function
- The perspective library exports the functions directly
- Runs synchronously in the main process

```javascript
const client = require("@perspective-dev/client");
```

---

## Sub-sections

1. [Serializing Data](./Serializing-Data.md)
2. [Cleaning Up Resources](./Cleaning-Up-Resources.md)
3. [Hosting a WebSocket Server in Node.js](./Hosting-WebSocketServer.md)
4. [Customizing perspective.worker()](./Customizing-Worker.md)
5. [Joining Tables](./Joining-Tables.md)