// Feedback do usuário (2026-08-07): cor por id (`colorById`) faz sentido pra terreno/bioma (o
// domínio só tem ids sem nome), mas pra ENTIDADE ela some o significado — dois NPCs ficam com
// cores arbitrárias diferentes e nada distingue visualmente "isto é um NPC" de "isto é uma
// cidade". Paleta fixa por CATEGORIA (kind), não por id: toda entidade do mesmo tipo tem a
// mesma cor-base; a cor por id continua reservada pra camadas de terreno/bioma/recurso.
import type { EntityRef } from "./types";

export const CATEGORY_COLOR: Record<EntityRef["kind"], string> = {
  city: "#d9a94f", // dourado — mesmo acento do tema
  building: "#a78bda", // lavanda — distingue de cidade/npc
  npc: "#4fd1c5", // ciano — distingue de cidade/prédio
  cell: "#7a8296",
};

/** Cor do anel de destaque de seleção — fixa, nunca por id (nada a ver com a entidade). */
export const SELECTION_HIGHLIGHT_COLOR = "#f0c674";
