# Advanced View Operations

Beyond the standard query configuration, View provides additional methods for interacting with hierarchical results and introspecting data.

## Tree Hierarchy Operations

When a View has `group_by` applied, the results form a tree hierarchy. Perspective provides methods to control which levels of the tree are expanded or collapsed.

### JavaScript

```javascript
const view = await table.view({ group_by: ["Region", "Country", "City"] });

// Collapse the tree at row index 5
await view.collapse(5);

// Expand the tree at row index 5
await view.expand(5);

// Set the expansion depth (0 = fully collapsed, 1 = first level, etc.)
await view.set_depth(1);
```

### Python (Using the sync API)

```python
view = table.view(group_by=["Region", "Country", "City"])

view.collapse(5)
view.expand(5)
view.set_depth(1)
```

### Rust

```rust
let view = table.view(Some(ViewConfigUpdate {
  group_by: Some(vec!["Region".into(), "Country".into(), "City".into()]),
  ..ViewConfigUpdate::default()
})).await?;

view.collapse(5).await?;
view.expand(5).await?;
view.set_depth(1).await?;
```

### Lazy Aggregation

> Perspective's built-in engine is lazy — aggregates for collapsed rows are not recalculated when the underlying Table is updated. Updates are only computed for rows that are currently visible (expanded). When a collapsed row is later expanded, its aggregates are calculated at that point.

---

## Column Range Queries

`View::get_min_max` returns the minimum and maximum values for a given column, which is useful for setting up scales in custom visualizations.

### JavaScript

```javascript
const [min, max] = await view.get_min_max("Sales");
```

### Python

```python
min_val, max_val = view.get_min_max("Sales")
```

---

## Expression Validation

Before creating a View with expressions, you can validate them against the table's schema using `Table::validate_expressions`. This returns information about which expressions are valid and their inferred types.

### JavaScript

```javascript
const result = await table.validate_expressions({
  expr1: '"Sales" + "Profit"',
  expr2: "invalid_column + 1",
});
// result.expression_schema contains valid expressions and their types
// result.errors contains invalid expressions and error messages
```

### Python

```python
result = table.validate_expressions(['"Sales" + "Profit"', 'invalid + 1'])
```

---

## View Dimensions

`View::dimensions` returns the number of rows and columns in the current view, including information about group-by header rows.

### JavaScript

```javascript
const dims = await view.dimensions();
// { num_view_rows, num_view_columns, num_table_rows, num_table_columns, ... }
```

### Python

```python
dims = view.dimensions()
```

---

## View Configuration Introspection

`View::get_config` returns the full configuration used to create the view.

### JavaScript

```javascript
const config = await view.get_config();
// { group_by: [...], split_by: [...], sort: [...], filter: [...], ... }
```

### Python

```python
config = view.get_config()
```

---

## Update Callbacks

Register a callback to be notified whenever the underlying Table is updated and the View has been recalculated.

### JavaScript

```javascript
view.on_update(
  (updated) => {
    console.log("View updated", updated.port_id);
  },
  { mode: "row" },
);

// Later, remove the callback
view.remove_update(callback);
```

### Python

```python
def on_update(port_id, delta):
  print("View updated", port_id)

view.on_update(on_update, mode="row")
view.remove_update(on_update)
```

> When `mode` is set to `"row"`, the callback receives a delta of only the rows that changed (as Apache Arrow), which is useful for efficiently synchronizing tables across clients.

---

## Flattening a View into a Table

In JavaScript, a `Table` can be constructed on a `Table::view` instance, which will return a new `Table` based on the `Table::view`'s dataset. All future updates that affect the `Table::view` will be forwarded to the new `Table`.

This is particularly useful for implementing a Client/Server Replicated design, by serializing the View to an arrow and setting up an `on_update` callback.

### JavaScript

```javascript
const worker1 = perspective.worker();
const table = await worker.table(data);
const view = await table.view({ filter: [["State", "==", "Texas"]] });
const table2 = await worker.table(view);

table.update([{ State: "Texas", City: "Austin" }]);
```

### Python

```python
table = perspective.Table(data)
view = table.view(filter=[["State", "==", "Texas"]])
table2 = perspective.Table(view.to_arrow())

def updater(port, delta):
  table2.update(delta)

view.on_update(updater, mode="Row")
table.update([{"State": "Texas", "City": "Austin"}])
```

### Rust

```rust
let opts = TableInitOptions::default();
let data = TableData::Update(UpdateData::Csv("x,y\n1,2\n3,4".into()));
let table = client.table(data, opts).await?;

let view = table.view(None).await?;
let table2 = client.table(TableData::View(view)).await?;

table.update(data).await?;
```