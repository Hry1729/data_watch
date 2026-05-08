# Saving and Restoring UI State

`<perspective-viewer>` is persistent, in that its entire state (sans the data itself) can be serialized or deserialized. This includes all column, filter, pivot, expressions, etc. properties, as well as datagrid style settings, config panel visibility, and more.

## Use Cases

This overloaded feature covers a range of use cases:

- Setting a `<perspective-viewer>`'s initial state after a `load()` call
- Updating a single or subset of properties, without modifying others
- Resetting some or all properties to their data-relative default
- Persisting a user's configuration to localStorage or a server

---

## Serializing and Deserializing

### save()

To retrieve the entire state as a JSON-ready JavaScript object, use the `save()` method:

```javascript
const json_token = await elem.save();
const string_token = await elem.save("string");
```

`save()` also supports a few other formats such as `"arraybuffer"` and `"string"` (base64, not JSON), which you may choose for size at the expense of easy migration/manual-editing.

### restore()

For any format, the serialized token can be restored to any `<perspective-viewer>` with a Table of identical schema, via the `restore()` method.

```javascript
await elem.restore(json_token);
await elem.restore(string_token);
```

> **Warning:** While the data for a token returned from `save()` may differ, generally its schema may not, as many other settings depend on column names and types.

### Type Matching

As `restore()` dispatches on the token's type, it is important to make sure that these types match!

```javascript
// This will error!
await elem.restore(JSON.stringify(json_token));
```

A common source of error occurs when passing a JSON-stringified token to `restore()`, which will assume base64-encoded msgpack when a string token is used.

---

## Updating Individual Properties

Using the JSON format, every facet of a `<perspective-viewer>`'s configuration can be manipulated from JavaScript using the `restore()` method.

The valid structure of properties is described via the `ViewerConfig` and embedded `ViewConfig` type declarations.

### Examples

```javascript
// Set the plugin (will also update `columns` to plugin-defaults)
await elem.restore({ plugin: "X Bar" });

// Update plugin and columns (only draws once)
await elem.restore({ plugin: "X Bar", columns: ["Sales"] });

// Open the config panel
await elem.restore({ settings: true });

// Create an expression
await elem.restore({
  columns: ['"Sales" + 100'],
  expressions: { "New Column": '"Sales" + 100' },
});

// ERROR if the column does not exist in the schema or expressions
// await elem.restore({columns: ["\"Sales\" + 100"], expressions: {}});

// Add a filter
await elem.restore({ filter: [["Sales", "<", 100]] });

// Add a sort, don't remove filter
await elem.restore({ sort: [["Profit", "desc"]] });

// Reset just filter, preserve sort
await elem.restore({ filter: undefined });

// Reset all properties to default e.g. after `load()`
await elem.reset();
```

---

## Quick Token Creation

Another effective way to quickly create a token for a desired configuration is to simply copy the token returned from `save()` after setting the view manually in the browser.

The JSON format is human-readable and should be quite easy to tweak once generated, as `save()` will return even the default settings for all properties.

### From Browser Console

```javascript
// Copy to clipboard
copy(await document.querySelector("perspective-viewer").save());
```