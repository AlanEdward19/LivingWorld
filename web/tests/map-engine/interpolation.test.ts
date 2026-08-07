import { describe, expect, it } from "vitest";
import { InterpolationBuffer } from "../../src/map-engine/interpolation";

describe("InterpolationBuffer", () => {
  it("snaps to the authoritative position on the very first observe (nothing to interpolate from)", () => {
    const buffer = new InterpolationBuffer();
    buffer.observe("npc-1", { x: 5, y: 5 }, 0);

    expect(buffer.visualPositionOf("npc-1", 0)).toEqual({ x: 5, y: 5 });
  });

  it("transitions smoothly from `from` to `to` over the observed interval and stops exactly at `to`", () => {
    const buffer = new InterpolationBuffer();
    buffer.observe("npc-1", { x: 0, y: 0 }, 1000);
    buffer.observe("npc-1", { x: 10, y: 0 }, 1200); // interval = 200ms

    expect(buffer.visualPositionOf("npc-1", 1200)).toEqual({ x: 0, y: 0 });
    expect(buffer.visualPositionOf("npc-1", 1300)).toEqual({ x: 5, y: 0 });
    expect(buffer.visualPositionOf("npc-1", 1400)).toEqual({ x: 10, y: 0 });
  });

  it("holds at the last target with no extrapolation once the animation has finished", () => {
    const buffer = new InterpolationBuffer();
    buffer.observe("npc-1", { x: 0, y: 0 }, 0);
    buffer.observe("npc-1", { x: 10, y: 0 }, 200);

    expect(buffer.visualPositionOf("npc-1", 2000)).toEqual({ x: 10, y: 0 });
  });

  it("authoritativePositionOf always returns the latest observed state, never the interpolated one", () => {
    const buffer = new InterpolationBuffer();
    buffer.observe("npc-1", { x: 0, y: 0 }, 1000);
    buffer.observe("npc-1", { x: 10, y: 0 }, 1200);

    // meio da animação: visual está em (5,0), autoritativa já é (10,0)
    expect(buffer.visualPositionOf("npc-1", 1300)).toEqual({ x: 5, y: 0 });
    expect(buffer.authoritativePositionOf("npc-1")).toEqual({ x: 10, y: 0 });
  });

  it("replaces the target on a mid-flight observe instead of queueing it", () => {
    const buffer = new InterpolationBuffer();
    buffer.observe("npc-1", { x: 0, y: 0 }, 1000);
    buffer.observe("npc-1", { x: 100, y: 0 }, 1200); // animação longa em curso

    // interrompe no meio (t=1250, 25% do caminho) com um novo alvo
    buffer.observe("npc-1", { x: 10, y: 0 }, 1250);

    expect(buffer.authoritativePositionOf("npc-1")).toEqual({ x: 10, y: 0 });
    // a posição visual nunca deve "pular" para além do novo alvo nem somar os dois trechos
    expect(buffer.visualPositionOf("npc-1", 2000)).toEqual({ x: 10, y: 0 });
  });

  it("collapses a burst of 5 rapid observes to the 5th target, not the sum of the segments", () => {
    const buffer = new InterpolationBuffer();
    buffer.observe("npc-1", { x: 0, y: 0 }, 0);
    buffer.observe("npc-1", { x: 1, y: 0 }, 10);
    buffer.observe("npc-1", { x: 2, y: 0 }, 20);
    buffer.observe("npc-1", { x: 3, y: 0 }, 30);
    buffer.observe("npc-1", { x: 4, y: 0 }, 40);
    buffer.observe("npc-1", { x: 5, y: 0 }, 50);

    expect(buffer.authoritativePositionOf("npc-1")).toEqual({ x: 5, y: 0 });
    expect(buffer.visualPositionOf("npc-1", 1000)).toEqual({ x: 5, y: 0 });
  });

  it("derives the animation duration from the measured interval, not a fixed constant", () => {
    const slow = new InterpolationBuffer();
    slow.observe("npc-1", { x: 0, y: 0 }, 0);
    slow.observe("npc-1", { x: 10, y: 0 }, 1000); // intervalo de 1000ms

    const fast = new InterpolationBuffer();
    fast.observe("npc-1", { x: 0, y: 0 }, 0);
    fast.observe("npc-1", { x: 10, y: 0 }, 200); // intervalo de 200ms

    // ambos devem estar na metade do caminho na metade proporcional do seu próprio intervalo
    // (a animação começa no instante do 2º observe, não em 0 — daí 1000+500 e 200+100)
    expect(slow.visualPositionOf("npc-1", 1500)).toEqual({ x: 5, y: 0 });
    expect(fast.visualPositionOf("npc-1", 300)).toEqual({ x: 5, y: 0 });
  });

  it("throws for an entity that was never observed", () => {
    const buffer = new InterpolationBuffer();

    expect(() => buffer.visualPositionOf("ghost", 0)).toThrow();
    expect(() => buffer.authoritativePositionOf("ghost")).toThrow();
  });
});
