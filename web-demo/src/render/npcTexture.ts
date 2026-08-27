import { Texture } from "pixi.js";
import { npcPawnDataUrl } from "../npc/appearance";

const cache = new Map<string, Promise<Texture>>();

/**
 * Textura Pixi do pawn de um NPC — reusa o mesmo SVG procedural de `appearanceForNpc`
 * (`appearance.ts`/`NpcToken`) em vez de desenhar uma arte nova só pro renderer Canvas (AD-020):
 * mesma identidade visual já validada (cabeça/corpo/cabelo/roupa em camadas, doc: "não use
 * emoji como personagem final"), só um destino de render diferente (Sprite em vez de `<img>`).
 *
 * Async e cacheada por Promise (não só por Texture) — bug corrigido: `Texture.from(image)` com
 * uma `Image` que ainda não terminou de decodificar vira uma textura 0×0 (sprite invisível) até
 * alguém forçar uma atualização, o que nunca acontecia aqui. `image.decode()` garante que o
 * pixel data já existe antes da `Texture` ser criada.
 */
export function getNpcTexture(id: string): Promise<Texture> {
  const cached = cache.get(id);
  if (cached) return cached;
  const image = new Image();
  image.src = npcPawnDataUrl({ id });
  // `decode()` não existe em jsdom (testes) — lá o Pixi inteiro já é mockado (tests/setup.ts),
  // então a textura real não importa; resolve na hora em vez de travar esperando um evento que
  // o jsdom nunca dispara por padrão.
  const ready = typeof image.decode === "function" ? image.decode().catch(() => {}) : Promise.resolve();
  const promise = ready.then(() => Texture.from(image));
  cache.set(id, promise);
  return promise;
}
