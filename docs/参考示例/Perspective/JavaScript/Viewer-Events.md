# Listening for Events

The `<perspective-viewer>` Custom Element fires all the same HTML Events that standard DOM `HTMLElement` objects fire, in addition to a few custom `CustomEvent`s which relate to UI updates including those initiated through user interaction.

---

## Update Events

Whenever a `<perspective-viewer>`'s underlying `table()` is changed via the `load()` or `update()` methods, a `perspective-view-update` DOM event is fired.

Similarly, `view()` updates instigated either through the Attribute API or through user interaction will fire a `perspective-config-update` event.

```javascript
elem.addEventListener("perspective-config-update", function (event) {
  var config = elem.save();
  console.log("The view() config has changed to " + JSON.stringify(config));
});
```

---

## Click Events

Whenever a `<perspective-viewer>`'s grid or chart is clicked, a `perspective-click` DOM event is fired containing a `detail` object with:

- `config` — an array of filters that can be applied
- `column_names` — an array of matching columns
- `row` — the associated row data

### Using Click Events

The `config` object contains an array of filters that can be applied to a `<perspective-viewer>` through the use of `restore()`, updating it to show the filtered subset of data.

```javascript
elem.addEventListener("perspective-click", function (event) {
  var config = event.detail.config;
  elem.restore(config);
});
```

---

## Event Types Summary

| Event | When Fired | Detail Contents |
|-------|------------|-----------------|
| `perspective-view-update` | When `table()` is changed via `load()` or `update()` | None |
| `perspective-config-update` | When `view()` config changes (API or user interaction) | None |
| `perspective-click` | When grid or chart is clicked | `config`, `column_names`, `row` |