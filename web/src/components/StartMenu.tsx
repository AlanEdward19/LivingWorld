import { useEffect, useRef } from "react";

export interface StartMenuProps {
  onCreateWorld: () => void;
  onContinue: () => void;
  onSettings: () => void;
}

interface Particle {
  x: number;
  y: number;
  radius: number;
  driftX: number;
  driftY: number;
  twinkleSpeed: number;
  twinklePhase: number;
}

/// UX pass (fase 15): tela inicial estilo menu de jogo (Minecraft etc.) — botões ao centro sobre
/// fundo animado. Motivo do fundo é deliberadamente atemporal (campo de partículas à deriva, não
/// iconografia de uma época): LivingWorld simula qualquer período, não só medieval.
export function StartMenu({ onCreateWorld, onContinue, onSettings }: StartMenuProps) {
  const canvasRef = useRef<HTMLCanvasElement>(null);

  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    const ctx = canvas.getContext("2d");
    if (!ctx) return;

    let width = 0;
    let height = 0;
    let particles: Particle[] = [];

    function resize() {
      width = canvas!.width = canvas!.clientWidth;
      height = canvas!.height = canvas!.clientHeight;
      const count = Math.round((width * height) / 9000);
      particles = Array.from({ length: count }, () => ({
        x: Math.random() * width,
        y: Math.random() * height,
        radius: Math.random() * 1.4 + 0.3,
        driftX: (Math.random() - 0.5) * 0.06,
        driftY: (Math.random() - 0.5) * 0.06 + 0.02,
        twinkleSpeed: Math.random() * 0.02 + 0.005,
        twinklePhase: Math.random() * Math.PI * 2,
      }));
    }

    resize();
    window.addEventListener("resize", resize);

    let frame = 0;
    let animationId: number;
    function tick() {
      frame++;
      ctx!.clearRect(0, 0, width, height);
      for (const p of particles) {
        p.x = (p.x + p.driftX + width) % width;
        p.y = (p.y + p.driftY + height) % height;
        const twinkle = 0.5 + 0.5 * Math.sin(frame * p.twinkleSpeed + p.twinklePhase);
        ctx!.beginPath();
        ctx!.arc(p.x, p.y, p.radius, 0, Math.PI * 2);
        ctx!.fillStyle = `rgba(217, 169, 79, ${0.15 + twinkle * 0.55})`;
        ctx!.fill();
      }
      animationId = requestAnimationFrame(tick);
    }
    tick();

    return () => {
      window.removeEventListener("resize", resize);
      cancelAnimationFrame(animationId);
    };
  }, []);

  return (
    <div className="start-menu" data-testid="start-menu">
      <canvas ref={canvasRef} className="start-menu-canvas" aria-hidden="true" />
      <div className="start-menu-content">
        <h1 className="start-menu-title">LivingWorld</h1>
        <p className="start-menu-subtitle">mundos simulados, através de qualquer época</p>
        <nav className="start-menu-buttons">
          <button type="button" style={{ animationDelay: "0.05s" }} onClick={onContinue}>
            Continuar
          </button>
          <button type="button" style={{ animationDelay: "0.15s" }} onClick={onCreateWorld}>
            Criar mundo
          </button>
          <button type="button" style={{ animationDelay: "0.25s" }} onClick={onSettings}>
            Configurações
          </button>
        </nav>
      </div>
    </div>
  );
}
