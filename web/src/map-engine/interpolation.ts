// Fase 15.1, T7: buffer de interpolação puramente visual (design.md "Components" ->
// `InterpolationBuffer`; master prompt §5/§21). A posição autoritativa nunca é escrita por
// interpolação — só `observe` a atualiza, e `authoritativePositionOf` sempre devolve o último
// valor observado, mesmo com a animação visual em curso.
import type { Vec2 } from "./types";

interface EntityAnimation {
  from: Vec2;
  to: Vec2;
  startedAt: number;
  durationMs: number;
  lastObserveAt: number;
  authoritative: Vec2;
}

function lerp(from: Vec2, to: Vec2, t: number): Vec2 {
  return { x: from.x + (to.x - from.x) * t, y: from.y + (to.y - from.y) * t };
}

export class InterpolationBuffer {
  private readonly byEntity = new Map<string, EntityAnimation>();

  /**
   * Registra a nova posição autoritativa. Se já havia uma animação em curso para esta
   * entidade, ela é SUBSTITUÍDA a partir da posição visual corrente — nunca enfileirada
   * (VTT2-14). A duração da próxima animação vem do intervalo real entre este `observe` e o
   * anterior, nunca de uma constante fixa (VTT2-15/§21: a 8x o intervalo encolhe, e uma
   * constante produziria exatamente o atraso acumulado que o master prompt proíbe).
   */
  observe(entityId: string, authoritative: Vec2, atMs: number): void {
    const previous = this.byEntity.get(entityId);

    if (!previous) {
      // primeira aparição: não há de onde interpolar, a entidade só "existe" ali.
      this.byEntity.set(entityId, {
        from: authoritative,
        to: authoritative,
        startedAt: atMs,
        durationMs: 0,
        lastObserveAt: atMs,
        authoritative,
      });
      return;
    }

    const from = this.visualPositionAt(previous, atMs);
    const interval = atMs - previous.lastObserveAt;
    const durationMs = interval > 0 ? interval : 0;

    this.byEntity.set(entityId, {
      from,
      to: authoritative,
      startedAt: atMs,
      durationMs,
      lastObserveAt: atMs,
      authoritative,
    });
  }

  private visualPositionAt(animation: EntityAnimation, nowMs: number): Vec2 {
    if (animation.durationMs <= 0) {
      return animation.to;
    }
    const elapsed = nowMs - animation.startedAt;
    const t = Math.min(Math.max(elapsed / animation.durationMs, 0), 1);
    return lerp(animation.from, animation.to, t);
  }

  /** Posição visual do frame — pode estar em trânsito entre `from` e `to`. */
  visualPositionOf(entityId: string, nowMs: number): Vec2 {
    const animation = this.byEntity.get(entityId);
    if (!animation) {
      throw new Error(`no observed position for entity "${entityId}"`);
    }
    return this.visualPositionAt(animation, nowMs);
  }

  /** Posição real do motor — é esta que hit-test/inspector/seleção consultam (VTT2-13). */
  authoritativePositionOf(entityId: string): Vec2 {
    const animation = this.byEntity.get(entityId);
    if (!animation) {
      throw new Error(`no observed position for entity "${entityId}"`);
    }
    return animation.authoritative;
  }
}
