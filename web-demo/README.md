# LivingWorld — World Explorer (Demo)

Demo isolada e descartável do fluxo `World → Settlement → Household → Agent → Why? → Causal
Explorer → Timeline` proposto no doc "Living World Cohesion" (pós-fase 16.2). Prova a
experiência de UX **antes** de qualquer integração com o backend real.

Ver `.specs/features/phase-16-3-web/spec.md` pro contexto completo (Goals/Out of Scope/User
Stories) e `design.md` pras decisões de arquitetura/visual.

## Escopo e isolamento

- **Projeto novo, sem import de `web/src/**`** — código próprio, sem dependência do cliente de
  produção.
- **Zero rede** — todo o dado vem de um fixture estático (`src/fixture/oakbridge.ts`, a vila de
  Oakbridge/Mira Valen do doc). Não fala com `LivingWorld.Api`, não roda simulação, não avança
  tempo real.
- **Descartável** — este projeto existe só pra validar a UX antes de integrar de verdade
  (`phase-16-3-world-cohesion`, backend). Não é o próximo cliente de produção.

## Como rodar

```bash
npm --prefix web-demo install
npm --prefix web-demo run dev     # http://localhost:5173 (ou a porta livre que o Vite escolher)
npm --prefix web-demo test        # suíte de testes (vitest)
npm --prefix web-demo run build   # type-check + build de produção
```

## O que foi portado vs. redesenhado

| Elemento | Origem | Por quê |
| --- | --- | --- |
| Token visual de NPC (`src/npc/appearance.ts`, `src/npc/NpcToken.tsx`) | **Cópia literal** de `web/src/npcAppearance.ts` / `NpcTokenSvg.tsx` | Único elemento explicitamente pedido pra reusar — fenótipo procedural (skin/hair/hairStyle/clothing) já validado no cliente atual |
| Prédios/terreno/tiles do mapa (`src/map/**`) | **Redesenho completo** — isométrico 2:1 simplificado, blocos flat-shaded de 3 faces, paleta nova/neutra | Correção explícita do usuário: não gostava do estilo rústico/top-down atual (`web/src/map-engine/**`) |
| Shell de 1 janela (`src/components/TopBar.tsx`/`Explorer.tsx`/`CenterStage.tsx`/`Inspector.tsx`/`TimelineBar.tsx`) | **Novo**, seguindo literalmente `LivingWorld — Frontend Experience & Design System.md` §5/§26-29/§39-46/§47-48/§105-107 | Doc pede um shell único (Top Bar / Explorer + World + Inspector / Timeline) pras 3 perspectivas (Observe/Table/Inhabit) — implementado 1:1 pra Observe, único modo real desta demo |
| Tema geral (cores, tipografia, painéis) — `src/styles/tokens.css` | **Novo**, baseado nos tokens literais do mesmo doc (§202) | `web/` não tinha um design system formal ainda; esta demo é onde ele entra pela primeira vez |
| Navegação/breadcrumb, stores (`NavigationStore`/`followStore`/`modeStore`) | **Novo**, idioma de store igual ao já usado em `web/src/state/*.ts` (`useSyncExternalStore`) | Estado de navegação específico desta demo, sem framework de roteamento |

### Shell — decisões de adaptação (honestas, não escondidas)

O shell segue o doc literalmente onde o fixture/escopo permite; onde não permite, o padrão
adotado foi **mostrar desabilitado** em vez de esconder como quebrado (mesmo princípio do doc
§6 pro Inhabit Mode), nunca fabricar dado que o fixture não modela:

| Componente do doc | Estado nesta demo |
| --- | --- |
| Mode Selector (§32) | Observe é real; Table/Inhabit aparecem desabilitados com "Coming" — Table Mode é Out of Scope desta spec |
| Simulation Controls (§34-35) | Desabilitados — fixture é um snapshot congelado, não há simulação rodando pra pausar/acelerar (Out of Scope) |
| World Selector (§31) | Só "World Details" é real — mundo único, sem troca de fixture em runtime (Out of Scope) |
| Notifications (§111-112) | Reais — contagem de eventos que afetam entidades seguidas (`followStore`), não decorativo |
| Explorer "People" filtro (§43) | Só All/Followed (reais) — Nearby/Notable exigiriam posição de câmera/flag de importância que o fixture não tem |
| Explorer "Organizations" (§44) | Estado vazio explícito — fixture não modela facções/organizações |
| Explorer "Places" (§42) | Lista flat de settlements — fixture não modela hierarquia de regiões |

## Comparação visual com `web/` (spec P1b Independent Test)

Verificado ao vivo, os dois projetos rodando lado a lado (`web/demo.html`, modo mock offline, vs
`web-demo/`):

- **Token de NPC — idêntico.** `web-demo/src/npc/appearance.ts` é diff-idêntico
  (`diff` sem saída) a `web/src/npcAppearance.ts` — mesmo algoritmo determinístico (hash FNV-1a
  por id), mesma paleta de skin/hair/clothing, mesmo SVG em camadas. Confirmado também por teste
  (`tests/npc/appearance.test.ts`, 5 ids fixos com saída idêntica ao algoritmo original).
- **Prédios/tiles/cidade — visivelmente diferentes.** `web/` usa um grid top-down 2D com
  telhados/paredes texturizados em tom rústico/medieval (`architectureAppearance.ts`); esta demo
  usa blocos isométricos 2:1 flat-shaded (3 faces sombreadas fixas, sem textura), paleta
  neutra/atmosférica (`isoPalette.ts`) — nenhuma sobreposição de estilo entre os dois.
- **Tema geral (painéis/cores/tipografia) — novo nesta demo.** `web/` não tinha design system
  formal; esta demo introduz o primeiro (dark-neutral, accent dourado, cores causais próprias
  pro Causal Explorer) baseado no doc de design do usuário.

## Estrutura

```
src/
  fixture/       dado estático (Oakbridge) + tipos
  npc/           token de NPC portado (appearance.ts + NpcToken.tsx)
  map/           projeção isométrica, paleta, IsoTile, SemanticZoomMap
  nav/           NavigationStore (pilha de breadcrumb + sync de URL)
  state/         followStore, modeStore (Experience/Debug)
  search/        SearchIndex (busca client-side)
  views/         conteúdo de entidade — Settlement/Household/Agent/Why/CausalExplorer/
                 Timeline/Life/WorldFeed/StoryThreads (consumidos pelo Inspector/CenterStage)
  components/    TopBar, Explorer, CenterStage, Inspector, TimelineBar (shell),
                 Breadcrumb, FollowButton, SearchBar (usados dentro do shell)
  styles/        tokens.css (tema visual + layout do shell)
  App.tsx        composition root — monta o shell, troca cada região por
                 NavigationStore.current().kind
```

## Checklist de experiência

Ver [`docs/ui/living-world-experience-checklist.md`](../docs/ui/living-world-experience-checklist.md)
— checklist de design QA do doc do usuário aplicado contra esta demo rodando, com gaps
encontrados documentados (nenhum bloqueante).
