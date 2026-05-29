import "./overlay.css";
import { createRoot } from "react-dom/client";
import { OverlayApp } from "./OverlayApp";

const root = createRoot(document.getElementById("root")!);
root.render(<OverlayApp />);
