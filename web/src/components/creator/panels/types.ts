import type { ScenarioFormState } from "../../../scenarioDefaults";

export interface PanelProps {
  form: ScenarioFormState;
  set: <K extends keyof ScenarioFormState>(key: K, value: ScenarioFormState[K]) => void;
  /** T26: só profession/skill têm catálogo real (`GET /periods/{id}/catalog`), condicional por período. */
  professionNames?: Record<number, string>;
  skillNames?: Record<number, string>;
}
