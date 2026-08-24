import type { ExtraordinaryDescriptorRow } from "./scenarioDefaults";

export interface ExtraordinaryTemplate {
  name: string;
  description: string;
  descriptor: ExtraordinaryDescriptorRow;
}

function descriptor(patch: Partial<ExtraordinaryDescriptorRow>): ExtraordinaryDescriptorRow {
  return {
    id: "", source: "", effects: "npc.health:10", mode: "Active", costs: "",
    reliability: "Guaranteed", failureModes: "", intrinsicVulnerabilities: "",
    manifestations: "", acquisitionRules: "event:authoring", appearanceScaleMultiplier: 1,
    appearanceSkinTint: "", appearanceMovementTrail: "",
    needSubstitutionReplacesNeed: "", needSubstitutionResourceId: null,
    needSubstitutionUnitsPerUse: 1, senescenceRateMultiplier: 1,
    manifestationCondition: "", ...patch,
  };
}

function lantern(color: string, tint: string): ExtraordinaryTemplate {
  return {
    name: `Lanterna ${color}`,
    description: "Voo, aura energética e constructos temporários.",
    descriptor: descriptor({
      id: `lanterna-${color.toLocaleLowerCase("pt-BR")}`, source: "artefato-externo",
      effects: `npc.health:10,movement.flight:1,construct.create:2x1:40:24:${tint}`,
      costs: "carrier.sleep:10", intrinsicVulnerabilities: "artefato-removido",
      manifestations: `${tint}-aura`, appearanceSkinTint: `${tint}-glow`,
      appearanceMovementTrail: tint,
    }),
  };
}

export const EXTRAORDINARY_TEMPLATES: ExtraordinaryTemplate[] = [
  {
    name: "Vampiro", description: "Manifestação noturna, palidez, sangue e ausência de senescência.",
    descriptor: descriptor({
      id: "vampiro", source: "exposicao-predatoria", mode: "Conditional",
      manifestations: "mudanca-noturna", intrinsicVulnerabilities: "luz-solar",
      appearanceSkinTint: "pale", appearanceMovementTrail: "mist",
      needSubstitutionReplacesNeed: "hunger", needSubstitutionResourceId: 9,
      needSubstitutionUnitsPerUse: 2, senescenceRateMultiplier: 0,
      manifestationCondition: "world:is-night",
    }),
  },
  {
    name: "Lobisomem", description: "Transformação cíclica, porte maior e vulnerabilidade específica.",
    descriptor: descriptor({
      id: "lobisomem", source: "maldicao-herdada", mode: "Conditional",
      costs: "carrier.sleep:5", manifestations: "mudanca-lunar",
      intrinsicVulnerabilities: "prata", appearanceScaleMultiplier: 1.4,
      appearanceSkinTint: "fur", appearanceMovementTrail: "dust",
      manifestationCondition: "world:tick-cycle:672:0:24",
    }),
  },
  lantern("Verde", "green-energy"),
  lantern("Azul", "blue-energy"),
  lantern("Amarelo", "yellow-energy"),
  {
    name: "Kryptoniano", description: "Voo, grande velocidade e longevidade sob fonte estelar.",
    descriptor: descriptor({
      id: "kryptoniano", source: "origem-estelar", mode: "Passive",
      effects: "npc.health:10,movement.flight:1,movement.speed-multiplier:2",
      intrinsicVulnerabilities: "radiacao-especifica", manifestations: "aura-solar",
      appearanceScaleMultiplier: 1.1, appearanceSkinTint: "sun-charged",
      appearanceMovementTrail: "air-ripple", senescenceRateMultiplier: 0.2,
    }),
  },
  {
    name: "Velocista", description: "Velocidade física, eletricidade visual e custo de esforço.",
    descriptor: descriptor({
      id: "velocista", source: "acidente-energetico", mode: "Conditional",
      effects: "npc.health:10,movement.speed-multiplier:4", costs: "carrier.sleep:15",
      intrinsicVulnerabilities: "esgotamento-energetico", manifestations: "aura-de-velocidade",
      appearanceSkinTint: "charged", appearanceMovementTrail: "electricity",
      manifestationCondition: "carrier:action:Travel",
    }),
  },
];
