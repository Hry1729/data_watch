# clear() and replace() Start-over Methods

## clear()

Calling `Table::clear` will remove all data from the underlying Table.

### JavaScript

```javascript
table.clear();
```

### Python

```python
table.clear()
```

---

## replace()

Calling `Table::replace` with new data will:

1. Clear the Table
2. Update it with a new dataset that conforms to Perspective's data types and the existing schema on the Table

### JavaScript

```javascript
table.replace(json);
```

### Python

```python
table.replace(df)
```

---

## Summary

| Method | Purpose | Behavior |
|--------|---------|----------|
| `clear()` | Remove all data | Empties the Table completely |
| `replace()` | Replace all data | Clears Table and loads new data with existing schema |

These methods are useful for:
- Resetting a Table to an empty state
- Completely replacing data while preserving the schema
- Refreshing data without recreating the Table