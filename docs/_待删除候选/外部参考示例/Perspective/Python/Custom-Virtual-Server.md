# Implementing a Custom Virtual Server (Python)

You can connect Perspective to any data source by subclassing `VirtualServerHandler` and wrapping it with `VirtualServer`.

For background on virtual servers, see the [Virtual Servers overview](../Concepts/Virtual%20Servers.md).

---

## Example

```python
from perspective import VirtualServerHandler, VirtualServer

class MyModel(VirtualServerHandler):
  def get_features(self):
    return {
      "group_by": True,
      "split_by": False,
      "sort": True,
      "filter_ops": {
        "string": ["==", "!=", "contains"],
        "float": ["==", "!=", ">", "<"],
      },
      "aggregates": {
        "float": ["sum", "avg", "count"],
        "string": ["count"],
      },
    }

  def get_hosted_tables(self):
    return ["my_table"]

  def table_schema(self, table_name):
    return {"name": "string", "price": "float"}

  def table_size(self, table_name):
    return 1000

  def table_make_view(self, table_name, view_id, config):
    # Translate `config` (group_by, sort, filter, etc.) into a
    # query against your data source. Store the query keyed by
    # `view_id` for later data retrieval.
    pass

  def view_delete(self, view_id):
    # Clean up resources for this view
    pass

  def view_get_data(self, view_id, start_row, end_row, start_col, end_col, ctx):
    # Execute the stored query with the given row/column window.
    # Push results via `ctx`.
    pass
```

---

## Using the Custom Handler

The `VirtualServer` instance can then be passed to a Tornado, Starlette, or AIOHTTP handler just like a regular `Server`:

```python
from perspective.handlers.tornado import PerspectiveTornadoHandler

app = tornado.web.Application([
  (r"/websocket", PerspectiveTornadoHandler, {"perspective_server": VirtualServer(MyModel)}),
])
```

---

## VirtualServerHandler Interface

### Methods

| Method | Purpose |
|--------|---------|
| `get_features()` | Return supported features |
| `get_hosted_tables()` | Return list of available table names |
| `table_schema(table_name)` | Return schema for a table |
| `table_size(table_name)` | Return row count for a table |
| `table_make_view(table_name, view_id, config)` | Create a view with given config |
| `view_delete(view_id)` | Clean up view resources |
| `view_get_data(view_id, start_row, end_row, start_col, end_col, ctx)` | Get data for a view window |

### Features Object

| Field | Description |
|-------|-------------|
| `group_by` | Whether group-by aggregation is supported |
| `split_by` | Whether split-by (pivot) is supported |
| `sort` | Whether sorting is supported |
| `filter_ops` | Map of column type to list of supported filter operators |
| `aggregates` | Map of column type to list of supported aggregate functions |