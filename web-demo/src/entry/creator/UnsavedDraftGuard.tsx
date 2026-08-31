/** Doc §5/§41 — shown when leaving the creator with unsaved changes. */
export function UnsavedDraftGuard({
  onSave,
  onDiscard,
  onCancel,
}: {
  onSave: () => void;
  onDiscard: () => void;
  onCancel: () => void;
}) {
  return (
    <div data-testid="unsaved-draft-guard-backdrop">
      <div data-testid="unsaved-draft-guard" role="dialog" aria-label="Unsaved changes">
        <p>Save this world draft before leaving?</p>
        <button type="button" data-testid="guard-save" onClick={onSave}>
          Save Draft
        </button>
        <button type="button" data-testid="guard-discard" onClick={onDiscard}>
          Discard
        </button>
        <button type="button" data-testid="guard-cancel" onClick={onCancel}>
          Cancel
        </button>
      </div>
    </div>
  );
}
