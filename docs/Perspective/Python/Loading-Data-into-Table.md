# Loading Data into a Table

A `Table` can be created from a dataset or a schema. In Python, Perspective supports additional data types that are commonly used when processing data.

## Supported Data Types

- `pandas.DataFrame`
- `polars.DataFrame`
- `bytes` (encoding an Apache Arrow)
- `objects` (either extracting a repr or via reference)
- `str` (encoding as a CSV)

---

## Creating a Table

A Table is created in a similar fashion to its JavaScript equivalent:

```python
from datetime import date, datetime
import numpy as np
import pandas as pd
import perspective

data = pd.DataFrame({
  "int": np.arange(100),
  "float": [i * 1.5 for i in range(100)],
  "bool": [True for i in range(100)],
  "date": [date.today() for i in range(100)],
  "datetime": [datetime.now() for i in range(100)],
  "string": [str(i) for i in range(100)]
})

table = perspective.table(data, index="float")
view = table.view(group_by=["float"], filter=[["bool", "==", True]])

column_data = view.to_columns()
row_data = view.to_json()
```

---

## Polars Support

Polars DataFrame types work similarly to Apache Arrow input, which Perspective uses to interface with Polars.

```python
import polars as pl
import perspective

df = pl.DataFrame({"a": [1,2,3,4,5]})
table = perspective.table(df)
```

---

## Pandas Support

Perspective's Table can be constructed from `pandas.DataFrame` objects. Internally, this just uses `pyarrow::from_pandas`, which dictates behavior of this feature including type support.

### Index Handling

- If the dataframe does not have an index set, an integer-typed column named `"index"` is created
- To preserve the indexing behavior, create the Table with `index="index"` as a keyword argument

```python
data.set_index("datetime")
table = perspective.table(data, index="index")
```

---

## Creating a View

Likewise, a View can be created via the `view()` method:

```python
view = table.view(group_by=["float"], filter=[["bool", "==", True]])

column_data = view.to_columns()
row_data = view.to_json()
```

---

## Time Zone Handling

When parsing `"datetime"` strings:

- Times without an explicit timezone offset are interpreted as **UTC**
- Strings with a timezone offset (e.g., `+05:00`) are converted to UTC
- All `"datetime"` values are stored internally as milliseconds since the Unix epoch
- Output as integer timestamps (milliseconds since epoch) from methods like `to_columns()` and `to_json()`

### Python datetime Objects

- Python `datetime` objects are serialized to strings before parsing
- Naive `datetime` objects (without `tzinfo`) produce strings without timezone information and are therefore treated as UTC
- Timezone-aware `datetime` objects include their offset in the serialized string, which is used to convert to UTC