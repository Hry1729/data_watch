# Client-only

For static datasets, datasets provided by the user, and simple server-less and read-only web applications.

## Overview

In this design, Perspective is run as a client Browser WebAssembly library, the dataset is downloaded entirely to the client and all calculations and UI interactions are performed locally.

### Advantages

- **Interactive performance is very good**: Using WebAssembly engine for near-native runtime plus WebWorker isolation for parallel rendering within the browser
- **Responsive operations**: Operations like scrolling and creating new views are responsive
- **Large dataset support**: Perspective is not a typical browser component, and dataset sizes of 1gb+ in Apache Arrow format will load fine with good interactive performance!

### Limitations

- **Entire dataset must be downloaded to the client**
- **No horizontal scaling concern**: Since there is no concurrent state to scale, and only uses client-side computation via WebAssembly client
- **Local updates**: Updates and edits are local to the browser client and will be lost when the page is refreshed, unless otherwise persisted by your application

## Scalability

Client-only perspective can support as many concurrent users as can download the web application itself. Once the data is loaded, no server connection is needed and all operations occur in the client browser, imparting no additional runtime cost on the server beyond initial load.

## Usage

As the client-only design starts with creating a client-side Perspective Table, data can be provided by any standard web service in any Perspective compatible format (JSON, CSV or Apache Arrow).

### Architecture Diagram

```
Browser
├── WebWorker 1
└── <html>
    └── table(csv)
        └── view({group_by: ["Sub-Category"]})
            └── <perspective-viewer view="xy_scatter" row-pivots='["Sub-Category"]'>
```

### Javascript Client Example

```javascript
const worker = await perspective.worker();
const table = await worker.table(csv);
const viewer = document.createElement("perspective-viewer");
document.body.appendChild(viewer);
await viewer.load(table);
```

## When to Use

Client-only architecture is ideal for:
- Static datasets
- User-provided datasets
- Simple server-less web applications
- Read-only web applications
- When you want excellent interactive performance without server overhead