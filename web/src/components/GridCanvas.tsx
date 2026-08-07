import { useEffect, useRef } from "react";
import { maxSafeZoom } from "../gridFit";

export interface GridMarker {
  id: string;
  x: number;
  y: number;
  color: string;
  /** raio do dot em modo zoom-out (LOD); o token (zoom-in) usa um raio fixo maior. */
  dotRadius?: number;
}

export interface GridCanvasProps {
  width: number;
  height: number;
  /** Cor de uma célula do grid (terreno/bioma); ausente = célula neutra (fundo). */
  cellColor?: (x: number, y: number) => string | undefined;
  /** Pontos de overlay simples (ex.: rios) — desenhados como um ponto pequeno sobre a célula. */
  overlayPoints?: { x: number; y: number; color: string }[];
  markers: GridMarker[];
  /** Pixels por célula. */
  zoom: number;
  onZoomChange?: (zoom: number) => void;
  /** Zoom no ou acima do qual um marcador vira "token" (círculo maior com anel) em vez de dot. */
  lodTokenThreshold?: number;
  onMarkerClick?: (id: string) => void;
  onCellClick?: (x: number, y: number) => void;
  readOnly?: boolean;
  minZoom?: number;
  maxZoom?: number;
  /** UX pass 3: o wrap ocupa 100% do container pai (posicionado absoluto) em vez do tamanho
   * natural do canvas — usado pelas telas em tela cheia (mapa-múndi/cidade). */
  fillContainer?: boolean;
}

const DEFAULT_LOD_THRESHOLD = 18;
const DEFAULT_MIN_ZOOM = 4;

/// T11 (fase 15, UX pass 2): grid 2D genérico reusado pelo mapa-múndi, cidade e editor de "criar
/// mundo" — não sabe o que os marcadores/cores significam, só desenha células + marcadores com
/// LOD dot↔token por zoom e faz hit-test de clique. Sem lib de canvas/jogo, canvas 2D puro.
export function GridCanvas({
  width,
  height,
  cellColor,
  overlayPoints = [],
  markers,
  zoom,
  onZoomChange,
  lodTokenThreshold = DEFAULT_LOD_THRESHOLD,
  onMarkerClick,
  onCellClick,
  readOnly = false,
  minZoom = DEFAULT_MIN_ZOOM,
  maxZoom = maxSafeZoom(width, height, DEFAULT_MIN_ZOOM),
  fillContainer = false,
}: GridCanvasProps) {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const isToken = zoom >= lodTokenThreshold;

  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    canvas.width = width * zoom;
    canvas.height = height * zoom;

    const ctx = canvas.getContext("2d");
    if (!ctx) return;

    ctx.fillStyle = "#1a1f2c";
    ctx.fillRect(0, 0, canvas.width, canvas.height);

    if (cellColor) {
      for (let y = 0; y < height; y++) {
        for (let x = 0; x < width; x++) {
          const color = cellColor(x, y);
          if (!color) continue;
          ctx.fillStyle = color;
          ctx.fillRect(x * zoom, y * zoom, zoom, zoom);
        }
      }
    }

    if (zoom >= 10) {
      ctx.strokeStyle = "rgba(255,255,255,0.05)";
      ctx.lineWidth = 1;
      for (let x = 0; x <= width; x++) {
        ctx.beginPath();
        ctx.moveTo(x * zoom, 0);
        ctx.lineTo(x * zoom, height * zoom);
        ctx.stroke();
      }
      for (let y = 0; y <= height; y++) {
        ctx.beginPath();
        ctx.moveTo(0, y * zoom);
        ctx.lineTo(width * zoom, y * zoom);
        ctx.stroke();
      }
    }

    for (const p of overlayPoints) {
      ctx.fillStyle = p.color;
      ctx.beginPath();
      ctx.arc((p.x + 0.5) * zoom, (p.y + 0.5) * zoom, Math.max(1.5, zoom * 0.12), 0, Math.PI * 2);
      ctx.fill();
    }

    for (const m of markers) {
      const cx = (m.x + 0.5) * zoom;
      const cy = (m.y + 0.5) * zoom;
      if (isToken) {
        const r = Math.max(4, zoom * 0.35);
        ctx.beginPath();
        ctx.arc(cx, cy, r, 0, Math.PI * 2);
        ctx.fillStyle = m.color;
        ctx.fill();
        ctx.lineWidth = Math.max(1, r * 0.18);
        ctx.strokeStyle = "#e8e6df";
        ctx.stroke();
      } else {
        const r = m.dotRadius ?? Math.max(1.5, zoom * 0.15);
        ctx.beginPath();
        ctx.arc(cx, cy, r, 0, Math.PI * 2);
        ctx.fillStyle = m.color;
        ctx.shadowColor = m.color;
        ctx.shadowBlur = r * 2;
        ctx.fill();
        ctx.shadowBlur = 0;
      }
    }
  }, [width, height, zoom, markers, overlayPoints, cellColor, isToken]);

  function handleClick(e: React.MouseEvent<HTMLCanvasElement>) {
    if (readOnly) return;
    const canvas = canvasRef.current;
    if (!canvas) return;
    const rect = canvas.getBoundingClientRect();
    const clickX = ((e.clientX - rect.left) / rect.width) * canvas.width;
    const clickY = ((e.clientY - rect.top) / rect.height) * canvas.height;

    const hitRadius = isToken ? Math.max(4, zoom * 0.35) : Math.max(6, zoom * 0.4);
    const hit = markers.find((m) => {
      const dx = (m.x + 0.5) * zoom - clickX;
      const dy = (m.y + 0.5) * zoom - clickY;
      return Math.sqrt(dx * dx + dy * dy) <= hitRadius;
    });
    if (hit) {
      onMarkerClick?.(hit.id);
      return;
    }

    const cellX = Math.floor(clickX / zoom);
    const cellY = Math.floor(clickY / zoom);
    if (cellX >= 0 && cellX < width && cellY >= 0 && cellY < height) {
      onCellClick?.(cellX, cellY);
    }
  }

  return (
    <div className={fillContainer ? "grid-canvas-wrap grid-canvas-wrap-fill" : "grid-canvas-wrap"}>
      {onZoomChange && (
        <div className="grid-canvas-zoom">
          <button
            type="button"
            aria-label="zoom-out"
            onClick={() => onZoomChange(Math.max(minZoom, zoom - 4))}
          >
            −
          </button>
          <button
            type="button"
            aria-label="zoom-in"
            onClick={() => onZoomChange(Math.min(maxZoom, zoom + 4))}
          >
            +
          </button>
        </div>
      )}
      <canvas
        ref={canvasRef}
        data-testid="grid-canvas"
        onClick={handleClick}
        style={{ cursor: readOnly ? "default" : "pointer", maxWidth: "100%" }}
      />
    </div>
  );
}
