import { useSyncExternalStore } from "react";
import { followStore } from "../state/followStore";

export interface FollowButtonProps {
  entityId: string;
}

/**
 * Botão Follow reusável (doc#128) — plugado em Agent/Household/Settlement View. Lê/escreve o
 * `followStore` singleton (T22), nunca o fixture.
 */
export function FollowButton({ entityId }: FollowButtonProps) {
  const followed = useSyncExternalStore(
    (listener) => followStore.subscribe(listener),
    () => followStore.isFollowed(entityId),
  );

  return (
    <button type="button" data-testid="follow-button" aria-pressed={followed} onClick={() => followStore.toggleFollow(entityId)}>
      {followed ? "Following" : "Follow"}
    </button>
  );
}
