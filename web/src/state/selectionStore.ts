// Fase 15.1, T12: store global de seleção (design.md "Components" -> `SelectionStore`; master
// prompt §14/§17/§33). Independente de espaço e de câmera — selecionar nunca chama nada do
// `ViewStore`, e substitui os dois `Selection` locais de
// `web/src/components/WorldMapView.tsx:14,23` / `CityView.tsx:18,29`.
import type { EntityRef, SpaceId } from "../map-engine/types";

function sameEntity(a: EntityRef, b: EntityRef): boolean {
  return a.kind === b.kind && a.id === b.id;
}

export class SelectionStore {
  private selected: EntityRef | null = null;
  private pinned = false;
  private readonly listeners = new Set<() => void>();

  select(ref: EntityRef): void {
    this.selected = ref;
    this.pinned = false;
    this.notify();
  }

  /** Sempre limpa, mesmo com `pin` ativo — pin é sinal pra quem decide auto-clear, não um bloqueio do próprio `clear()`. */
  clear(): void {
    if (this.selected === null && !this.pinned) {
      return;
    }
    this.selected = null;
    this.pinned = false;
    this.notify();
  }

  current(): EntityRef | null {
    return this.selected;
  }

  pin(on: boolean): void {
    this.pinned = on;
  }

  isPinned(): boolean {
    return this.pinned;
  }

  /**
   * Reconciliação com o estado do mundo — chamada tanto ao trocar de espaço quanto a cada
   * delta do `SimulationStore`: se a entidade selecionada ainda existir em `entitiesInSpace`,
   * a seleção sobrevive (com `space` atualizado); senão, limpa. O mesmo método cobre "trocou de
   * espaço e a entidade não está lá" e "a entidade morreu/desmaterializou no espaço atual" —
   * são o mesmo caso visto do ponto de vista da seleção: sumiu da lista atual.
   */
  syncWithSpace(space: SpaceId, entitiesInSpace: EntityRef[]): void {
    if (!this.selected) {
      return;
    }
    const stillExists = entitiesInSpace.some((e) => sameEntity(e, this.selected!));
    if (stillExists) {
      this.selected = { ...this.selected, space };
      this.notify();
    } else {
      this.clear();
    }
  }

  subscribe(listener: () => void): () => void {
    this.listeners.add(listener);
    return () => this.listeners.delete(listener);
  }

  private notify(): void {
    for (const listener of this.listeners) {
      listener();
    }
  }
}
