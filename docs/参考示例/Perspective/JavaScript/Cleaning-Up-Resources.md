# Cleaning Up Resources

Unlike standard JavaScript objects, Perspective objects such as `table()` and `view()` store their associated data in the WebAssembly heap. Because of this, as well as the current lack of a hook into the JavaScript runtime's garbage collector from WebAssembly, the memory allocated to these Perspective objects does not automatically get cleaned up when the object falls out of scope.

## Why Manual Cleanup is Required

In order to prevent memory leaks and reclaim the memory associated with a Perspective `table()` or `view()`, you **must** call the `delete()` method.

## Deleting a View

```javascript
await view.delete();
```

## Deleting a Table

```javascript
// This method will throw an exception if there are still `view()`s depending
// on this `table()`!
await table.delete();
```

> **Important:** You must delete all Views that depend on a Table before deleting the Table itself.

## Deleting a Perspective Viewer

Similarly, `<perspective-viewer>` Custom Elements do not delete the memory allocated for the UI when they are removed from the DOM.

```javascript
await viewer.delete();
```

---

## Cleanup Order

When cleaning up resources, follow this order:

1. Delete all `View`s that depend on a `Table`
2. Delete the `Table`
3. Delete any `<perspective-viewer>` elements that were using the `Table`

---

## Summary

| Object | Method | Note |
|--------|--------|------|
| `View` | `await view.delete()` | Always delete Views before Tables |
| `Table` | `await table.delete()` | Throws exception if Views still exist |
| `<perspective-viewer>` | `await viewer.delete()` | Required when removing from DOM |