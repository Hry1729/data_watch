# Grouping and Pivots

## Group By

A **group by** groups the dataset by the unique values of each column used as a group by - a close analogue in SQL to the `GROUP BY` statement.

### Behavior

- The underlying dataset is aggregated to show the values belonging to each group
- A **total row** is calculated for each group, showing the currently selected aggregated value (e.g. `sum`) of the column
- Group by are useful for hierarchies, categorizing data and attributing values

### Example

Showing the number of units sold based on State and City:

- A group by of `["State", "City", "Postal Code"]` shows the values for each Postal Code
- Which are grouped by City, which are in turn grouped by State

### Implementation

In Perspective, group by are represented as an array of string column names to pivot, and are applied in the order provided.

#### JavaScript

```javascript
const view = await table.view({ group_by: ["a", "c"] });
```

#### Python

```python
view = table.view(group_by=["a", "c"])
```

#### Rust

```rust
let view = table.view(Some(ViewConfigUpdate {
  group_by: Some(vec!["a".into(), "c".into()]),
  ..ViewConfigUpdate::default()
})).await?;
```

---

## Split By

A **split by** splits the dataset by the unique values of each column used as a split by.

### Behavior

- The underlying dataset is **not aggregated**
- A new column is created for each unique value of the split by
- Each newly created column contains the parts of the dataset that correspond to the column header

### Example

A View that has `["State"]` as its split by will have a new column for each state.

### Implementation

In Perspective, Split By are represented as an array of string column names to pivot.

#### JavaScript

```javascript
const view = await table.view({ split_by: ["a", "c"] });
```

#### Python

```python
view = table.view(split_by=["a", "c"])
```

#### Rust

```rust
let view = table.view(Some(ViewConfigUpdate {
  split_by: Some(vec!["a".into(), "c".into()]),
  ..ViewConfigUpdate::default()
})).await?;
```

---

## Aggregates

Aggregates perform a calculation over an entire column, and are displayed when one or more Group By are applied to the View.

### Default Aggregates

Perspective uses the following sensible default aggregates based on column type:

- `"sum"` for integer and float columns
- `"count"` for all other columns

### Custom Aggregates

Perspective provides a selection of aggregate functions that can be applied to columns in the View constructor using a dictionary of column name to aggregate function name.

#### JavaScript

```javascript
const view = await table.view({
  aggregates: {
    a: "avg",
    b: "distinct count",
  },
});
```

#### Python

```python
view = table.view(
  aggregates={
    "a": "avg",
    "b": "distinct count"
  }
)
```

#### Rust

```rust
use std::collections::HashMap;

let view = table.view(Some(ViewConfigUpdate {
  aggregates: Some(HashMap::from([
    ("a".into(), "avg".into()),
    ("b".into(), "distinct count".into()),
  ])),
  ..ViewConfigUpdate::default()
})).await?;
```

### Available Aggregate Functions by Column Type

| Column Type | Available Aggregates |
|-------------|----------------------|
| Numeric (`integer`, `float`) | `sum`, `abs sum`, `sum abs`, `sum not null`, `any`, `avg`, `mean`, `count`, `distinct count`, `dominant`, `first`, `last`, `last by index`, `high`, `low`, `max`, `min`, `high minus low`, `last minus first`, `median`, `q1`, `q3`, `pct sum parent`, `pct sum total`, `stddev`, `var`, `unique`, `weighted mean`, `min by`, `max by` |
| String | `count`, `any`, `distinct count`, `dominant`, `first`, `last`, `last by index`, `join`, `median`, `q1`, `q3`, `unique`, `min by`, `max by` |
| Date/Datetime | `count`, `any`, `avg`, `distinct count`, `dominant`, `first`, `last`, `last by index`, `high`, `low`, `max`, `min`, `median`, `q1`, `q3`, `unique` |
| Boolean | `count`, `any`, `distinct count`, `dominant`, `first`, `last`, `last by index`, `unique` |