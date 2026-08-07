// Fase 15.1, T19 (adiantado por feedback do usuário — "menu sem interações"): botão de
// Follow reusado pelos três inspectors. `useSyncExternalStore` no `ViewStore` — o botão troca
// de rótulo sozinho se o follow for cancelado por outro caminho (pan manual, `MapView.tsx`).
import { useSyncExternalStore } from "react";
import type { ViewStore } from "../../state/viewStore";
import type { EntityRef } from "../../map-engine/types";

export interface FollowButtonProps {
  entityRef: EntityRef;
  viewStore: ViewStore;
}

function sameEntity(a: EntityRef | null, b: EntityRef): boolean {
  return a !== null && a.kind === b.kind && a.id === b.id;
}

export function FollowButton({ entityRef, viewStore }: FollowButtonProps) {
  const followed = useSyncExternalStore(
    (onStoreChange) => viewStore.subscribe(onStoreChange),
    () => viewStore.followedEntity(),
  );
  const isFollowing = sameEntity(followed, entityRef);

  return (
    <button
      type="button"
      onClick={() => (isFollowing ? viewStore.stopFollow() : viewStore.startFollow(entityRef))}
    >
      {isFollowing ? "Parar de seguir" : "Seguir"}
    </button>
  );
}
