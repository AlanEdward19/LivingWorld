export type ActionIcon = "moon" | "apple" | "tool" | "chat" | "coin" | "waves" | "question";

export interface ActionVisual {
  key: string;
  label: string;
  icon: ActionIcon;
  animated: boolean;
  hidden: boolean;
}

/** `ActionType` integer ids from `LivingWorld.Domain` — Eat..Buy..UsePower. */
export const ACTION_TYPE_IDS = [0, 1, 2, 3, 4, 5, 6, 7] as const;

/**
 * Stage 4 `ProcessVisual.descriptorKey` values projected by rest/food/water/crop/construction.
 * Rest place kinds: Ground/Dwelling/Bed → `sleep-*`.
 */
export const STAGE4_PROCESS_DESCRIPTORS = [
  "sleep-ground",
  "sleep-dwelling",
  "sleep-bed",
  "eat-raw",
  "eat-prepared",
  "cook-food",
  "collect-water",
  "carry-water",
  "deliver-water",
  "plant-crop",
  "water-crop",
  "harvest-crop",
  "construction",
] as const;

/**
 * LWV-07.3 lifecycle `WorldEventKind` integer values (enum order in Domain).
 * Birth, Death, Starvation, Marriage, courtship family, maternal/still birth.
 */
export const LWV07_EVENT_KINDS = [0, 1, 2, 9, 10, 11, 12, 13, 14] as const;

export interface NpcAnimationSpec {
  key: string;
  keyframes: string;
  durationMs: number;
  a11yLabel: string;
  reducedMotionFallback: "static-icon";
  icon: ActionIcon;
  hidden: boolean;
  animated: boolean;
}

const STATIC: Pick<NpcAnimationSpec, "keyframes" | "durationMs" | "reducedMotionFallback" | "animated"> = {
  keyframes: "none",
  durationMs: 0,
  reducedMotionFallback: "static-icon",
  animated: false,
};

function spec(
  key: string,
  a11yLabel: string,
  icon: ActionIcon,
  extras: Partial<NpcAnimationSpec> = {},
): NpcAnimationSpec {
  return {
    key,
    a11yLabel,
    icon,
    hidden: false,
    ...STATIC,
    ...extras,
  };
}

const REST_ZZZ: Partial<NpcAnimationSpec> = { keyframes: "npc-rest-zzz", durationMs: 1800, animated: true };
const EAT_BITE: Partial<NpcAnimationSpec> = { keyframes: "npc-eat-bite", durationMs: 1400, animated: true };
const BUY_COIN: Partial<NpcAnimationSpec> = { keyframes: "npc-buy-coin", durationMs: 1400, animated: true };

const ACTION_SPECS: Record<number, NpcAnimationSpec> = {
  0: spec("eat", "Comendo", "apple", EAT_BITE),
  1: spec("sleep", "Dormindo", "moon", REST_ZZZ),
  2: spec("work", "Trabalhando", "tool", { keyframes: "npc-work-hammer", durationMs: 1400, animated: true }),
  3: spec("socialize", "Socializando", "chat", { keyframes: "npc-social-chat", durationMs: 1400, animated: true }),
  // SPEC_DEVIATION: LWV-07.1 says every ActionType SHALL show a cue; Travel stays hidden.
  // Reason: T9/T21 — the map walking/relocation route is the travel cue; a token badge would duplicate it.
  4: spec("travel", "Viajando", "question", { hidden: true }),
  5: spec("rest", "Descansando", "waves", REST_ZZZ),
  6: spec("buy", "Comprando", "coin", BUY_COIN),
  7: spec("use-power", "Usando poder", "waves", { keyframes: "npc-social-chat", durationMs: 1400, animated: true }),
};

const PROCESS_SPECS: Record<string, NpcAnimationSpec> = {
  "sleep-ground": spec("sleep-ground", "Dormindo no chão", "moon", REST_ZZZ),
  "sleep-dwelling": spec("sleep-dwelling", "Dormindo em casa", "moon", REST_ZZZ),
  "sleep-bed": spec("sleep-bed", "Dormindo na cama", "moon", REST_ZZZ),
  "eat-raw": spec("eat-raw", "Comendo cru", "apple", EAT_BITE),
  "eat-prepared": spec("eat-prepared", "Comendo refeição", "apple", EAT_BITE),
  "cook-food": spec("cook-food", "Cozinhando", "apple", { keyframes: "npc-cook-steam", durationMs: 1600, animated: true }),
  "collect-water": spec("collect-water", "Coletando água", "waves", { keyframes: "npc-water-collect", durationMs: 1200, animated: true }),
  "carry-water": spec("carry-water", "Carregando água", "waves", { keyframes: "npc-water-carry", durationMs: 1200, animated: true }),
  "deliver-water": spec("deliver-water", "Entregando água", "waves", { keyframes: "npc-water-deliver", durationMs: 1200, animated: true }),
  "plant-crop": spec("plant-crop", "Plantando", "tool", { keyframes: "npc-crop-plant", durationMs: 1400, animated: true }),
  "water-crop": spec("water-crop", "Regando a lavoura", "waves", { keyframes: "npc-crop-water", durationMs: 1200, animated: true }),
  "harvest-crop": spec("harvest-crop", "Colhendo", "tool", { keyframes: "npc-crop-harvest", durationMs: 1400, animated: true }),
  construction: spec("construction", "Construindo", "tool", { keyframes: "npc-build-scaffold", durationMs: 1400, animated: true }),
};

const EVENT_SPECS: Record<number, NpcAnimationSpec> = {
  0: spec("birth", "Um novo habitante nasceu", "waves", { keyframes: "npc-life-birth", durationMs: 1100, animated: true }),
  1: spec("death", "Um habitante faleceu", "moon", { keyframes: "npc-life-farewell", durationMs: 1400, animated: true }),
  2: spec("starvation", "A fome causou uma morte", "apple", { keyframes: "npc-life-farewell", durationMs: 1400, animated: true }),
  9: spec("marriage", "Um casamento foi celebrado", "chat", { keyframes: "npc-marriage-ribbon", durationMs: 1600, animated: true }),
  10: spec("courtship-started", "Um cortejo começou", "chat", { keyframes: "npc-courtship-spark", durationMs: 1200, animated: true }),
  11: spec("courtship-rejected", "Um cortejo não foi correspondido", "chat", { keyframes: "npc-courtship-spark", durationMs: 1200, animated: true }),
  12: spec("courtship-succeeded", "Um cortejo foi correspondido", "chat", { keyframes: "npc-courtship-spark", durationMs: 1200, animated: true }),
  13: spec("maternal-death", "Uma mãe faleceu durante o parto", "moon", { keyframes: "npc-life-farewell", durationMs: 1400, animated: true }),
  14: spec("still-birth", "Uma gestação terminou sem nascimento vivo", "moon", { keyframes: "npc-life-farewell", durationMs: 1400, animated: true }),
};

export function animationSpecForUnknown(labelHint: string): NpcAnimationSpec {
  return spec("unknown", labelHint.startsWith("Atividade") ? labelHint : `Atividade ${labelHint}`, "question");
}

export function animationSpecForAction(actionType: number): NpcAnimationSpec {
  return ACTION_SPECS[actionType] ?? animationSpecForUnknown(String(actionType));
}

export function animationSpecForProcess(descriptorKey: string): NpcAnimationSpec {
  return PROCESS_SPECS[descriptorKey] ?? animationSpecForUnknown(descriptorKey);
}

export function animationSpecForEvent(worldEventKind: number): NpcAnimationSpec {
  return EVENT_SPECS[worldEventKind] ?? animationSpecForUnknown(String(worldEventKind));
}

export function actionVisualFromSpec(specValue: NpcAnimationSpec): ActionVisual {
  return {
    key: specValue.key,
    label: specValue.a11yLabel,
    icon: specValue.icon,
    animated: specValue.animated,
    hidden: specValue.hidden,
  };
}
