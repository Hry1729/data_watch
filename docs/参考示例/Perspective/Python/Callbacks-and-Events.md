# Callbacks and Events

`perspective.Table` allows for `on_update` and `on_delete` callbacks to be set. Simply call `on_update` or `on_delete` with a reference to a function or a lambda without any parameters.

---

## Setting Callbacks

```python
def update_callback():
  print("Updated!")
  
# set the update callback
on_update_id = view.on_update(update_callback)

def delete_callback():
  print("Deleted!")

# set the delete callback
on_delete_id = view.on_delete(delete_callback)

# set a lambda as a callback
view.on_delete(lambda: print("Deleted x2!"))
```

---

## Removing Callbacks

If the callback is a named reference to a function, it can be removed with `remove_update` or `remove_delete`:

```python
view.remove_update(on_update_id)
view.remove_delete(on_delete_id)
```

> **Note:** Callbacks defined with a lambda function cannot be removed, as lambda functions have no identifier.

---

## Summary

| Method | Purpose |
|--------|---------|
| `on_update(callback)` | Register callback for Table/View updates |
| `on_delete(callback)` | Register callback for Table/View deletion |
| `remove_update(id)` | Remove update callback by ID |
| `remove_delete(id)` | Remove delete callback by ID |