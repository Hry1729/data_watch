# Serializing Data

The `view()` allows for serialization of data to JavaScript through the following methods:

- `to_json()`
- `to_ndjson()`
- `to_columns()`
- `to_csv()`
- `to_arrow()`

These methods return a promise for the calculated data.

## Example

```javascript
const view = await table.view({ group_by: ["State"], columns: ["Sales"] });

// JavaScript Objects
console.log(await view.to_json());
console.log(await view.to_columns());

// String
console.log(await view.to_csv());
console.log(await view.to_ndjson());

// ArrayBuffer
console.log(await view.to_arrow());
```

## Supported Formats

| Method | Return Type | Description |
|--------|-------------|-------------|
| `to_json()` | Object | Row-oriented JSON (array of objects) |
| `to_columns()` | Object | Column-oriented JSON (object of arrays) |
| `to_csv()` | String | CSV format |
| `to_ndjson()` | String | NDJSON (newline-delimited JSON) |
| `to_arrow()` | ArrayBuffer | Apache Arrow IPC format |

---

## Data Format Details

These are the same data formats supported by the `Client::table` factory function.