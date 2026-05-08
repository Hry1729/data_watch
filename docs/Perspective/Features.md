# Features

Perspective provides a comprehensive set of features for interactive data analytics and visualization:

## User Interface

- **Framework-agnostic Custom Element**: Packaged as a Web Component that works with any JavaScript framework
- **Data Grid**: High-performance tabular data display
- **10+ Chart Types**: Including:
  - Line charts
  - Bar charts
  - Area charts
  - Scatter plots
  - Heatmaps
  - Treemaps
  - Sunburst charts
  - Candlestick charts
  - And more

## Data Connectivity

- **In-browser Data Model**: Via WebAssembly for client-side processing
- **Remote Data Model**: Via WebSocket with integration in Python, Node.js and Rust
- **Data Model API**: For pluggable engines, enabling Perspective's UI to query external data sources like DuckDB while translating view configurations into native queries

## Performance

- **High-performance Query Engine**: Compiled to WebAssembly, Python, and Rust
- **Fast, Memory-efficient Streaming Data Model**: Built-in, written in C++
- **Apache Arrow Support**: Supports read/write/streaming for Apache Arrow
- **Columnar Expression Language**: Based on ExprTk

## JupyterLab Integration

- **JupyterLab Widget**: Interactive data analysis in notebooks
- **Python Client Library**: Full integration with Python data science ecosystem

## Project Status

- Build Passing: NPM v4.4.1, PyPI v4.4.1, Crates.io v4.4.1

The Perspective project is a member of the The OpenJS Foundation.
