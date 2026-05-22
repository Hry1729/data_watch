# View

The `View` struct is Perspective's query and serialization interface. It represents a query on the Table's dataset and is always created from an existing Table instance via the `Table::view` method.

## Key Characteristics

- `View`s are **immutable** with respect to the arguments provided to the `Table::view` method
- To change parameters, you must create a new `View` on the same `Table`
- Each `View` is **live** with respect to the `Table`'s data
- Views will update with the latest state as its parent `Table` updates (within a conflation window)
- Includes incrementally recalculating all aggregates, pivots, filters, etc.

## Composable Query Parameters

`View` query parameters are composable, in that each parameter works independently and in conjunction with each other. There is no limit to the number of pivots, filters, etc. which can be applied.

## Examples

### JavaScript

```javascript
const table = await perspective.table({
  id: [1, 2, 3, 4],
  name: ["a", "b", "c", "d"],
});

const view = await table.view({ columns: ["name"] });
const json = await view.to_json();

await view.delete();
```

### Python

```python
table = perspective.Table({
  "id": [1, 2, 3, 4],
  "name": ["a", "b", "c", "d"]
});

view = table.view(columns=["name"])
arrow = view.to_arrow()
view.delete()
```

### Rust

```rust
let opts = TableInitOptions::default();
let data = TableData::Update(UpdateData::Csv("x,y\n1,2\n3,4".into()));
let table = client.table(data, opts).await?;

let view = table.view(None).await?;
let arrow = view.to_arrow().await?;
view.delete().await?;
```

---

## Sub-sections

1. [Querying Data](./View-Querying-Data.md)
   - [Grouping and Pivots](./View-Grouping-and-Pivots.md)
   - [Selection and Ordering](./View-Selection-and-Ordering.md)
   - [Expressions](./View-Expressions.md)
2. [Advanced View Operations](./View-Advanced-Operations.md)