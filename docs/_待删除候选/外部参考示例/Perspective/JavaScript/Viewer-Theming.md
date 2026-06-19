# Theming

Theming is supported in `perspective-viewer` and its accompanying plugins.

## Bundled Themes

A number of themes come bundled with `perspective-viewer`. You can import any of these themes directly into your app, and the `perspective-viewer`s will be themed accordingly:

### Prospective Design Themes

```javascript
// Themes based on Thought Merchants's Prospective design
import "@perspective-dev/viewer/dist/css/pro.css";
import "@perspective-dev/viewer/dist/css/pro-dark.css";
```

### Other Themes

```javascript
import "@perspective-dev/viewer/dist/css/solarized.css";
import "@perspective-dev/viewer/dist/css/solarized-dark.css";
import "@perspective-dev/viewer/dist/css/monokai.css";
import "@perspective-dev/viewer/dist/css/vaporwave.css";
```

### All Themes Bundle

```javascript
import "@perspective-dev/viewer/dist/css/themes.css";
```

---

## CDN Themes

If you choose not to bundle the themes yourself, they are available through CDN. These can be directly linked in your HTML file:

```html
<link
  rel="stylesheet"
  crossorigin="anonymous"
  href="https://cdn.jsdelivr.net/npm/@perspective-dev/viewer/dist/css/pro.css"
/>
```

> **Note:** The `crossorigin="anonymous"` attribute is required. When including a theme from a cross-origin context, this attribute may be required to allow `<perspective-viewer>` to detect the theme.

---

## Theme Auto-Detection

If theme auto-detection fails, additional themes are added to the document after `<perspective-viewer>` init, or for any other reason, you may manually inform `<perspective-viewer>` of the available theme names with the `.resetThemes()` method.

```javascript
// re-auto-detect themes
viewer.resetThemes();

// Set available themes explicitly (they still must be imported as CSS!)
viewer.resetThemes(["Pro Light", "Pro Dark"]);
```

---

## Setting the Initial Theme

`<perspective-viewer>` will default to the first loaded theme when initialized. You may override this via `.restore()` or by setting the `theme` attribute:

```html
<perspective-viewer theme="Pro Light"></perspective-viewer>
```

Or via JavaScript:

```javascript
const viewer = document.querySelector("perspective-viewer");
await viewer.restore({ theme: "Pro Dark" });
```

---

## Custom Themes

The best way to write a new theme is to fork and modify an existing theme, which are just collections of regular CSS variables (no preprocessor is required, though Perspective's own themes use one).

> **Note:** `<perspective-viewer>` is not "themed" by default and will lack icons and label text in addition to colors and fonts. Starting from an empty theme forces you to define every theme-able variable to get a functional UI.

---

## Icons and Translation

### Icons

UI icons are defined by CSS variables provided by `@perspective-dev/viewer/dist/css/icons.css`. These variables must be defined for the UI icons to work — there are no default icons without a theme.

### Translation

UI text is also defined in CSS variables provided by `@perspective-dev/viewer/dist/css/intl.css`, and has identical import requirements.

Some example definitions (automatically-translated sans-editing) can be found in the `@perspective-dev/viewer/dist/css/intl/` folder.

### Using themes.css

Importing the pre-built `themes.css` stylesheet as well as a custom theme will define Icons and Translation globally as a side-effect.

You can still customize icons in this mode with rules (of the appropriate specificity), but if you do not still remember to define these variables yourself, your theme will not work without the base `themes.css` package available.