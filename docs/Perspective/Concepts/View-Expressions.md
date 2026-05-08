# Expressions

The `expressions` property specifies new columns in Perspective that are created using existing column values or arbitrary scalar values defined within the expression. In `<perspective-viewer>`, expressions are added using the "New Column" button in the side panel.

## Syntax

Expressions are strings parsed by Perspective's expression engine (based on ExprTK). Column names are referenced by wrapping them in double quotes, e.g. `"Sales"`.

### JavaScript

```javascript
const view = await table.view({
  expressions: {
    "Profit Ratio": '"Profit" / "Sales"',
  },
});
```

### Python

```python
view = table.view(expressions={'Profit Ratio': '"Profit" / "Sales"'})
```

### Rust

```rust
let view = table.view(Some(ViewConfigUpdate {
  expressions: Some(Expressions([
    ("Profit Ratio", "\"Profit\" / \"Sales\"".into())
  ].into_iter().collect())),
  ..ViewConfigUpdate::default()
})).await?;
```

---

## Type Conversion and Coercion

Perspective expressions are strongly typed — each column and literal has a fixed type, and most operators require matching types on both sides.

### Conversion Functions

| Function | Description |
|----------|-------------|
| `to_string(x)` | Convert any type to string |
| `to_integer(x)` | Convert to integer (null if not parsable) |
| `to_float(x)` | Convert to float (null if not parsable) |
| `to_boolean(x)` | Convert to boolean (truthy/falsy) |
| `integer(x)` | Alias for `to_integer(x)` |
| `float(x)` | Alias for `to_float(x)` |
| `datetime(x)` | Construct a datetime from a POSIX timestamp (ms since epoch) |
| `date(y, m, d)` | Construct a date from year, month, day |

### How Coercion Works

Perspective does **not** implicitly coerce types. For example:

- You cannot directly add an integer to a float — you must cast one side explicitly
- Similarly, `datetime` and `date` values are not numeric
- To perform arithmetic on them, you must first convert to a numeric representation, do the math, then convert back

### Datetime Internal Representation

Internally, `datetime` values are stored as milliseconds since the Unix epoch (1970-01-01T00:00:00Z):

- Converting a `datetime` to a `float` yields this millisecond timestamp
- `datetime()` accepts a millisecond timestamp to produce a `datetime`

### Example: Offsetting a datetime by 7 days

```javascript
// Due Date
datetime(float("Shipped Date") + 604800000)
```

This expression takes a `"Shipped Date"` column, converts it to its millisecond-epoch representation, adds 7 days worth of milliseconds (7 × 24 × 60 × 60 × 1000 = 604800000), and converts the result back to a `datetime`.

---

## Operators

Standard arithmetic and comparison operators are supported:

| Operator | Description |
|----------|-------------|
| `+`, `-`, `*`, `/` | Arithmetic |
| `%` | Modulo |
| `==`, `!=`, `<`, `>`, `<=`, `>=` | Comparison |
| `and`, `or`, `not` | Logical |
| `if ... else ...` | Conditional |

---

## Numeric Functions

ExprTK provides a rich set of built-in numeric functions including:

- `abs`, `ceil`, `floor`, `round`
- `exp`, `log`, `log10`, `sqrt`
- `min`, `max`, `pow`
- `clamp`, `iclamp`, `inrange`
- Trigonometric functions: `sin`, `cos`, `tan`, `asin`, `acos`, `atan`

---

## String Functions

| Function | Description |
|----------|-------------|
| `concat(a, b, ...)` | Concatenate strings |
| `upper(s)` | Convert to uppercase |
| `lower(s)` | Convert to lowercase |
| `length(s)` | String length |
| `contains(s, substr)` | Whether `s` contains `substr` |
| `order(col, 'B', 'C', 'A')` | Custom sort order for a string column |
| `match(s, pattern)` | Regex partial match (returns boolean) |
| `match_all(s, pattern)` | Regex full match (returns boolean) |
| `search(s, pattern)` | First capturing group match |
| `indexof(s, pattern)` | Start index of first regex match |
| `substring(s, start, end)` | Substring from start (inclusive) to end (exclusive) |
| `replace(s, repl, pattern)` | Replace first regex match |
| `replace_all(s, repl, pattern)` | Replace all regex matches |

---

## Date/Datetime Functions

| Function | Description |
|----------|-------------|
| `today()` | Current date |
| `now()` | Current datetime |
| `date(year, month, day)` | Construct a date |
| `datetime(timestamp_ms)` | Construct a datetime from a POSIX timestamp (ms since epoch) |
| `hour_of_day(dt)` | Hour component (0-23) |
| `day_of_week(dt)` | Day of the week as a string |
| `month_of_year(dt)` | Month of the year as a string |
| `bucket(dt, unit)` | Bucket datetime by unit: `'s'`, `'m'`, `'h'`, `'D'`, `'W'`, `'M'`, `'Y'` |

**Note:** `bucket` also works on numeric columns: `bucket("Price", 10)` rounds values down to the nearest multiple of 10.

---

## Other Functions

| Function | Description |
|----------|-------------|
| `is_null(x)` | Whether the value is null |
| `is_not_null(x)` | Whether the value is not null |
| `percent_of(a, b)` | `a` as a percentage of `b` |
| `inrange(low, val, high)` | Whether `val` is between `low` and `high` (inclusive) |
| `min(a, b, ...)` | Minimum of inputs |
| `max(a, b, ...)` | Maximum of inputs |
| `random()` | Random float between 0.0 and 1.0 |
| `col(name)` | Look up a column by string name at runtime |
| `vlookup(col, key)` | Look up a value in another column by row key |