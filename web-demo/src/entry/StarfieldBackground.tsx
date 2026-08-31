import { useMemo, type CSSProperties } from "react";

type Star = { left: number; top: number; size: number; delay: number; duration: number };
type Streak = { top: number; left: number; angle: number; length: number; delay: number; duration: number; variant: "meteor" | "comet" };

function randomStars(count: number): Star[] {
  return Array.from({ length: count }, () => ({
    left: Math.random() * 100,
    top: Math.random() * 100,
    size: Math.random() < 0.15 ? 2 : 1,
    delay: Math.random() * 6,
    duration: 3 + Math.random() * 4,
  }));
}

function randomStreaks(): Streak[] {
  return [
    { top: 8, left: 60, angle: 18, length: 140, delay: 0, duration: 9, variant: "meteor" },
    { top: 22, left: 15, angle: 22, length: 110, delay: 5, duration: 13, variant: "meteor" },
    { top: 4, left: 30, angle: 14, length: 220, delay: 3, duration: 20, variant: "comet" },
  ];
}

/**
 * Doc §11 wants a mostly-empty deep-space background (stars/dust, no "moving star field"), but
 * the user explicitly asked for stars/meteors/comets to fill the dead space around the menu —
 * kept sparse and occasional (long cycles, mostly invisible) rather than a constant swarm, and
 * fully static under `prefers-reduced-motion` (doc §15) since none of it carries information.
 */
export function StarfieldBackground() {
  const stars = useMemo(() => randomStars(90), []);
  const streaks = useMemo(() => randomStreaks(), []);

  return (
    <div data-testid="starfield" aria-hidden="true">
      {stars.map((star, i) => (
        <div
          key={i}
          className="starfield-star"
          style={{
            left: `${star.left}%`,
            top: `${star.top}%`,
            width: star.size,
            height: star.size,
            animationDelay: `${star.delay}s`,
            animationDuration: `${star.duration}s`,
          }}
        />
      ))}
      {streaks.map((streak, i) => (
        <div
          key={i}
          className={`starfield-streak starfield-streak--${streak.variant}`}
          style={
            {
              top: `${streak.top}%`,
              left: `${streak.left}%`,
              width: streak.length,
              "--streak-angle": `${streak.angle}deg`,
              animationDelay: `${streak.delay}s`,
              animationDuration: `${streak.duration}s`,
            } as CSSProperties
          }
        />
      ))}
    </div>
  );
}
