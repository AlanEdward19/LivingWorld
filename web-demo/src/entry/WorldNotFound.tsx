/** Doc §103-104 — invalid `/worlds/:id` or `/create/:draftId`, no silent redirect. */
export function NotFoundScreen({
  kind,
  onNavigate,
}: {
  kind: "world" | "draft";
  onNavigate: (path: string) => void;
}) {
  return (
    <div data-testid="not-found-screen">
      <p>{kind === "world" ? "World not found." : "This world draft could not be found."}</p>
      {kind === "world" ? (
        <button type="button" onClick={() => onNavigate("/worlds")}>
          Browse Worlds
        </button>
      ) : (
        <button type="button" onClick={() => onNavigate("/create")}>
          Create New World
        </button>
      )}
      <button type="button" onClick={() => onNavigate("/")}>
        Main Menu
      </button>
    </div>
  );
}
