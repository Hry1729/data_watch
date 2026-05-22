# update() and remove() Streaming Methods

Once a Table has been created, it can be updated with new data conforming to the Table's schema. `Table::update` supports the same data formats as `Client::table`, minus schema.

## update()

### Without an Index

Without an index set, calls to `update()` append new data to the end of the Table.

#### JavaScript

```javascript
const schema = {
  a: "integer",
  b: "float",
};

const table = await perspective.table(schema);
table.update(new_data);
```

#### Python

```python
schema = {"a": "integer", "b": "float"}
table = perspective.Table(schema)
table.update(new_data)
```

### With an Index (Partial Updates)

Otherwise, Perspective allows partial updates (in-place) using the index to determine which rows to update:

#### JavaScript

```javascript
indexed_table.update({ id: [1, 4], name: ["x", "y"] });
```

#### Python

```python
indexed_table.update({"id": [1, 4], "name": ["x", "y"]})
```

### Unsetting Values

Any value on a `Client::table` can be unset using the value `null` in JSON or Arrow input formats. Values may be unset on construction, as any `null` in the dataset will be treated as an unset value.

### Partial Column Updates

`Table::update` calls do not need to provide all columns in the Table's schema; missing columns will be omitted from the Table's updated rows.

#### JavaScript

```javascript
table.update([{ x: 3, y: null }]); // `z` missing
```

#### Python

```python
table.update([{"x": 3, "y": None}])  # `z` missing
```

---

## remove()

Rows can also be removed from an indexed Table, by calling `Table::remove` with an array of index values:

#### JavaScript

```javascript
indexed_table.remove([1, 4]);
```

#### Python

```python
indexed_table.remove([1, 4])
```

---

## Summary

| Method | Purpose | Requirements |
|--------|---------|--------------|
| `update()` | Add or modify data | Works on all Tables; in-place updates require `index` |
| `remove()` | Delete rows by index | Requires indexed Table |