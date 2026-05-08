# Installation and Module Structure

Perspective is designed for flexibility, allowing developers to pick and choose which modules they need.

## Main Modules

### @perspective-dev/client

The data engine library, as both a browser ES6 and Node.js module.

- Provides a WebAssembly, WebWorker (browser) and Process (node.js) runtime

### @perspective-dev/viewer

A user-configurable visualization widget, bundled as a Web Component.

- This module includes the core data engine module as a dependency
- `<perspective-viewer>` by itself only implements a trivial debug renderer, which prints the currently configured `view()` as a CSV
- Plugin modules for popular JavaScript libraries, such as d3fc, are packaged separately and must be imported individually

### @perspective-dev/viewer-datagrid

A custom high-performance data-grid component based on HTML `<table>`.

### @perspective-dev/viewer-d3fc

A `<perspective-viewer>` plugin for the d3fc charting library.

## Plugin Registration

When imported after `@perspective-dev/viewer`, the plugin modules will register themselves automatically, and the renderers they export will be available in the plugin dropdown in the `<perspective-viewer>` UI.

---

## Browser Installation

Perspective's WebAssembly data engine is available via NPM in the same package as its Node.js counterpart, `@perspective-dev/client`.

The Perspective Viewer UI (which has no Node.js component) must be installed separately:

```bash
$ npm add @perspective-dev/client @perspective-dev/viewer
```

By itself, `@perspective-dev/viewer` does not provide any visualizations, only the UI framework.

**Perspective Plugins provide visualizations and must be installed separately.** All Plugins are optional - but a `<perspective-viewer>` without Plugins would be rather boring!

```bash
$ npm add @perspective-dev/viewer-d3fc @perspective-dev/viewer-datagrid @perspective-dev/viewer-openlayers
```

---

## Node.js Installation

To use Perspective from a Node.js server, simply install via NPM:

```bash
$ npm add @perspective-dev/client
```