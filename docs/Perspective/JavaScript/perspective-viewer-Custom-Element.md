# perspective-viewer Custom Element Library

`<perspective-viewer>` provides a complete graphical UI for configuring the perspective library and formatting its output to the provided visualization plugins.

Once imported and initialized in JavaScript, the `<perspective-viewer>` Web Component will be available in any standard HTML on your site.

## Simple Example

```html
<perspective-viewer id="view1"></perspective-viewer>
<script type="module">
import perspective from "@perspective-dev/client";
import "@perspective-dev/viewer";

const worker = await perspective.worker();
const table = await worker.table(data);

document.getElementById("view1").load(table);
</script>
```

---

## Attributes

`<perspective-viewer>` can be configured via HTML attributes or JavaScript properties. When set as attributes, the viewer will apply the configuration on initialization:

```html
<perspective-viewer
  columns='["Sales", "Profit"]'
  group-by='["Region"]'
  sort='[["Sales", "desc"]]'>
</perspective-viewer>
```

---

## UI Features

The viewer provides an interactive side panel with:

- **Column list** - Drag and drop columns to configure `group_by`, `split_by`, `sort`, and `filter` fields
- **New Column button** - Opens an expression editor for creating computed columns via the expression language
- **Plugin selector** - Switch between visualization plugins such as Datagrid, X/Y Line, X/Y Scatter, Treemap, Sunburst, and Heatmap
- **Theme selector** - Toggle between available themes
- **Export** - Download the current view as CSV or Arrow
- **Copy** - Copy the current view to the clipboard
- **Reset** - Restore the viewer to its default configuration

---

## Methods

| Method | Description |
|--------|-------------|
| `load(table)` | Bind a Table to the viewer |
| `restore(config)` | Apply a saved configuration object |
| `save()` | Serialize the current configuration |
| `reset(all)` | Reset configuration (pass `true` to also reset expressions) |
| `getTable()` | Get the bound Table |
| `flush()` | Wait for any pending UI updates to complete |

---

## Sub-sections

1. [Loading Data](./Viewer-Loading-Data.md)
2. [Theming](./Viewer-Theming.md)
3. [Saving and Restoring UI State](./Viewer-Saving-Restoring.md)
4. [Listening for Events](./Viewer-Events.md)
5. [Plugin Render Limits](./Viewer-Plugin-Limits.md)