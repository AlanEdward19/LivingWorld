import type { ReactNode } from "react";

/**
 * One labeled input, everywhere in the Creator — label, a plain-language one-line explanation
 * (what it does / what values make sense), the control itself, and an optional lock toggle.
 * User feedback: without the explanation, fields like "Terrain 1 weight" or "Base cost" gave a
 * first-time user nothing to go on. Every section's fields go through this now instead of
 * hand-rolling the same `<label>` shape with or without a hint.
 */
export function FieldRow({
  testId,
  label,
  hint,
  locked,
  onToggleLock,
  children,
}: {
  testId: string;
  label: string;
  hint?: ReactNode;
  locked?: boolean;
  onToggleLock?: () => void;
  children: ReactNode;
}) {
  return (
    // A single native <label> wrapping everything — clicking the label text or hint still
    // focuses the control, no separate htmlFor/id wiring needed per field.
    <label data-testid={testId} className="field-row">
      <span className="field-row-label">{label}</span>
      {hint && <span className="field-hint">{hint}</span>}
      <span className="field-row-control">
        {children}
        {onToggleLock && (
          <button
            type="button"
            aria-pressed={locked}
            onClick={onToggleLock}
            title={locked ? "Unlock" : "Lock (survives Randomize)"}
          >
            {locked ? "🔒" : "🔓"}
          </button>
        )}
      </span>
    </label>
  );
}
