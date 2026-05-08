# Index and Limit Options

> **Note:** `limit` cannot be used in conjunction with `index`.

## Index

Initializing a Table with an **index** tells Perspective to treat a column as the primary key, allowing in-place updates of rows. Only a single column (of any type) can be used as an `index`.

### Indexed Table Features

Indexed Table instances allow:

- **In-place updates**: Whenever a new row shares an index values with an existing row
- **Partial updates**: When a data batch omits some column
- **Removes**: Delete a row by index

### Creating an Indexed Table

To create an indexed Table, provide the `index` property with a string column name to be used as an index:

#### JavaScript

```javascript
const indexed_table = await perspective.table(data, { index: "a" });
```

#### Python

```python
indexed_table = perspective.Table(data, index="a")
```

---

## Limit

Initializing a Table with a **limit** sets the total number of rows the Table is allowed to have.

### Behavior

When the Table is updated, and the resulting size of the Table would exceed its `limit`, rows that exceed `limit` overwrite the **oldest rows** in the Table.

### Creating a Table with Limit

To create a Table with a limit, provide the `limit` property with an integer indicating the maximum rows:

#### JavaScript

```javascript
const limit_table = await perspective.table(data, { limit: 1000 });
```

#### Python

```python
limit_table = perspective.Table(data, limit=1000)
```

---

## Summary

| Option | Purpose | Behavior |
|--------|---------|----------|
| `index` | Primary key column | Enables in-place updates and row removal |
| `limit` | Maximum row count | Older rows are overwritten when limit exceeded |

> **Important:** You cannot use both `index` and `limit` on the same Table.