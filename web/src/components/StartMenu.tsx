import { useEffect, useRef, useState } from "react";

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

interface NebulaBlob {
  x: number;
  y: number;
  radius: number;
  hue: "amber" | "teal";
  driftX: number;
  driftY: number;
}

interface ShootingStar {
  x: number;
  y: number;
  vx: number;
  vy: number;
  age: number;
  life: number;
}

type ExitKind = "create" | "continue" | null;

// Posição/raio do planeta em fração do viewport — usados tanto pro desenho no canvas quanto pro
// transform-origin CSS do zoom (start-menu--exiting-create), pra a câmera "mergulhar" exatamente
// nele em vez de zoomar no centro da tela.
const PLANET_X_FRAC = 0.76;
const PLANET_Y_FRAC = 0.66;
const PLANET_RADIUS_FRAC = 0.15;
const MOON_OFFSET_X_FRAC = 0.09;
const MOON_OFFSET_Y_FRAC = -0.16;
const MOON_RADIUS_FRAC = 0.32;

const CREATE_EXIT_MS = 950;
const CONTINUE_EXIT_MS = 900;

/// UX pass (fase 15, reforma 17): tela inicial estilo menu de jogo — botões ao centro sobre fundo
/// animado com um planeta (motivo RimWorld: "criar mundo" mergulha nele, "continuar" faz um warp
/// de entrada). Planeta é puramente procedural (sem assets), permanece atemporal (LivingWorld
/// simula qualquer período) — não é uma iconografia de época específica.
export function StartMenu({ onCreateWorld, onContinue, onSettings }: StartMenuProps) {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const [exiting, setExiting] = useState<ExitKind>(null);
  const exitTimeoutRef = useRef<number | undefined>(undefined);

  useEffect(() => {
    return () => {
      if (exitTimeoutRef.current !== undefined) {
        window.clearTimeout(exitTimeoutRef.current);
      }
    };
  }, []);

  function beginExit(kind: Exclude<ExitKind, null>, after: () => void, delayMs: number) {
    if (exiting) return;
    setExiting(kind);
    exitTimeoutRef.current = window.setTimeout(after, delayMs);
  }

  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    const ctx = canvas.getContext("2d");
    if (!ctx) return;

    let width = 0;
    let height = 0;
    let particles: Particle[] = [];
    let farParticles: Particle[] = [];
    let nebulaBlobs: NebulaBlob[] = [];
    let shootingStars: ShootingStar[] = [];

    function makeParticles(count: number, radiusMax: number, speedMax: number): Particle[] {
      return Array.from({ length: count }, () => ({
        x: Math.random() * width,
        y: Math.random() * height,
        radius: Math.random() * radiusMax + 0.25,
        driftX: (Math.random() - 0.5) * speedMax,
        driftY: (Math.random() - 0.5) * speedMax + speedMax * 0.3,
        twinkleSpeed: Math.random() * 0.02 + 0.005,
        twinklePhase: Math.random() * Math.PI * 2,
      }));
    }

    function resize() {
      width = canvas!.width = canvas!.clientWidth;
      height = canvas!.height = canvas!.clientHeight;
      const count = Math.round((width * height) / 9000);
      // Duas camadas (paralaxe): fundo mais denso/fraco/lento, frente mais esparsa/brilhante/rápida.
      farParticles = makeParticles(Math.round(count * 0.9), 0.9, 0.025);
      particles = makeParticles(count, 1.4, 0.06);
      nebulaBlobs = [
        { x: width * 0.18, y: height * 0.28, radius: Math.max(width, height) * 0.32, hue: "amber", driftX: 0.006, driftY: 0.003 },
        { x: width * 0.55, y: height * 0.82, radius: Math.max(width, height) * 0.26, hue: "teal", driftX: -0.004, driftY: -0.002 },
        { x: width * 0.85, y: height * 0.12, radius: Math.max(width, height) * 0.2, hue: "teal", driftX: -0.003, driftY: 0.004 },
      ];
      shootingStars = [];
    }

    resize();
    window.addEventListener("resize", resize);

    function spawnShootingStar() {
      const fromLeft = Math.random() < 0.5;
      const y0 = Math.random() * height * 0.5;
      const speed = 6 + Math.random() * 4;
      const angle = Math.PI / 7;
      shootingStars.push({
        x: fromLeft ? -20 : width + 20,
        y: y0,
        vx: (fromLeft ? 1 : -1) * speed * Math.cos(angle),
        vy: speed * Math.sin(angle),
        age: 0,
        life: 45 + Math.random() * 20,
      });
    }

    function drawNebula() {
      for (const blob of nebulaBlobs) {
        blob.x += blob.driftX;
        blob.y += blob.driftY;
        const color = blob.hue === "amber" ? "217, 169, 79" : "74, 138, 148";
        const g = ctx!.createRadialGradient(blob.x, blob.y, 0, blob.x, blob.y, blob.radius);
        g.addColorStop(0, `rgba(${color}, 0.09)`);
        g.addColorStop(1, `rgba(${color}, 0)`);
        ctx!.fillStyle = g;
        ctx!.beginPath();
        ctx!.arc(blob.x, blob.y, blob.radius, 0, Math.PI * 2);
        ctx!.fill();
      }
    }

    function drawParticleLayer(layer: Particle[], frame: number, baseAlpha: number) {
      for (const p of layer) {
        p.x = (p.x + p.driftX + width) % width;
        p.y = (p.y + p.driftY + height) % height;
        const twinkle = 0.5 + 0.5 * Math.sin(frame * p.twinkleSpeed + p.twinklePhase);
        ctx!.beginPath();
        ctx!.arc(p.x, p.y, p.radius, 0, Math.PI * 2);
        ctx!.fillStyle = `rgba(217, 169, 79, ${baseAlpha + twinkle * 0.55})`;
        ctx!.fill();
      }
    }

    function drawShootingStars(frame: number) {
      if (Math.random() < 0.003) spawnShootingStar();
      shootingStars = shootingStars.filter((s) => s.age < s.life);
      for (const s of shootingStars) {
        s.age++;
        s.x += s.vx;
        s.y += s.vy;
        const fade = 1 - s.age / s.life;
        const tailX = s.x - s.vx * 4;
        const tailY = s.y - s.vy * 4;
        const grad = ctx!.createLinearGradient(tailX, tailY, s.x, s.y);
        grad.addColorStop(0, "rgba(240, 198, 116, 0)");
        grad.addColorStop(1, `rgba(240, 198, 116, ${fade * 0.85})`);
        ctx!.strokeStyle = grad;
        ctx!.lineWidth = 1.5;
        ctx!.beginPath();
        ctx!.moveTo(tailX, tailY);
        ctx!.lineTo(s.x, s.y);
        ctx!.stroke();
      }
      void frame;
    }

    function drawMoon() {
      const cx = width * PLANET_X_FRAC + width * MOON_OFFSET_X_FRAC;
      const cy = height * PLANET_Y_FRAC + height * MOON_OFFSET_Y_FRAC;
      const r = Math.min(width, height) * PLANET_RADIUS_FRAC * MOON_RADIUS_FRAC;

      ctx!.save();
      ctx!.beginPath();
      ctx!.arc(cx, cy, r, 0, Math.PI * 2);
      ctx!.clip();
      const sphere = ctx!.createRadialGradient(cx - r * 0.35, cy - r * 0.35, r * 0.1, cx, cy, r);
      sphere.addColorStop(0, "#9a9188");
      sphere.addColorStop(0.6, "#5c564f");
      sphere.addColorStop(1, "#26221e");
      ctx!.fillStyle = sphere;
      ctx!.fillRect(cx - r, cy - r, r * 2, r * 2);
      const craters = [
        [-0.3, -0.1, 0.22],
        [0.15, 0.35, 0.16],
        [0.35, -0.25, 0.12],
      ];
      for (const [dx, dy, cr] of craters) {
        ctx!.fillStyle = "rgba(0, 0, 0, 0.18)";
        ctx!.beginPath();
        ctx!.arc(cx + dx * r, cy + dy * r, cr * r, 0, Math.PI * 2);
        ctx!.fill();
      }
      ctx!.restore();
      ctx!.beginPath();
      ctx!.arc(cx, cy, r, 0, Math.PI * 2);
      ctx!.strokeStyle = "rgba(240, 198, 116, 0.25)";
      ctx!.lineWidth = 1;
      ctx!.stroke();
    }

    function drawPlanet(frame: number) {
      const cx = width * PLANET_X_FRAC;
      const cy = height * PLANET_Y_FRAC;
      const r = Math.min(width, height) * PLANET_RADIUS_FRAC;

      const glow = ctx!.createRadialGradient(cx, cy, r * 0.9, cx, cy, r * 1.7);
      glow.addColorStop(0, "rgba(217, 169, 79, 0.22)");
      glow.addColorStop(1, "rgba(217, 169, 79, 0)");
      ctx!.fillStyle = glow;
      ctx!.beginPath();
      ctx!.arc(cx, cy, r * 1.7, 0, Math.PI * 2);
      ctx!.fill();

      ctx!.save();
      ctx!.beginPath();
      ctx!.arc(cx, cy, r, 0, Math.PI * 2);
      ctx!.clip();

      const sphere = ctx!.createRadialGradient(cx - r * 0.35, cy - r * 0.35, r * 0.1, cx, cy, r * 1.05);
      sphere.addColorStop(0, "#5f8296");
      sphere.addColorStop(0.55, "#31485a");
      sphere.addColorStop(1, "#10161d");
      ctx!.fillStyle = sphere;
      ctx!.fillRect(cx - r, cy - r, r * 2, r * 2);

      const bandCount = 7;
      for (let i = 0; i < bandCount; i++) {
        const wobble = Math.sin(frame * 0.0025 + i * 1.7) * r * 0.06;
        const bandY = cy - r + ((i + 0.5) * (2 * r)) / bandCount + wobble;
        ctx!.fillStyle = `rgba(214, 196, 158, ${i % 2 === 0 ? 0.07 : 0.035})`;
        ctx!.fillRect(cx - r, bandY - r * 0.045, r * 2, r * 0.09);
      }

      ctx!.fillStyle = "rgba(6, 9, 14, 0.4)";
      ctx!.beginPath();
      ctx!.arc(cx + r * 0.3, cy, r, 0, Math.PI * 2);
      ctx!.fill();
      ctx!.restore();

      ctx!.beginPath();
      ctx!.arc(cx, cy, r, 0, Math.PI * 2);
      ctx!.strokeStyle = "rgba(240, 198, 116, 0.4)";
      ctx!.lineWidth = 1.5;
      ctx!.stroke();
    }

    let frame = 0;
    let animationId: number;
    function tick() {
      frame++;
      ctx!.clearRect(0, 0, width, height);
      drawNebula();
      drawParticleLayer(farParticles, frame, 0.08);
      drawParticleLayer(particles, frame, 0.15);
      drawShootingStars(frame);
      drawMoon();
      drawPlanet(frame);
      animationId = requestAnimationFrame(tick);
    }
    tick();

    return () => {
      window.removeEventListener("resize", resize);
      cancelAnimationFrame(animationId);
    };
  }, []);

  const exitClass = exiting ? ` start-menu--exiting-${exiting}` : "";

  return (
    <div className={`start-menu${exitClass}`} data-testid="start-menu">
      <canvas ref={canvasRef} className="start-menu-canvas" aria-hidden="true" />
      <div className="start-menu-warp" aria-hidden="true" />
      <div className="start-menu-content">
        <h1 className="start-menu-title">LivingWorld</h1>
        <div className="start-menu-flourish" aria-hidden="true" />
        <p className="start-menu-subtitle">mundos simulados, através de qualquer época</p>
        <nav className="start-menu-buttons">
          <button
            type="button"
            className="ui-btn ui-btn--primary ui-btn--lg"
            style={{ animationDelay: "0.05s" }}
            disabled={exiting !== null}
            onClick={() => beginExit("continue", onContinue, CONTINUE_EXIT_MS)}
          >
            Continuar
          </button>
          <button
            type="button"
            className="ui-btn ui-btn--lg"
            style={{ animationDelay: "0.15s" }}
            disabled={exiting !== null}
            onClick={() => beginExit("create", onCreateWorld, CREATE_EXIT_MS)}
          >
            Criar mundo
          </button>
          <button
            type="button"
            className="ui-btn ui-btn--ghost ui-btn--lg"
            style={{ animationDelay: "0.25s" }}
            disabled={exiting !== null}
            onClick={onSettings}
          >
            Configurações
          </button>
        </nav>
      </div>
      <p className="start-menu-loading" aria-hidden="true">
        Entrando no mundo…
      </p>
      <div className="start-menu-blackout" aria-hidden="true" />
    </div>
  );
}
