# Data Architecture

Application developers can choose from Client (WebAssembly), Server (Python/Node) or Client/Server Replicated designs to bind data, and a web application can use one or a mix of these designs as needed. By serializing to Apache Arrow, tables are duplicated and synchronized across runtimes efficiently.

Perspective is a multi-language platform. The examples in this section use Python and JavaScript as an example, but the same general principles apply to any Client / Server combination.

## Architecture Diagram Overview

A typical Perspective architecture consists of:

- **Python Server**
  - PerspectiveManager - Thread 1
  - PerspectiveManager - Thread 2

- **Browser**
  - WebWorker 1
  - WebWorker 2
  - `<html>` content with perspective-viewer elements

## Data Flow Examples

```
table(df)
view({group_by: ["State"]})
view({group_by: ["City"]})

table(...)
table(arrow)
view({group_by: ["State"], split_by: ["Segment"]})

table(csv)
view({group_by: ["Category"], filter: [["State","==","Texas"]]})
view({group_by: ["Sub-Category"]})

table(json)
view()
```

## Perspective Viewer Examples

```html
<perspective-viewer view="heatmap" row-pivots='["State"]' column-pivots='["Segment"]'>

<perspective-viewer view="Y Bar" row-pivots='["Category"]' filters='[["State","==","Texas"]]'>

<perspective-viewer view="xy_scatter" row-pivots='["Sub-Category"]'>

<perspective-viewer view="grid">

<perspective-viewer view="heatmap" row-pivots='["State"]' column-pivots='["Segment"]'>
```

## Sub-sections

1. [Client-only](./Client-only.md) - For static datasets and server-less applications
2. [Client/Server replicated](./Client-Server-replicated.md) - For medium-sized, real-time synchronized datasets
3. [Server only](./Server-only.md) - For extremely large datasets