export interface NpcAppearance {
  skin: string;
  hair: string;
  hairStyle: "crop" | "parted" | "tuft" | "shaved";
  clothing: string;
  clothingAccent: string;
}

export interface NpcPawnState {
  id: string;
  currentAction?: number | null;
}

const SKINS = ["#f2c49b", "#d99a6c", "#ad6848", "#754331"] as const;
const HAIR = ["#201915", "#513522", "#8a5b32", "#c8a46b", "#8b3030", "#77706a"] as const;
const CLOTHES = ["#486b70", "#6f536f", "#66734b", "#8a5d45", "#425978", "#756844"] as const;
const ACCENTS = ["#c8a96b", "#8db1a5", "#c27b6c", "#9d8cc0", "#b8b36c"] as const;
const HAIR_STYLES: NpcAppearance["hairStyle"][] = ["crop", "parted", "tuft", "shaved"];

function hash(text: string, salt: number): number {
  let value = 2166136261 ^ salt;
  for (let index = 0; index < text.length; index += 1) {
    value ^= text.charCodeAt(index);
    value = Math.imul(value, 16777619);
  }
  return value >>> 0;
}

function pick<T>(values: readonly T[], id: string, salt: number): T {
  return values[hash(id, salt) % values.length];
}

/** Fenótipo visual estável: depende somente da identidade, nunca da ação ou do frame. */
export function appearanceForNpc(id: string): NpcAppearance {
  return {
    skin: pick(SKINS, id, 11),
    hair: pick(HAIR, id, 23),
    hairStyle: pick(HAIR_STYLES, id, 37),
    clothing: pick(CLOTHES, id, 53),
    clothingAccent: pick(ACCENTS, id, 71),
  };
}

function hairMarkup(appearance: NpcAppearance): string {
  switch (appearance.hairStyle) {
    case "parted":
      return `<path d="M25 48C25 27 38 17 50 17c14 0 25 10 26 31-8-10-16-14-25-14-10 0-18 4-26 14Z" fill="${appearance.hair}"/><path d="M50 18 44 35" stroke="#fff" stroke-opacity=".16" stroke-width="3"/>`;
    case "tuft":
      return `<path d="M27 46c1-18 12-28 25-28 11 0 19 6 23 19-8-5-13-7-18-7l5-11-13 10-3-14-5 14-11-8 5 14c-3 3-6 6-8 11Z" fill="${appearance.hair}"/>`;
    case "shaved":
      return `<path d="M28 42c3-16 12-23 23-23 12 0 20 7 23 23-8-6-15-8-23-8s-15 2-23 8Z" fill="${appearance.hair}" opacity=".55"/>`;
    default:
      return `<path d="M25 46c1-19 12-29 26-29 13 0 23 10 25 29-8-8-16-11-25-11s-18 3-26 11Z" fill="${appearance.hair}"/>`;
  }
}

function escapeAttribute(value: string): string {
  return value.replace(/[&<>"']/g, (character) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" })[character]!);
}

/** SVG original em camadas, pronto tanto para React quanto para o cache de imagem do canvas. */
export function npcPawnSvg(state: NpcPawnState): string {
  const appearance = appearanceForNpc(state.id);
  const safeId = escapeAttribute(state.id);
  const stateLayer = state.currentAction == null
    ? ""
    : `<g data-layer="state"><circle cx="78" cy="26" r="10" fill="#171b20" stroke="#f0c96a" stroke-width="3"/><circle cx="78" cy="26" r="3" fill="#f0c96a"/></g>`;

  return `<svg xmlns="http://www.w3.org/2000/svg" width="100" height="120" viewBox="0 0 100 120" data-npc-id="${safeId}">
  <g data-layer="shadow"><ellipse cx="50" cy="103" rx="34" ry="11" fill="#050608" opacity=".48"/></g>
  <g data-layer="body"><path d="M18 91c2-26 14-42 32-42s30 16 32 42c-8 9-19 14-32 14S26 100 18 91Z" fill="${appearance.clothing}" stroke="#171b20" stroke-width="4"/><path d="M26 81c7 5 15 7 24 7s17-2 24-7" fill="none" stroke="${appearance.clothingAccent}" stroke-width="6" stroke-linecap="round"/></g>
  <g data-layer="head"><circle cx="50" cy="45" r="25" fill="${appearance.skin}" stroke="#171b20" stroke-width="4"/><circle cx="39" cy="48" r="2.2" fill="#28231f"/><circle cx="61" cy="48" r="2.2" fill="#28231f"/><path d="M44 60c4 2 8 2 12 0" fill="none" stroke="#714738" stroke-width="2" stroke-linecap="round"/></g>
  <g data-layer="hair">${hairMarkup(appearance)}</g>
  ${stateLayer}
</svg>`;
}

export function npcPawnDataUrl(state: NpcPawnState): string {
  return `data:image/svg+xml;charset=utf-8,${encodeURIComponent(npcPawnSvg(state))}`;
}
