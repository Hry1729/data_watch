# Importing with or without a Bundler

Perspective requires the browser to have access to Perspective's `.wasm` binaries in addition to the bundled `.js` files. As a result, the build process requires a few extra steps.

Perspective's NPM releases come with multiple prebuilt configurations.

---

## ESM Builds with a Bundler

The recommended builds for production use are packaged as ES Modules and require a bootstrapping step in order to acquire the `.wasm` binaries and initialize Perspective's JavaScript with them.

### Why ESM Builds?

- They have no hard-coded dependencies on the `.wasm` paths
- Ideal for use with JavaScript bundlers such as ESBuild, Rollup, Vite or Webpack

### Bootstrapping

ESM builds must be bootstrapped with their `.wasm` binaries to initialize. The wasm binaries can be found in their respective `dist/wasm` directories.

```javascript
import perspective_viewer from "@perspective-dev/viewer";
import perspective from "@perspective-dev/client";

// TODO These paths must be provided by the bundler!
const SERVER_WASM = ... // "@perspective-dev/server/dist/wasm/perspective-server.wasm"
const CLIENT_WASM = ... // "@perspective-dev/viewer/dist/wasm/perspective-viewer.wasm"

await Promise.all([
  perspective.init_server(SERVER_WASM),
  perspective_viewer.init_client(CLIENT_WASM),
]);

// Now Perspective API will work!
const worker = await perspective.worker();
const viewer = document.createElement("perspective-viewer");
```

---

## Vite

You'll need to target `esnext` in your `vite.config.js` in order to run the build step:

```javascript
import SERVER_WASM from "@perspective-dev/server/dist/wasm/perspective-server.wasm?url";
import CLIENT_WASM from "@perspective-dev/viewer/dist/wasm/perspective-viewer.wasm?url";

await Promise.all([
  perspective.init_server(fetch(SERVER_WASM)),
  perspective_viewer.init_client(fetch(CLIENT_WASM)),
]);
```

### vite.config.js

```javascript
import { defineConfig } from "vite";

export default defineConfig({
  build: {
    target: "esnext",
  },
});
```

---

## ESBuild

ESBuild config JSON to encode this asset as a file:

```javascript
import SERVER_WASM from "@perspective-dev/server/dist/wasm/perspective-server.wasm";
import CLIENT_WASM from "@perspective-dev/viewer/dist/wasm/perspective-viewer.wasm";

await Promise.all([
  perspective.init_server(fetch(SERVER_WASM)),
  perspective_viewer.init_client(fetch(CLIENT_WASM)),
]);
```

```json
{
  // ...
  "loader": {
    // ...
    ".wasm": "file"
  }
}
```

---

## Webpack

Webpack config:

```javascript
import SERVER_WASM from "@perspective-dev/server/dist/wasm/perspective-server.wasm";
import CLIENT_WASM from "@perspective-dev/viewer/dist/wasm/perspective-viewer.wasm";

await Promise.all([
  perspective.init_server(SERVER_WASM),
  perspective_viewer.init_client(CLIENT_WASM),
]);
```

```javascript
{
  // ...
  module: {
    // ...
    rules: [
      // ...
      {
        test: /\.wasm$/,
        type: "asset/resource"
      },
    ]
  },
  experiments: {
    // ...
    asyncWebAssembly: false,
    syncWebAssembly: false,
  },
}
```

---

## Inline Builds with a Bundler

> Inline builds are deprecated and will be removed in a future release.

Perspective's Inline Builds work by inlining WebAssembly binary content as a base64-encoded string. While inline builds work with most bundlers and do not require bootstrapping, there is an inherent file-size and boot-performance penalty.

**Prefer your bundler's inlining features and Perspective ESM builds where possible.**

```javascript
import "@perspective-dev/viewer/dist/esm/perspective-viewer.inline.js";
import psp from "@perspective-dev/client/dist/esm/perspective.inline.js";
```

---

## CDN Builds

Perspective's CDN builds are good for non-bundled scenarios, such as importing directly from a `<script>` tag.

### Characteristics

- CDN builds do not require bootstrapping the WebAssembly binaries
- They also generally do not work with bundlers
- CDN builds are in ES Module format

### Usage

To include them via a CDN, they must be imported from a `<script type="module">`:

```html
<script type="module">
import "https://cdn.jsdelivr.net/npm/@perspective-dev/viewer/dist/cdn/perspective-viewer.js";
import "https://cdn.jsdelivr.net/npm/@perspective-dev/viewer-datagrid/dist/cdn/perspective-viewer-datagrid.js";
import "https://cdn.jsdelivr.net/npm/@perspective-dev/viewer-d3fc/dist/cdn/perspective-viewer-d3fc.js";
import perspective from "https://cdn.jsdelivr.net/npm/@perspective-dev/client/dist/cdn/perspective.js";

// .. Do stuff here ..
</script>
```

---

## Node.js Builds

The Node.js runtime for the `@perspective-dev/client` module runs in-process by default and does not implement a child_process interface.

### Key Differences

- There is no `worker()` method
- The module object itself directly exports the full perspective API
- Perspective does not run in a WebWorker (as this API does not exist in Node.js)
- No need to call the `.worker()` factory function
- The perspective library exports the functions directly and run synchronously in the main process

```javascript
const perspective = require("@perspective-dev/client");
```