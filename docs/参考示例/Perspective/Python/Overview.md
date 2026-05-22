# Python Overview

Perspective for Python uses the exact same C++ data engine used by the WebAssembly version and Rust version.

## Key Features

- The library consists of many of the same abstractions and API as in JavaScript
- Python-specific data loading support for NumPy, Pandas, and Apache Arrow
- Session manager suitable for integration into server systems
- Supports Tornado websockets, AIOHTTP, and Starlette/FastAPI

## Example

A simple example which loads an Apache Arrow and computes a "Group By" operation, returning a new Arrow:

```python
from perspective import Server

client = Server().new_local_client()
table = client.table(arrow_bytes_data)
view = table.view(group_by = ["CounterParty", "Security"])
arrow = view.to_arrow()
```

---

## What's Included

The `perspective` module exports several tools:

- `Server` — the constructor for a new instance of the Perspective data engine

The `perspective.widget` module exports:

- `PerspectiveWidget` — the JupyterLab widget for interactive visualization in a notebook cell

The `perspective.handlers` modules exports web frameworks handlers:

- `perspective.handlers.tornado.PerspectiveTornadoHandler` — for Tornado
- `perspective.handlers.starlette.PerspectiveStarletteHandler` — for Starlette and FastAPI
- `perspective.handlers.aiohttp.PerspectiveAIOHTTPHandler` — for AIOHTTP

---

## Virtual UI Server

As `<perspective-viewer>` or any other Perspective Client will only consume the data necessary to render the current screen (or whatever else was requested via the API), this runtime mode allows large datasets without the need to copy them entirely to the Browser, at the expense of network latency on UI interaction/API calls.

---

## JupyterLab

`PerspectiveWidget` is a JupyterLab widget that implements the same API as `<perspective-viewer>`, allowing running such a viewer in JupyterLab in either server or client (via WebAssembly) mode.

`PerspectiveWidget` is compatible with:
- JupyterLab 3
- Jupyter Notebook 6 (via a prebuilt extension)

To use it, simply install `perspective-python` and the extensions should be available.

### Builtin Viewers

`perspective-python`'s JupyterLab extension also provides convenient builtin viewers for `csv`, `json`, or `arrow` files. Simply right-click on a file with this extension and choose the appropriate Perspective option from the context menu.

---

## More Examples

More examples are available on [GitHub](https://github.com/finos/perspective/tree/master/examples).