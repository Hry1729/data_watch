# Schema and Column Types

The mapping of a Table's column names to data types is referred to as a **schema**. Each column has a unique name and a single data type.

## Available Column Types

| Type | Description |
|------|-------------|
| `float` | Floating-point numbers |
| `integer` | Integer numbers |
| `boolean` | True/false values |
| `date` | Date values (without time) |
| `datetime` | Date and time values |
| `string` | Text strings |

## Schema Definition

A Table schema is fixed at construction, either by:

1. Explicitly passing a schema dictionary to the `Client::table` method
2. Passing data from which the schema is inferred (if CSV or JSON format) or inherited (if Arrow)

## Type Inference

When passing CSV or JSON data to the `Client::table` constructor, the type of each column is inferred automatically. In some cases, the inference algorithm may not return exactly what you'd like.

### Common Inference Issues

- A column may be interpreted as a `datetime` when you intended it to be a `string`
- A column may have no values at all (yet), as it will be updated with values from a real-time data source later on

### Solution

In these cases, create a `table()` with an explicit schema.

## Type Coercion on Updates

Once the Table has been created, further `Table::update` calls will perform limited type coercion based on the schema.

- Coercion works similarly to inference
- `Table::update` will not change the column's type further
- For example, a number literal `1234` would be inferred as an `"integer"`
- In the context of a `Table::update` call on a known `"string"` column, this will be parsed as the string `"1234"`

## Date and Datetime Inference

Various string representations of date and datetime format columns can be inferred as well as coerced from strings if they match one of Perspective's internal known datetime parsing formats.

### Supported Formats

- ISO 8601 format (which is also the format Perspective will output these types for CSV)

### Native JSON Types for Date/Datetime

For columns of these types for JSON input, a Table must first be constructed with a schema. Then, call `Table::update` with the JSON input - Perspective's JSON reader may coerce a date or datetime from these native JSON types:

- **integer** as milliseconds-since-epoch
- **string** as any of Perspective's built-in date format formats

### Important Notes

- JavaScript `Date` and Python `datetime.date` and `datetime.datetime` are not supported directly
- However, in JavaScript, `Date` types are automatically coerced to correct integer timestamps by default when converted to JSON