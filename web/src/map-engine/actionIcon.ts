// Fase 15.1, T8 (LWV-02): desenho canvas do badge de ação — nunca uma imagem cacheada (a versão
// anterior baixava a ação pro SVG do pawn, que ganhava um `<style>` com `@keyframes` animando um
// `<img>` redesenhado a cada frame do canvas; suspeito real da travada relatada pelo usuário, já
// que decodificar uma imagem SVG animada em todo `drawImage` é caro). Aqui é só matemática +
// `ctx.arc/fillRect` por frame — sem imagem, sem decode, custo desprezível mesmo a 60fps.
import type { ActionIcon } from "./actionVisuals";

const BADGE_FILL = "#171b20";
const BADGE_ACCENT = "#f0c96a";

/** Desenha o badge (círculo + ícone) centrado em `(cx, cy)`; `opacity` só varia pra sleep. */
export function drawActionIcon(
  ctx: CanvasRenderingContext2D,
  cx: number,
  cy: number,
  radius: number,
  icon: ActionIcon,
  opacity: number,
): void {
  ctx.save();
  ctx.globalAlpha = opacity;
  ctx.fillStyle = BADGE_FILL;
  ctx.strokeStyle = BADGE_ACCENT;
  ctx.lineWidth = Math.max(1, radius * 0.16);
  ctx.beginPath();
  ctx.arc(cx, cy, radius, 0, Math.PI * 2);
  ctx.fill();
  ctx.stroke();

  const s = radius * 0.62;
  ctx.fillStyle = BADGE_ACCENT;
  ctx.strokeStyle = BADGE_ACCENT;

  switch (icon) {
    case "moon": {
      ctx.beginPath();
      ctx.arc(cx - s * 0.08, cy, s * 0.62, 0, Math.PI * 2);
      ctx.fill();
      ctx.fillStyle = BADGE_FILL;
      ctx.beginPath();
      ctx.arc(cx + s * 0.28, cy - s * 0.08, s * 0.54, 0, Math.PI * 2);
      ctx.fill();
      break;
    }
    case "apple": {
      ctx.beginPath();
      ctx.arc(cx, cy + s * 0.08, s * 0.55, 0, Math.PI * 2);
      ctx.fill();
      ctx.lineWidth = Math.max(1, s * 0.14);
      ctx.beginPath();
      ctx.moveTo(cx, cy - s * 0.5);
      ctx.lineTo(cx + s * 0.12, cy - s * 0.82);
      ctx.stroke();
      break;
    }
    case "tool": {
      ctx.save();
      ctx.translate(cx, cy);
      ctx.rotate(Math.PI / 4);
      ctx.fillRect(-s * 0.13, -s * 0.62, s * 0.26, s * 1.24);
      ctx.fillRect(-s * 0.34, -s * 0.62, s * 0.68, s * 0.3);
      ctx.restore();
      break;
    }
    case "chat": {
      ctx.beginPath();
      ctx.ellipse(cx, cy - s * 0.08, s * 0.6, s * 0.42, 0, 0, Math.PI * 2);
      ctx.fill();
      ctx.beginPath();
      ctx.moveTo(cx - s * 0.12, cy + s * 0.28);
      ctx.lineTo(cx - s * 0.3, cy + s * 0.58);
      ctx.lineTo(cx + s * 0.12, cy + s * 0.3);
      ctx.closePath();
      ctx.fill();
      break;
    }
    case "coin": {
      ctx.beginPath();
      ctx.arc(cx, cy, s * 0.58, 0, Math.PI * 2);
      ctx.fill();
      ctx.fillStyle = BADGE_FILL;
      ctx.font = `bold ${Math.max(6, s)}px sans-serif`;
      ctx.textAlign = "center";
      ctx.textBaseline = "middle";
      ctx.fillText("$", cx, cy + radius * 0.05);
      break;
    }
    case "waves": {
      ctx.lineWidth = Math.max(1, s * 0.18);
      for (const dy of [-0.3, 0, 0.3]) {
        ctx.beginPath();
        ctx.moveTo(cx - s * 0.5, cy + s * dy);
        ctx.quadraticCurveTo(cx - s * 0.15, cy + s * (dy - 0.2), cx + s * 0.2, cy + s * dy);
        ctx.quadraticCurveTo(cx + s * 0.5, cy + s * (dy + 0.2), cx + s * 0.68, cy + s * dy);
        ctx.stroke();
      }
      break;
    }
    case "question": {
      ctx.font = `bold ${Math.max(6, s * 1.05)}px sans-serif`;
      ctx.textAlign = "center";
      ctx.textBaseline = "middle";
      ctx.fillText("?", cx, cy + radius * 0.05);
      break;
    }
  }
  ctx.restore();
}
