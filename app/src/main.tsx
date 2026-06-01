import React from "react";
import ReactDOM from "react-dom/client";
import "./index.css";
import App from "./App";
import { ThemeProvider } from "./contexts/ThemeContext";


ReactDOM.createRoot(document.getElementById("root") as HTMLElement).render(
  <React.StrictMode>
    <ThemeProvider defaultTheme="system" storageKey="data-watch-theme">
      <App />
    </ThemeProvider>
  </React.StrictMode>,
);
