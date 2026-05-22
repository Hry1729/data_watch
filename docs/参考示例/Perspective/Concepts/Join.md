# Join

`Client::join` creates a read-only Table by joining two source tables on a shared key column. The `left` and `right` arguments can be `Table` objects or string table names (as returned by `get_hosted_table_names()`).

## Key Characteristics

- The resulting table is **reactive**: whenever either source table is updated, the join is automatically recomputed
- Any `View` derived from the joined table will update accordingly
- Joined tables support the full View API — you can apply `group_by`, `split_by`, `sort`, `filter`, `expressions`, and all other View operations on the result, just as you would with any other `Table`

---

## Join Types

`Client::join` supports three join types, specified via the `join_type` option. The default is `"inner"`.

### Inner Join (default)

An **inner join** includes only rows where the key column exists in both source tables. Rows from either table that have no match in the other are excluded.

### Left Join

A **left join** includes all rows from the left table. For left rows that have no match in the right table, right-side columns are filled with `null`.

### Outer Join

An **outer join** includes all rows from both tables. Unmatched rows on either side have their missing columns filled with `null`.

### Summary Table

| join_type | Left-only rows | Right-only rows |
|-----------|----------------|-----------------|
| `"inner"` | excluded | excluded |
| `"left"` | included | excluded |
| `"outer"` | included | included |

---

## Join Options

### `on` — Join Key Column

The `on` parameter specifies the column name used to match rows between the left and right tables.

Requirements:
- This column must exist in the left table
- By default, must also exist in the right table with the same name and compatible type
- The join key column becomes the index of the resulting table

### `right_on` — Different Right Key Column

When the join key has a different name in the right table, use `right_on` to specify the right table's column name.

Behavior:
- The left table's column name (`on`) is used in the output schema
- The right key column is excluded from the result
- The `on` and `right_on` columns must have compatible types
- An error is thrown if the types do not match

### `join_type` — Join Type

Controls which rows are included in the result. See Join Types for details.

| Value | Behavior |
|-------|----------|
| `"inner"` | Only rows with matching keys in both tables (default) |
| `"left"` | All left rows; unmatched right columns are `null` |
| `"outer"` | All rows from both tables; unmatched columns are `null` |

### `name` — Table Name

An optional name for the resulting joined table.

- If omitted, a random name is generated
- This name is used to identify the table in the server's hosted table registry

---

## Reactivity and Constraints

### Reactive Updates

Joined tables are fully reactive. When either source table receives an `update()`, the join is automatically recomputed and any `View` created from the joined table will reflect the new data.

This includes:
- Updates that modify existing rows in either source table
- New rows added to either source table that create new matches
- **Chained joins** — if a joined table is itself used as input to another join, updates propagate through the entire chain

### Duplicate Keys

Like SQL, `join()` produces a cross-product for each matching key value. When multiple rows in the left table share the same key, each is paired with every matching row in the right table (and vice versa).

The number of output rows for a given key is: `left_count × right_count`.

#### Behavior Based on Table Type

| Table Type | Behavior |
|------------|----------|
| Unindexed tables (no `index` option) | Rows are appended, so duplicate keys accumulate naturally. Each `update()` appends new rows, which may introduce additional duplicates. |
| Indexed tables (`index` set to the join key) | Each key appears at most once per table, so the join produces at most one row per key. Updates replace existing rows in-place rather than appending. |

### Read-Only

Joined tables are **read-only**. Calling `update()`, `remove()`, `clear()`, or `replace()` on a joined table will throw an error.

Data can only change indirectly, by updating the source tables.

### Column Name Conflicts

The left and right tables must not have overlapping column names (other than the join key).

- If a non-key column name appears in both tables, `join()` throws an error
- Rename columns in your source data or use `View` expressions to avoid conflicts

### Source Table Deletion

A source table cannot be deleted while a joined table depends on it.

- You must delete the joined table first
- Then delete the source tables