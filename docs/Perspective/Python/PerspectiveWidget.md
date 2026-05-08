# PerspectiveWidget for JupyterLab

Building on top of the API provided by `perspective.Table`, the `PerspectiveWidget` is a JupyterLab plugin that offers the entire functionality of Perspective within the Jupyter environment.

## Features

- Supports the same API semantics as `<perspective-viewer>`
- Additional data types supported by `perspective.Table`
- Takes keyword arguments for the managed `View`

---

## Creating a Widget

A widget is created through the `PerspectiveWidget` constructor, which takes as its first, required parameter:

- A `perspective.Table`
- A dataset
- A schema
- `None` (defers loading until later)

### Important Note

In maintaining consistency with the JavaScript API, Widgets cannot be created with empty dictionaries or lists — `None` should be used if the intention is to await data for loading later on.

---

## Examples

### From a Dataset

```python
from perspective.widget import PerspectiveWidget

PerspectiveWidget(data, group_by=["date"])
```

### From a Schema

```python
from perspective.widget import PerspectiveWidget

PerspectiveWidget({"a": int, "b": str})
```

### From a Table

```python
from perspective.widget import PerspectiveWidget
import perspective

table = perspective.table(data)
PerspectiveWidget(table)
```

### With Full Configuration

```python
from perspective.widget import PerspectiveWidget

w = perspective.PerspectiveWidget(
  data,
  plugin="X Bar",
  aggregates={"datetime": "any"},
  sort=[["date", "desc"]]
)
```

---

## Updating a Widget

`PerspectiveWidget` shares a similar API to the `<perspective-viewer>` Custom Element, and has similar `save()` and `restore()` methods that serialize/deserialize UI state for the widget.