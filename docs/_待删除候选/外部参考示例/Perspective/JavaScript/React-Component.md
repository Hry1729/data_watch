# React Component

We provide a React wrapper to prevent common issues and mistakes associated with using the perspective-viewer web component in the context of React.

> **Before trying this example**, please take a look at how to bootstrap perspective in [Importing with or without a Bundler](./Importing%20with%20or%20without%20a%20Bundler.md).

---

## PerspectiveViewer

A simple example using the `PerspectiveViewer` component:

```jsx
import React, { useCallback, useEffect, useRef } from "react";
import {
  PerspectiveViewer,
} from "@perspective-dev/react";
import perspective from "@perspective-dev/client";

function App() {
  const worker = useRef(null);

  useEffect(() => {
    (async () => {
      worker.current = await perspective.worker();
      const resp = await fetch("data.arrow");
      const arrow = await resp.arrayBuffer();
      await worker.current.table(arrow, { name: "my_table" });
    })();
  }, []);

  return (
    <PerspectiveViewer
      client={worker.current}
      config={{group_by: ["State"], columns: ["Sales"]}}
    />
  );
}
```

---

## PerspectiveWorkspace

For multi-viewer layouts, use `PerspectiveWorkspace`:

```jsx
import { PerspectiveWorkspace } from "@perspective-dev/react";

const WORKSPACE_CONFIG = // ...

function Dashboard() {
  return (
    <PerspectiveWorkspace
      client={perspective.worker()}
      config={WORKSPACE_CONFIG} />
  );
}
```

---

## Important Notes

- You must still initialize Perspective's WebAssembly as per your bundler setup
- The React component wraps the `<perspective-viewer>` web component to handle React-specific lifecycle and ref management
- Both `PerspectiveViewer` and `PerspectiveWorkspace` accept a `client` prop and a `config` prop