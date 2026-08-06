import { createRoot } from "react-dom/client";
import { App } from "./App";
import "./styles/global.css";

// Sem StrictMode: seu double-invoke de effects em dev abre 2 WebSockets quase juntos no mount
// (useRealtimeSnapshot) — o segundo fecha o primeiro ainda em handshake, e isso derruba o proxy
// de WS do Vite ("ws proxy socket error: write ECONNABORTED"), não só um warning cosmético.
createRoot(document.getElementById("root")!).render(<App />);
