# Selection and Ordering

## Columns

The `columns` property specifies which columns should be included in the View's output.

### Purpose

This allows users to:

- Show or hide a specific subset of columns
- Control the order in which columns appear to the user

### Implementation

In Perspective, this is represented as an array of string column names.

#### JavaScript

```javascript
const view = await table.view({
  columns: ["a"],
});
```

#### Python

```python
view = table.view(columns=["a"])
```

#### Rust

```rust
let view = table.view(Some(ViewConfigUpdate {
  columns: Some(vec![Some("a".into())]),
  ..ViewConfigUpdate::default()
})).await?;
```

---

## Sort

The `sort` property specifies columns on which the query should be sorted, analogous to `ORDER BY` in SQL.

### Behavior

- A column can be sorted regardless of its data type
- Sorts can be applied in ascending or descending order
- Perspective represents sort as an array of arrays
- Each inner array contains: a string column name and a string sort direction
- When `split_by` are applied, the additional sort directions `"col asc"` and `"col desc"` will determine the order of pivot column groups

#### JavaScript

```javascript
const view = await table.view({
  sort: [["a", "asc"]],
});
```

#### Python

```python
view = table.view(sort=[["a", "asc"]])
```

#### Rust

```rust
let view = table.view(Some(ViewConfigUpdate {
  sort: Some(vec![Sort("a".into(), SortDir::Asc)]),
  ..ViewConfigUpdate::default()
})).await?;
```

### Available Sort Directions

| Direction | Description |
|-----------|-------------|
| `"asc"` | Ascending order |
| `"desc"` | Descending order |
| `"asc abs"` | Ascending by absolute value |
| `"desc abs"` | Descending by absolute value |
| `"col asc"` | Ascending order for pivot column groups (requires `split_by`) |
| `"col desc"` | Descending order for pivot column groups (requires `split_by`) |
| `"col asc abs"` | Ascending by absolute value for pivot column groups |
| `"col desc abs"` | Descending by absolute value for pivot column groups |

---

## Filter

The `filter` property specifies columns on which the query can be filtered, returning rows that pass the specified filter condition. This is analogous to the `WHERE` clause in SQL.

### Behavior

- There is no limit on the number of columns where filter is applied
- The resulting dataset is one that passes **all** the filter conditions (filters are joined with an `AND` condition)
- The join condition can be changed to `OR` via the `filter_op` property
- Perspective represents filter as an array of arrays
- Each inner array contains: a string column name, a string filter operator, and a filter operand in the type of the column

#### JavaScript

```javascript
const view = await table.view({
  filter: [["a", "<", 100]],
});
```

#### Python

```python
view = table.view(filter=[["a", "<", 100]])
```

#### Rust

```rust
let view = table.view(Some(ViewConfigUpdate {
  filter: Some(vec![Filter::new("a", "<",
    FilterTerm::Scalar(Scalar::Float(100.0)))]),
  ..ViewConfigUpdate::default()
})).await?;
```

### Available Filter Operators by Column Type

| Column Type | Available Operators |
|-------------|---------------------|
| String | `==`, `!=`, `>`, `>=`, `<`, `<=`, `begins with`, `contains`, `ends with`, `in`, `not in`, `is not null`, `is null` |
| Numeric (`integer`, `float`) | `==`, `!=`, `>`, `>=`, `<`, `<=`, `is not null`, `is null` |
| Boolean | `==`, `is not null`, `is null` |
| Date/Datetime | `==`, `!=`, `>`, `>=`, `<`, `<=`, `is not null`, `is null` |