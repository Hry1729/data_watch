# Python Installation

`perspective-python` contains full bindings to the Perspective API, a JupyterLab widget, and WebSocket handlers for several webserver libraries that allow you to host Perspective using server-side Python.

---

## PyPI

`perspective-python` can be installed from PyPI via `pip`:

```bash
pip install perspective-python
```

That's it! If JupyterLab is installed in this Python environment, you'll also get the `perspective.widget.PerspectiveWidget` class when you import `perspective` in a Jupyter Lab kernel.

---

## Requirements

- Supported Python versions and platforms
- For Windows: ensure you have a compatible Python version and architecture

### Pre-built Wheels

Pre-built wheels are available for:
- Most supported Python versions
- Major platforms (Linux, macOS, Windows)

Pre-built wheels eliminate the need for a C++ compiler in most cases.