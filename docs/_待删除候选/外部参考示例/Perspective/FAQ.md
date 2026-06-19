# FAQ

## Installation

### Python installation fails on Windows

Python wheels are published for supported Python versions and platforms. On Windows, ensure you have a compatible Python version and architecture. Install with:

```bash
pip install perspective-python
```

If you encounter C++ binding errors or link errors, make sure you are using a supported Python version and that your pip is up to date. Pre-built wheels eliminate the need for a C++ compiler in most cases.

### Python import perspective fails with ImportError or undefined symbol

This typically happens when the C++ shared library (`libpsp.so`) cannot be found or was built against a different Python version. Ensure your Python version matches the installed wheel. On Linux, verify that required system libraries are present. If you see errors about `libpsp.so` or undefined symbols, try reinstalling in a clean virtual environment.

### Python installation fails on macOS

On Apple Silicon (M1/M2/M3), make sure you are using a native ARM Python build, not one running under Rosetta. The published wheels include aarch64 variants for supported platforms.

### How do I install Perspective in a Docker container?

Perspective's Python wheels are built against `manylinux_2_28` containers, so they are compatible with most Linux distributions based on glibc 2.28+ (e.g., Debian 10+, Ubuntu 20.04+, RHEL 8+). Use a compatible base image:

```dockerfile
FROM python:3.12-slim
RUN pip install perspective-python
```

Alpine Linux uses musl instead of glibc and is not compatible with the published wheels.

---

## JavaScript Bundling

### How do I use Perspective with Vite, Webpack, or esbuild?

Perspective no longer exports bundler plugins. Instead, you must manually bootstrap the WASM binaries using your bundler's asset handling. See [Importing with or without a Bundler](./JavaScript/Importing%20with%20or%20without%20a%20Bundler.md) for complete examples.

---

## Framework Integration

### How do I use Perspective with React?

Perspective provides a dedicated [React component](./JavaScript/React-Component.md). You must also still initialize Perspective's WebAssembly as per your bundler.

### How do I use Perspective with Next.js?

Perspective relies on Web Workers and WASM, which require client-side rendering. Use dynamic imports with `ssr: false` in Next.js to load Perspective components only on the client.

### How do I use Perspective with Vue.js/Angular/etc?

As a standard Web Component, `<perspective-viewer>` works in most JavaScript web frameworks directly via standard HTML/DOM APIs, but does not have dedicated integration libraries for these frameworks.

---

## Expressions

### How do I create computed/expression columns?

Use the `expressions` config option in your View to define new columns with ExprTK syntax, which must then be used somewhere else in your config (like `columns`) to actually be visible & calculated. In `<perspective-viewer>`, expression columns can be created from the UI column sidebar by clicking the "New Column" button.

### Can I reference one expression column from another?

No, you must duplicate calculations that are shared between expression columns.

### Can I do date arithmetic in expressions?

Yes, but they must be converted to `float` values first (`integer` is an i32 which is too small). See [Expressions](./Concepts/View-Expressions.md).

### Can I do rolling sums or cumulative calculations?

Not in Perspective's built-in engine, but DuckDB supports rolling and cumulative sums via WINDOW functions, and DuckDB now has native Perspective Virtual Server support.

---

## Filters

### Can I compose filters with OR logic?

Perspective filters are composed with AND logic by default. As an alternative, you can use expression columns to create a boolean column that encodes your OR logic:

```javascript
const view = await table.view({
  expressions: {
    or_filter:
      "if (\"State\" == 'Texas') true; else if (\"State\" == 'California') true; else false",
  },
  filter: [["or_filter", "==", true]],
});
```

### How do I update filters programmatically?

Set the `filter` property on a View config, or use `<perspective-viewer>.restore()` method to update filters at runtime.

### Does date filtering support ranges?

Date columns can be filtered with comparison operators (`>`, `<`, `>=`, `<=`) to achieve range-based filtering. Apply two filters on the same date column for a range.

---

## JupyterLab

### PerspectiveWidget is not loading in JupyterLab

See the [PerspectiveWidget guide](./Python/PerspectiveWidget.md) for full setup details. Ensure the JupyterLab extension version matches your `perspective-python` version. Make sure you are using a compatible JupyterLab for your Perspective version (JupyterLab 4+ currently).

Check that the extension is enabled with `jupyter labextension list`.

---

## Memory and Performance

### Perspective has a memory leak

Maybe, but please review the [Cleaning up resources docs](./JavaScript/Cleaning-Up-Resources.md) carefully before opening an Issue. Ensure you call `.delete()` on Views, Tables, and `<perspective-viewer>` instances when they are no longer needed, in reverse dependency order.

### How many rows can Perspective's built-in engine handle?

Perspective is designed for large datasets and can handle millions of rows depending on the number of columns and available memory. Performance also significantly depends on column types (`"string"` being slower and larger than other types).

For larger datasets or out-of-memory virtualized datasets, see [Virtual Servers](./Concepts/Virtual%20Servers.md).

### How do I control threading in perspective-python?

The Python library uses a thread pool internally. For advanced threading control, consult the [multithreading documentation](./Python/Multithreading.md).

---

## Theming and Styling

### How do I enable dark theme?

Import `themes.css` (see [Theming](./JavaScript/Viewer-Theming.md)) and set the theme via `restore()`:

```javascript
await viewer.restore({ theme: "Pro Dark" });
```

Or import just the dark theme directly: `import "@perspective-dev/viewer/dist/css/pro-dark.css";`

### Can I create a custom cell renderer for the datagrid?

The datagrid plugin supports custom styling via `column_config` and CSS custom properties, but custom cell renderers require building a custom plugin.

### How do I customize chart colors?

Chart colors can be customized via CSS custom properties on the `<perspective-viewer>` element.

---

## Streaming and Real-Time Updates

### How do I stream data into a Perspective table?

Use `table.update()` to push new data incrementally. For indexed tables, updates with matching index values will replace existing rows.

### table.update() raises "No Running Event Loop"

Perspective 3+ is now threadsafe by default and no longer requires special loop integration.

### How do I listen for data updates?

Use `view.on_update()` to register a callback that fires when the underlying table data changes. See [Listening for events](./JavaScript/Viewer-Events.md) and [Advanced View Operations](./Concepts/View-Advanced-Operations.md).

---

## Server Architecture

### What is the difference between Client-only, Client/Server, and Server-only modes?

- **Client-only**: The Perspective engine runs entirely in the browser via WASM. Best for small to medium datasets.
- **Client/Server (replicated)**: Data is hosted on a server and replicated to the client. The client has a full copy and performs queries locally.
- **Server-only**: All queries are executed on the server. The client only renders results. Best for very large datasets.

See [Data Architecture](./Concepts/Data%20Architecture.md) for detailed explanations.

### How do I set up WebSocket authentication?

The `WebSocketServer` does not include built-in authentication. Implement authentication at the transport layer (e.g., via middleware in your HTTP server) before the WebSocket upgrade.

### Can I bind Perspective to a database?

Perspective supports Virtual Servers that proxy queries to external data sources, with built-in implementations for DuckDB, ClickHouse, and Polars.

---

## Aggregation

### Can I apply multiple aggregates to the same column?

Yes, by creating a duplicate/alias for your column via `expressions`:

```javascript
await viewer.restore({
  columns: ["Sales", "Sales 2"],
  expressions: { "Sales 2": '"Sales"' },
  aggregate: {
    Sales: "sum",
    "Sales 2": "avg",
  },
});
```

### Can I compute a ratio between aggregated columns?

Use expression columns on an aggregated View to compute ratios. Define an expression that divides one column by another.

---

## Data Loading and Arrow

### How do I load Apache Arrow data into Perspective?

Perspective natively accepts Apache Arrow format. Pass an `ArrayBuffer` containing Arrow IPC data directly to `table()` or `table.update()`.

### What data formats does Perspective accept?

Perspective accepts:
- **JavaScript**: JSON (row-oriented or column-oriented objects), CSV strings, Apache Arrow ArrayBuffer
- **Python**: dict, list, `pandas.DataFrame`, `pyarrow.Table`, CSV strings, Apache Arrow bytes

### CSV update fails but CSV creation works

When updating a table created with a schema, ensure the CSV column names and types match the schema exactly. Mismatched column names or types will cause update failures.

---

## Export

### Can I export the viewer to HTML, PNG or PDF?

HTML and PNG exports are available via `viewer.export("html")` and `viewer.export("png")`. For PDF, render the viewer and use browser or headless browser screenshot capabilities.

### Can I export data to Excel?

Perspective does not have built-in Excel export. Export data via `view.to_csv()`, `view.to_json()`, or `view.to_arrow()` and convert to Excel using a library like `xlsx` (JavaScript) or `openpyxl` (Python).

### How do I copy data from a cell or row?

Use the "text" export mode when data is selected: `await viewer.export("text")`.

---

## Table Operations

### table.remove() does not update the viewer

The `remove()` method requires an indexed table. Ensure your table was created with an `index` option, and pass the index values to remove.

---

## Viewer Configuration

### How do I save and restore the viewer state?

Use `viewer.save()` and `viewer.restore()` to serialize and deserialize the full viewer configuration.

### Can I hide the configuration panel?

The settings panel can be toggled programmatically via `await viewer.restore({ settings: false })`.

### Can I collapse row groups by default?

Row groups can be closed imperatively via `view.set_depth()`. Expansion state is not persisted or configurable via the save/restore API currently.

---

## Internationalization

### Can I change the UI language?

Perspective's UI text is defined via CSS variables, which can be customized per theme. See the Icons and Translation section of the [Theming guide](./JavaScript/Viewer-Theming.md) for details.

---

## Rust

### How do I build Perspective from Rust?

See the [Getting Started guide](./Rust/Getting-Started.md) for Rust. The Rust crate wraps the C++ engine and requires a C++ toolchain. You need cmake installed and on your path to build the engine.

---

## Miscellaneous

### Can I use Perspective without <perspective-viewer>?

Yes. The perspective library (data engine) can be used independently for server-side data processing without any UI. Use `table()` and `view()` directly to query data.

### Can I use Perspective in Pyodide?

There is an emscripten wheel published via Releases, but it must be downloaded and hosted manually and is only built for specific pyodide versions.

### How do I handle row selection events?

Listen for `perspective-click` and `perspective-select` events on the `<perspective-viewer>` element.