import { useEffect, useRef, useState } from "react";
import { useEntryServices } from "./EntryContext";
import type { GenerationEvent, GenerationResult, WorldDraft } from "./repository/types";

type Phase = "generating" | "complete" | "failed" | "cancelled";

/** Doc §47-51 — generation progress → completion → Enter World. Frontend-only mock (doc §46). */
export function GenerationView({
  draft,
  onCancel,
  onComplete,
}: {
  draft: WorldDraft;
  onCancel: () => void;
  onComplete: (result: GenerationResult) => void;
}) {
  const { generation } = useEntryServices();
  const [phase, setPhase] = useState<Phase>("generating");
  const [event, setEvent] = useState<GenerationEvent | null>(null);
  const [result, setResult] = useState<GenerationResult | null>(null);
  const controllerRef = useRef<AbortController | null>(null);

  useEffect(() => {
    const controller = new AbortController();
    controllerRef.current = controller;
    generation
      .generate(draft, setEvent, controller.signal)
      .then((r) => {
        setResult(r);
        setPhase("complete");
      })
      .catch((err) => {
        if (err?.name === "AbortError") setPhase("cancelled");
        else setPhase("failed");
      });
    return () => controller.abort();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [draft.id]);

  function cancel() {
    controllerRef.current?.abort();
    onCancel();
  }

  if (phase === "complete" && result) {
    return (
      <div data-testid="generation-complete">
        <h1>The world is ready</h1>
        <button type="button" data-testid="enter-world" onClick={() => onComplete(result)}>
          Enter World
        </button>
      </div>
    );
  }

  if (phase === "failed") {
    return (
      <div data-testid="generation-failed">
        <p>World generation failed.</p>
        <button type="button" onClick={onCancel}>
          Back
        </button>
      </div>
    );
  }

  return (
    <div data-testid="generation-view">
      <h1>World Generation</h1>
      <p data-testid="generation-stage">{event?.message ?? "Preparing world..."}</p>
      <div data-testid="generation-progress" role="progressbar" aria-valuenow={event?.progress ?? 0} aria-valuemin={0} aria-valuemax={100}>
        <div style={{ width: `${event?.progress ?? 0}%` }} />
      </div>
      <button type="button" data-testid="generation-cancel" onClick={cancel}>
        Cancel
      </button>
    </div>
  );
}
