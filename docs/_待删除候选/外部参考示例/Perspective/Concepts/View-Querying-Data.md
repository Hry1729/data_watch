# Querying Data

To query the table, create a `Table::view` on the table instance with an optional configuration object. A `Table` can have as many `View`s associated with it as you need - Perspective conserves memory by relying on a single `Table` to power multiple `View`s concurrently.

## JavaScript

```javascript
const view = await table.view({
  columns: ["Sales"],
  aggregates: { Sales: "sum" },
  group_by: ["Region", "Country"],
  filter: [["Category", "in", ["Furniture", "Technology"]]],
});
```

## Python

```python
view = table.view(
  columns=["Sales"],
  aggregates={"Sales": "sum"},
  group_by=["Region", "Country"],
  filter=[["Category", "in", ["Furniture", "Technology"]]]
)
```

## Rust

```rust
use crate::config::*;

let view = table
  .view(Some(ViewConfigUpdate {
    columns: Some(vec![Some("Sales".into())]),
    aggregates: Some(HashMap::from_iter(vec![("Sales".into(), "sum".into())])),
    group_by: Some(vec!["Region".into(), "Country".into()]),
    filter: Some(vec![Filter::new("Category", "in", &[
      "Furniture",
      "Technology",
    ])]),
    ..ViewConfigUpdate::default()
  }))
  .await?;
```

---

## Query Parameters

| Parameter | Description |
|-----------|-------------|
| `columns` | Which columns to include in output |
| `aggregates` | Aggregate functions for columns |
| `group_by` | Columns to group by |
| `split_by` | Columns to split/pivot by |
| `filter` | Filter conditions |
| `sort` | Sort columns and directions |
| `expressions` | Computed columns |