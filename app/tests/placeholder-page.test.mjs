import assert from "node:assert/strict";
import test from "node:test";
import React from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { createServer } from "vite";

test("placeholder page renders the requested title and pending state", async () => {
  const server = await createServer({
    root: process.cwd(),
    appType: "custom",
    server: { middlewareMode: true },
  });

  try {
    const { PlaceholderPage } = await server.ssrLoadModule(
      "/src/pages/PlaceholderPage.tsx",
    );
    const html = renderToStaticMarkup(
      React.createElement(PlaceholderPage, { title: "设备管理" }),
    );

    assert.match(html, /设备管理/);
    assert.match(html, /页面待复刻/);
  } finally {
    await server.close();
  }
});
