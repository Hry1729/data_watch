# Construct an Empty Table from a Schema

Examples of constructing an empty Table from a schema.

## JavaScript

```javascript
var schema = {
  x: "integer",
  y: "string",
  z: "boolean",
};

const table2 = await worker.table(schema);
```

## Python

```python
from datetime import date, datetime

schema = {
  "x": "integer",
  "y": "string",
  "z": "boolean",
}

table2 = perspective.table(schema)
```

## Rust

```rust
let data = TableData::Schema(vec![("a".to_string(), ColumnType::FLOAT)]);
let options = TableInitOptions::default();
let table = client.table(data.into(), options).await?;
```

## Notes

- Constructing a Table from a schema is useful when you want to create a Table before loading data
- This is particularly useful for Tables that will be updated with real-time data
- The schema defines the column names and types that will be used
- Once the Table is created with a schema, you can use `Table::update()` to add data later