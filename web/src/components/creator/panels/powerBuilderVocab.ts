// Fase 17 (reforma web): vocabulário curado + parse/serialize pra tirar o criador de poder da
// "sopa de letras" (campos CSV crus tipo "npc.health:10,movement.flight:1"). O schema de fio
// (ExtraordinaryDescriptorRow, todo texto/CSV) não muda — só a UI para de expor esse texto cru
// como caminho principal. Qualquer token que não reconhecemos aqui é preservado em "extra" e
// mostrado no editor avançado, nunca descartado silenciosamente.
//
// IMPORTANTE (recon no motor, `ExtraordinaryInvocationEngine.cs`/`ExtraordinaryLocomotion.cs`):
// o motor só entende um conjunto FECHADO e pequeno de efeitos — switch em C#, sem registro
// data-driven/plugin. Fora esse conjunto (`STAT_KEYS`, voo, multiplicador de velocidade,
// constructo), qualquer token é rejeitado em tempo de invocação com "Effects: alvo não suportado".
// Teleporte, visão de calor, invisibilidade etc. NÃO existem hoje — nem no motor, nem aqui — e
// exigiriam mudança de backend (C#), não só de UI. STAT_KEYS/EFFECT_CATALOG abaixo é a lista
// completa e honesta do que existe; a UI não pode fingir cobertura maior que essa.

export const STAT_KEYS = ["health", "hunger", "thirst", "sleep", "social"] as const;
export type StatKey = typeof STAT_KEYS[number];

export const STAT_LABELS: Record<StatKey, string> = {
  health: "Saúde", hunger: "Fome", thirst: "Sede", sleep: "Sono", social: "Social",
};

export interface ParsedEffects {
  stats: Partial<Record<StatKey, number>>;
  flight: boolean;
  speed: number | null;
  construct: { dims: string; costA: string; costB: string; color: string } | null;
  extra: string;
}

function tokensOf(csv: string): string[] {
  return csv.split(",").map((t) => t.trim()).filter(Boolean);
}

export function parseEffects(csv: string): ParsedEffects {
  const leftovers: string[] = [];
  const stats: ParsedEffects["stats"] = {};
  let flight = false;
  let speed: number | null = null;
  let construct: ParsedEffects["construct"] = null;

  for (const token of tokensOf(csv)) {
    const statMatch = /^npc\.(health|hunger|thirst|sleep|social):(-?\d+(?:\.\d+)?)$/.exec(token);
    const speedMatch = /^movement\.speed-multiplier:(\d+(?:\.\d+)?)$/.exec(token);
    const constructMatch = /^construct\.create:([^:]+):([^:]+):([^:]+):([^:]+)$/.exec(token);
    if (statMatch) {
      stats[statMatch[1] as StatKey] = Number(statMatch[2]);
    } else if (token === "movement.flight:1") {
      flight = true;
    } else if (speedMatch) {
      speed = Number(speedMatch[1]);
    } else if (constructMatch) {
      construct = { dims: constructMatch[1], costA: constructMatch[2], costB: constructMatch[3], color: constructMatch[4] };
    } else {
      leftovers.push(token);
    }
  }
  return { stats, flight, speed, construct, extra: leftovers.join(", ") };
}

export function serializeEffects(state: ParsedEffects): string {
  const tokens: string[] = [];
  for (const key of STAT_KEYS) {
    const value = state.stats[key];
    if (value !== undefined) tokens.push(`npc.${key}:${value}`);
  }
  if (state.flight) tokens.push("movement.flight:1");
  if (state.speed !== null) tokens.push(`movement.speed-multiplier:${state.speed}`);
  if (state.construct) {
    const { dims, costA, costB, color } = state.construct;
    tokens.push(`construct.create:${dims}:${costA}:${costB}:${color}`);
  }
  tokens.push(...tokensOf(state.extra));
  return tokens.join(", ");
}

export interface ParsedCosts {
  stats: Partial<Record<StatKey, number>>;
  householdResource: { resourceId: number; amount: number } | null;
  extra: string;
}

export function parseCosts(csv: string): ParsedCosts {
  const leftovers: string[] = [];
  const stats: ParsedCosts["stats"] = {};
  let householdResource: ParsedCosts["householdResource"] = null;
  for (const token of tokensOf(csv)) {
    const statMatch = /^carrier\.(health|hunger|thirst|sleep|social):(-?\d+(?:\.\d+)?)$/.exec(token);
    const resourceMatch = /^household\.resource\.(\d+):(-?\d+(?:\.\d+)?)$/.exec(token);
    if (statMatch) {
      stats[statMatch[1] as StatKey] = Number(statMatch[2]);
    } else if (resourceMatch) {
      householdResource = { resourceId: Number(resourceMatch[1]), amount: Number(resourceMatch[2]) };
    } else {
      leftovers.push(token);
    }
  }
  return { stats, householdResource, extra: leftovers.join(", ") };
}

export function serializeCosts(state: ParsedCosts): string {
  const tokens: string[] = [];
  for (const key of STAT_KEYS) {
    const value = state.stats[key];
    if (value !== undefined) tokens.push(`carrier.${key}:${value}`);
  }
  if (state.householdResource) {
    tokens.push(`household.resource.${state.householdResource.resourceId}:${state.householdResource.amount}`);
  }
  tokens.push(...tokensOf(state.extra));
  return tokens.join(", ");
}

export const SOURCE_OPTIONS: ReadonlyArray<{ value: string; label: string; hint: string }> = [
  { value: "artefato-externo", label: "Artefato externo", hint: "Um objeto concede o poder — pode ser perdido ou roubado." },
  { value: "exposicao-predatoria", label: "Exposição predatória", hint: "Transmitido por ataque — herança sombria, passa adiante." },
  { value: "maldicao-herdada", label: "Maldição herdada", hint: "Vem de linhagem ou pacto — cíclico, ligado a condições." },
  { value: "origem-estelar", label: "Origem estelar", hint: "Nascido sob influência cósmica — raro e geralmente passivo." },
  { value: "acidente-energetico", label: "Acidente energético", hint: "Evento único deu o poder — normalmente exige esforço." },
];

export const CONDITION_PRESETS: ReadonlyArray<{ value: string; label: string; hint: string }> = [
  { value: "", label: "Sempre ativo", hint: "Não depende de nada — o poder está sempre disponível." },
  { value: "world:is-night", label: "Só à noite", hint: "Só se manifesta entre o pôr e o nascer do sol." },
  { value: "world:tick-cycle:672:0:24", label: "Ciclo lunar (28 dias)", hint: "Ativa em uma janela recorrente de 28 dias." },
  { value: "carrier:action:Travel", label: "Durante viagem", hint: "Só enquanto o portador está viajando." },
];

// Recon no motor (`ExtraordinaryInvocationEngine.cs`, `PrepareFailureModes`): só `carrier.health:`
// tem efeito mecânico (custo extra de saúde na falha); qualquer outra tag em `failureModes` é
// registrada mas não muda nada no resultado — puramente narrativo/crônica. As tags abaixo são
// exatamente as que `docs/roadmap/phase-16-powers.md` (task 5) cita como conceito de falha.
export interface ParsedFailureModes {
  healthPenalty: number | null;
  tags: string;
}

export function parseFailureModes(csv: string): ParsedFailureModes {
  const tags: string[] = [];
  let healthPenalty: number | null = null;
  for (const token of tokensOf(csv)) {
    const match = /^carrier\.health:(-?\d+(?:\.\d+)?)$/.exec(token);
    if (match) {
      healthPenalty = Number(match[1]);
    } else {
      tags.push(token);
    }
  }
  return { healthPenalty, tags: tags.join(", ") };
}

export function serializeFailureModes(state: ParsedFailureModes): string {
  const tokens: string[] = [];
  if (state.healthPenalty !== null) tokens.push(`carrier.health:${state.healthPenalty}`);
  tokens.push(...tokensOf(state.tags));
  return tokens.join(", ");
}

export const FAILURE_TAG_OPTIONS: ReadonlyArray<{ value: string; label: string }> = [
  { value: "efeito-parcial", label: "Efeito parcial" },
  { value: "alvo-errado", label: "Alvo errado" },
  { value: "custo-sem-resultado", label: "Custo sem resultado" },
  { value: "exposicao", label: "Exposição (revela o poder)" },
  { value: "dano-permanente-portador", label: "Dano permanente ao portador" },
];

// Recon no motor (`ExtraordinaryStateSystem.cs`): a gramática de `AcquisitionRules` é genérica —
// "<gatilho>", "event:<gatilho>" ou "rate:0-1:event:<gatilho>" — MAS nenhum sistema do motor hoje
// dispara o evento "acquire|..." que esse gatilho casaria. Poderes só são concedidos de verdade
// hoje pela aba Administração (grant manual), que ignora este campo por completo. Preencher aqui
// fica salvo no descritor, mas não tem efeito nenhum até a Fase 16 ser reaberta e algum sistema
// (nascimento, quase-morte, trauma, item, ritual, exposição) passar a emitir esse evento.
export const ACQUISITION_GRAMMAR_HINT = "formatos aceitos: \"gatilho\", \"event:gatilho\" ou \"rate:0-1:event:gatilho\"";

export const TAG_PRESETS: Record<"intrinsicVulnerabilities" | "manifestations", ReadonlyArray<{ value: string; label: string }>> = {
  intrinsicVulnerabilities: [
    { value: "luz-solar", label: "Luz solar" },
    { value: "prata", label: "Prata" },
    { value: "artefato-removido", label: "Artefato removido" },
    { value: "esgotamento-energetico", label: "Esgotamento energético" },
    { value: "radiacao-especifica", label: "Radiação específica" },
  ],
  manifestations: [
    { value: "mudanca-noturna", label: "Mudança noturna" },
    { value: "mudanca-lunar", label: "Mudança lunar" },
    { value: "aura-solar", label: "Aura solar" },
    { value: "aura-de-velocidade", label: "Aura de velocidade" },
  ],
};

export const TINT_SWATCHES: ReadonlyArray<{ value: string; label: string; color: string }> = [
  { value: "pale", label: "Pálido", color: "#c9d3d8" },
  { value: "fur", label: "Peludo", color: "#8a6a4a" },
  { value: "sun-charged", label: "Carregado solar", color: "#f0c674" },
  { value: "charged", label: "Eletrizado", color: "#8fd0ff" },
  { value: "green-energy", label: "Energia verde", color: "#6fd88a" },
  { value: "blue-energy", label: "Energia azul", color: "#6fa8f5" },
  { value: "yellow-energy", label: "Energia amarela", color: "#f0e26a" },
];

export const TRAIL_OPTIONS: ReadonlyArray<{ value: string; label: string }> = [
  { value: "mist", label: "Névoa" },
  { value: "dust", label: "Poeira" },
  { value: "air-ripple", label: "Ondulação de ar" },
  { value: "electricity", label: "Eletricidade" },
];

export const NEED_OPTIONS: ReadonlyArray<{ value: string; label: string }> = [
  { value: "hunger", label: "Fome" },
  { value: "thirst", label: "Sede" },
  { value: "sleep", label: "Sono" },
  { value: "social", label: "Social" },
];
