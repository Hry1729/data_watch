# Table

Table is Perspective's columnar data frame, analogous to a Pandas DataFrame or Apache Arrow, supporting append & in-place updates, removal by index, and update notifications.

A Table contains columns, each of which have a unique name, are strongly and consistently typed, and contains rows of data conforming to the column's type. Each column in a Table must have the same number of rows, though not every row must contain data; null-values are used to indicate missing values in the dataset.

## Key Characteristics

- The schema of a Table is immutable after creation
- Column names and data types cannot be changed after the Table has been created
- Columns cannot be added or deleted after creation
- A View can be used to select an arbitrary set of columns from the Table

## Column Types

Each column has a unique name and a single data type, one of:

- `float`
- `integer`
- `boolean`
- `date`
- `datetime`
- `string`

## Schema Construction

A Table schema is fixed at construction, either by:

1. Explicitly passing a schema dictionary to the `Client::table` method
2. Passing data from which the schema is inferred (if CSV or JSON format) or inherited (if Arrow)

## Type Inference

When passing CSV or JSON data to the `Client::table` constructor, the type of each column is inferred automatically. In some cases, the inference algorithm may not return exactly what you'd like. For example:

- A column may be interpreted as a `datetime` when you intended it to be a `string`
- A column may have no values at all (yet), as it will be updated with values from a real-time data source later on

In these cases, create a `table()` with a schema.

Once the Table has been created, further `Table::update` calls will perform limited type coercion based on the schema. While coercion works similarly to inference, in that input data may be parsed based on the expected column type, `Table::update` will not change the column's type further. For example, a number literal `1234` would be inferred as an `"integer"`, but in the context of a `Table::update` call on a known `"string"` column, this will be parsed as the string `"1234"`.

### date and datetime Inference

Various string representations of date and datetime format columns can be inferred as well coerced from strings if they match one of Perspective's internal known datetime parsing formats, for example ISO 8601 (which is also the format Perspective will output these types for CSV).

---

## Sub-sections

1. [Schema and Column Types](./Table-Schema-and-Column-Types.md)
2. [Loading Data](./Table-Loading-Data.md)
3. [Construct an Empty Table from a Schema](./Table-Empty-Table.md)
4. [Index and Limit Options](./Table-Index-and-Limit.md)
5. [Update and Remove Streaming Methods](./Table-Update-Remove.md)
6. [Clear and Replace Start-over Methods](./Table-Clear-Replace.md)