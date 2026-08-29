import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { EntryRoot } from "./entry/EntryRoot";
import "./styles/tokens.css";

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <EntryRoot />
  </StrictMode>,
);
