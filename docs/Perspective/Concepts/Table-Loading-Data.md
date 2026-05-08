# Loading Data

A Table may also be created-or-updated by data in CSV, Apache Arrow, JSON row-oriented or JSON column-oriented formats.

## Additional Python Support

In addition to these formats, `perspective-python` additionally supports:

- `pyarrow.Table`
- `polars.DataFrame`
- `pandas.DataFrame` objects directly

These formats are otherwise identical to the built-in formats and don't exhibit any additional support or type-awareness.

### Pandas Support Note

`pandas.DataFrame` support is just `pyarrow.Table.from_pandas` piped into Perspective's Arrow reader.

## Coercion Behavior

`Client::table` and `Table::update` perform coercion on their input for all input formats **except Arrow** (which comes with its own schema and has no need for coercion).

## Date/Datetime Column Types

`"date"` and `"datetime"` column types do not have native JSON representations, so these column types cannot be inferred from JSON input.

### For JSON Input with Date/Datetime Columns

1. A Table must first be constructed with a schema
2. Next, call `Table::update` with the JSON input

Perspective's JSON reader may coerce a date or datetime from these native JSON types:

- `integer` as milliseconds-since-epoch
- `string` as any of Perspective's built-in date format formats

---

## Data Format Examples

### Apache Arrow

The most efficient way to load data into Perspective, encoded as Apache Arrow IPC format.

```javascript
const resp = await fetch(
  "https://cdn.jsdelivr.net/npm/superstore-arrow/superstore.lz4.arrow",
);
const arrow = await resp.arrayBuffer();
```

**Note:** Apache Arrow input does not support type coercion, preferring Arrow's internal self-describing schema.

### CSV

Perspective relies on Apache Arrow's CSV parser, and as such uses mostly the same column-type inference logic as Arrow itself would use for parsing CSV.

### Row-Oriented JSON

Row-oriented JSON is in the form of a list of objects. Each object in the list corresponds to a row in the table.

```json
[
  { "a": 86, "b": false, "c": "words" },
  { "a": 0, "b": true, "c": "" },
  { "a": 12345, "b": false, "c": "here" }
]
```

### Column-Oriented JSON

Column-Oriented JSON comes in the form of an object of lists. Each key of the object is a column name, and each element of the list is the corresponding value in the row.

```json
{
  "a": [86, 0, 12345],
  "b": [false, true, false],
  "c": ["words", "", "here"]
}
```

### NDJSON

NDJSON (sometimes also referred to as JSONL) is a streaming-friendly format where each line is a valid JSON object, separated by newlines. It is commonly used in data streaming and messaging queues.

```json
{ "a": 86, "b": false, "c": "words" }
{ "a": 0, "b": true, "c": "" }
{ "a": 12345, "b": false, "c": "here" }
```