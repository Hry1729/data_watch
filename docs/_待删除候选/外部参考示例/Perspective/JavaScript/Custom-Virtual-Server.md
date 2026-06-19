# Implementing a Custom Virtual Server (JavaScript)

You can connect Perspective to any data source by implementing the `VirtualServerHandler` interface and passing it to `createMessageHandler()`.

For background on virtual servers, see the [Virtual Servers overview](../Concepts/Virtual%20Servers.md).

---

## Example

```javascript
import perspective from "@perspective-dev/client";
import type {
  VirtualServerHandler,
  ColumnType,
  ViewConfig,
  ViewWindow,
  VirtualDataSlice,
} from "@perspective-dev/client";

const handler = {
  async getHostedTables(): Promise<string[]> {
    return ["my_table"];
  },

  async tableSchema(tableId: string): Promise<Record<string, ColumnType>> {
    return { name: "string", price: "float", date: "date" };
  },

  async tableSize(tableId: string): Promise<number> {
    return 1000;
  },

  async tableMakeView(
    tableId: string,
    viewId: string,
    config: ViewConfig,
  ): Promise<void> {
    // Translate `config` (group_by, sort, filter, etc.) into a query
    // against your data source. Store the query keyed by `viewId`
    // for later data retrieval.
  },

  async viewDelete(viewId: string): Promise<void> {
    // Clean up resources for this view
  },

  async viewGetData(
    viewId: string,
    config: ViewConfig,
    schema: Record<string, ColumnType>,
    viewport: ViewWindow,
    dataSlice: VirtualDataSlice,
  ): Promise<void> {
    // Query your data source using `config` and `viewport` for the
    // row/column window. Push columnar results via `dataSlice.setCol()`.
  },

  getFeatures() {
    return {
      group_by: true,
      sort: true,
      filter_ops: {
        string: ["==", "!=", "contains", "is null", "is not null"],
        float: ["==", "!=", ">", "<", ">=", "<="],
      },
      aggregates: {
        float: ["sum", "avg", "count", "min", "max"],
        string: ["count", "any"],
      },
    };
  },
} satisfies VirtualServerHandler;

// Create a message handler and use it like a worker
const messageHandler = perspective.createMessageHandler(handler);
const client = await perspective.worker(messageHandler);
const table = await client.open_table("my_table");
document.getElementById("viewer").load(table);
```

---

## VirtualServerHandler Interface

### Methods

| Method | Purpose |
|--------|---------|
| `getHostedTables()` | Return list of available table names |
| `tableSchema(tableId)` | Return schema for a table |
| `tableSize(tableId)` | Return row count for a table |
| `tableMakeView(tableId, viewId, config)` | Create a view with given config |
| `viewDelete(viewId)` | Clean up view resources |
| `viewGetData(viewId, config, schema, viewport, dataSlice)` | Get data for a view window |
| `getFeatures()` | Return supported features |

### Features Object

The `getFeatures()` method returns an object that tells Perspective which query operations the virtual server supports:

| Field | Type | Description |
|-------|------|-------------|
| `group_by` | `bool` | Whether group-by aggregation is supported |
| `split_by` | `bool` | Whether split-by (pivot) is supported |
| `sort` | `bool` | Whether sorting is supported |
| `expressions` | `bool` | Whether computed expressions are supported |
| `filter_ops` | `dict` | Map of column type to list of supported filter operators |
| `aggregates` | `dict` | Map of column type to list of supported aggregate functions |
| `on_update` | `bool` | Whether update callbacks are supported |