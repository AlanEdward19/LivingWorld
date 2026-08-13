# STATE

## Decisions

### AD-001
- **Decision**: A tela de "criar mundo" do cliente web vai expor o body de cenário (`ScenarioLoaderV2`) como formulário campo a campo (não um textarea de JSON cru).
- **Reason**: Usuário pediu explicitamente formulário campo a campo, mesmo sendo mais trabalho de UI — prioriza usabilidade sobre velocidade de entrega.
- **Trade-off**: Formulário precisa acompanhar manualmente qualquer campo novo que `ScenarioLoaderV2`/`MapScenarioLoader`/`PopulationScenarioLoader`/etc. passem a exigir (um editor de JSON cru não teria esse risco de drift, mas foi descartado).
- **Scope**: Feature ad-hoc "criar mundo" (ainda sem `.specs/features/` própria) — cliente web (`web/src/**`) e o novo endpoint de criação de mundo na API.
- **Date**: 2026-08-06
- **Status**: active

### AD-002
- **Decision**: Tela inicial (start menu) estilo jogo — botões centrais (Continuar/Criar mundo/Configurações) sobre fundo animado — com motivo visual deliberadamente atemporal (campo de partículas à deriva), não medieval nem preso a nenhuma época.
- **Reason**: Usuário pediu estilo "Minecraft" de menu inicial, mas corrigiu que o projeto simula qualquer período de tempo (não só medieval) — iconografia de época específica ficaria errada.
- **Trade-off**: Sem CSS/design system prévio no cliente (era HTML puro sem estilo); criado `web/src/styles/global.css` com estilos por seletor de elemento (não por classe) pra herdar em todos os componentes existentes sem reescrevê-los.
- **Scope**: UX geral do cliente web (fase 15) — tema visual, menu inicial, tela de configurações placeholder.
- **Date**: 2026-08-06
- **Status**: active

### AD-003
- **Decision**: Grid 2D real (canvas) substitui listas/botões no mapa-múndi/cidade; NPCs viram token/dot por LOD de zoom; seleção por clique abre painel lateral; editor de mapa em "criar mundo" também vira grid clicável; overlay de mapa (tecla M) em modo jogador. Token/terreno usam cor procedural determinística por id — não há pipeline de arte (pixel-art/ilustrado) no projeto.
- **Reason**: Usuário rejeitou a entrega textual do T8 original ("nada batendo com o que eu esperava") e trouxe referências de VTT ilustrado — cor procedural é o teto realista sem um pipeline de assets.
- **Trade-off**: Prédios não têm `CellCoord` no domínio — layout em anel calculado no cliente (aproximado, marcado visualmente, não é posição real). Movimento "andar até a saída" pra trocar de escopo mundo↔cidade não foi construído (exigiria sistema de movimento em escala mapa-múndi que não existe) — mantido botão/painel de drill-down.
- **Scope**: Fase 15, UX Pass 2 — ver `.specs/features/phase-15-map-visual/spec.md` (seção "UX Pass 2"), `design.md` e `tasks.md` (T10-T15), atualizados antes da implementação a pedido do usuário.
- **Date**: 2026-08-06
- **Status**: active

### AD-004
- **Decision**: UX Pass 3 — corrigido bug real (modo Jogador quebrava a conexão realtime no mapa-múndi), mapa virou tela cheia de verdade (HUD flutuante em vez de header/legenda empurrando layout), sem teto de tamanho de mapa além do limite técnico real de canvas do browser, formulário "criar mundo" virou wizard por abas com seletor de template real (backend seeda 3 períodos válidos, não inventado no cliente).
- **Reason**: Usuário rejeitou a entrega da AD-003 ("mapa é um quadrado minúsculo", "modo jogador não pega", "form ainda parece formulário") e pediu presets/templates + wizard bonito.
- **Trade-off**: Templates seedados (`DefaultPeriodSeeder.cs`) são só 3 variações de tamanho/população do mesmo cenário base — não há autoria de conteúdo temático por template (nomes de profissão, terrenos diferentes etc.), só o que já era editável no formulário. Presets de população específicos por aba (ex.: botão "vila" dentro da aba População) não foram construídos — o mecanismo de template cobre esse caso de uso.
- **Scope**: Fase 15, UX Pass 3 — mesmos arquivos de spec/design/tasks da fase 15.
- **Date**: 2026-08-06
- **Status**: active

## Handoff

- **Feature**: Fase 15.1 (VTT frontend redesign) fechada e validada (ver `.specs/features/phase-15.1-vtt-frontend-redesign/validation.md`, PASS). Sessão atual: bugfix pós-fechamento — 3 bugs relatados pelo usuário no fluxo de "criar mundo", com um 4º achado durante a investigação ao vivo.
- **Phase / Task**: Fora do fluxo `tlc-spec-driven` normal — bugfix direto, sem tasks.md próprio. Investigação feita com API+web reais rodando (`preview_start` + `Claude_Browser`), não só leitura de código.

- **Bugs relatados pelo usuário (nesta ordem)**:
  1. Cancelar a criação de mundo não voltava ao menu principal, "abria um mundo". **Corrigido e confirmado ao vivo** (`web/src/App.tsx`: `cancelCreatingWorld`/`hasEnteredWorldRef`, commit `3aac17e`, já testado no browser real nesta sessão).
  2. Nome digitado na tela de criação nunca chegava na API → sempre "Name é obrigatório". **Corrigido** (`createWorld()` em `api.ts` nunca enviava `name`; `WorldEditor` não recebia `worldName`). Também: "Começar" agora exige nome preenchido (antes avançava sem). Commit `3aac17e`.
  3. **Header mostrava "Criar mundo" enquanto já dentro de um mundo rodando** — pedido do usuário: só "☰ menu" (voltar), "talvez um botão de salvar" (não implementado — não existe endpoint de save manual hoje, seria feature nova, não bugfix; ficou só removido o botão indevido). **Corrigido nesta sessão, NÃO COMMITADO AINDA**: `web/src/App.tsx` — "Criar mundo" só aparece a partir do `StartMenu` agora; dentro do jogo só existe "Cancelar" (durante criação) ou nada (jogando). `web/tests/App.test.tsx` atualizado (removido teste de "criar mundo mid-game", novo teste garante ausência do botão).
  4. **"PIOR BUG": população/assentamentos autorados nunca apareciam** (nem NPCs, nem cidade real — sempre a mesma cidade fixa (2,2) com população 0). Root cause encontrado por reprodução ao vivo (curl direto na API):
     - `PopulationSeeder.SeedInitial` sempre criava NPCs com `City = default(CityId)` — nunca vinculados a nenhuma cidade.
     - `GlobalProjector`/`CityProjector` filtram por `Npc.City` == cidade conhecida → esses NPCs ficavam invisíveis em TODA projeção (World e City).
     - `form.cities` (usado por `Cities` no JSON) é um array TOTALMENTE separado de `form.settlements` (pintado no mapa do WorldEditor) e de `VillageX/VillageY` (onde a população realmente nasce) — três conceitos desconectados. `defaultScenarioForm()` sempre manda uma cidade fixa em (2,2) com `count:0`, nunca tocada pelo editor visual.
     - Afeta **100% dos mundos criados** (em branco ou por template — os 3 templates do `DefaultPeriodSeeder` também têm `Cities: []` e população via `InitialPopulation`), não só o World Editor visual. Não é uma regressão da fase 15.1 — bug pré-existente, nunca coberto por teste (`WorldCreateEndpointsTests` nunca checava NPC/cidade visível).
     - **Fix implementado, NÃO COMMITADO AINDA**: `ScenarioLoaderV2.LoadWorld` agora funda (ou reaproveita, se já existir uma na mesma célula) uma cidade real em `population.Village` ANTES de semear a população, e passa esse `CityId` até `PopulationGenerator.GenerateInitial` (novo param opcional `city`) → `Npc`/`Household` nascem com `City` real. Arquivos: `PopulationGenerator.cs`, `PopulationSeeder.cs`, `ScenarioLoaderV2.cs`.
  5. **Achado ao vivo pelo usuário durante o teste do bug 4** (screenshot): a cidade renderizada era MAIOR que o mapa inteiro. Root cause: `CityBoundsResolver.Resolve` usava um tamanho FIXO 34×24 células (herdado de um placeholder client-side de quando cidade tinha grid local próprio) — em mundos Pequeno (10×10) ou Médio (20×20) a muralha sempre estourava a borda. **Fix implementado, NÃO COMMITADO AINDA**: `CityBoundsResolver.Resolve(city, population)` agora escala o lado do quadrado com a população real (piso 4, teto 34 — o teto preserva o tamanho de cidades grandes de antes). Chamada única em `GlobalProjector.cs` atualizada para passar a população já calculada.

- **Arquivos alterados, staged/committed status**:
  - **Committed** (`3aac17e`, sessão anterior): fix dos bugs 1 e 2 (`App.tsx`, `api.ts`, `WorldEditor.tsx`, `PresetStart.tsx`, `global.css`, testes).
  - **NÃO COMMITADO** (`git status` no momento da pausa):
    - `src/LivingWorld.Api/Visual/GlobalProjector.cs`
    - `src/LivingWorld.Domain/Cities/CityBoundsResolver.cs`
    - `src/LivingWorld.Domain/Cities/SpatialBoundsResolver.cs`
    - `src/LivingWorld.Domain/Population/PopulationGenerator.cs`
    - `src/LivingWorld.Simulation/Population/PopulationSeeder.cs`
    - `src/LivingWorld.Simulation/ScenarioLoaderV2.cs`
    - `tests/LivingWorld.Tests/Cities/BuildingFootprintAndPlacementTests.cs` (atualizado pro novo `Resolve(city, population)`)
    - `tests/LivingWorld.Tests/Geography/SpatialAddressAndScaleTests.cs` (idem)
    - `tests/LivingWorld.Tests/Periods/ScenarioLoaderV2Tests.cs` (novo assert: população fica numa cidade fundada de verdade, `Cities.Count == 2` em vez de 1 — o cenário de teste tem Village≠City declarada)
    - `tests/LivingWorld.Tests/Visual/GlobalProjectorTests.cs` (idem, novo param population)
    - `web/src/App.tsx` (bug 3 — remove "Criar mundo" fora de criação)
    - `web/tests/App.test.tsx` (idem)

- **Verificação feita**:
  - `dotnet build LivingWorld.sln` — **verde** (0 erros).
  - Regressão real encontrada e corrigida durante a própria verificação: `PopulationGenerator.PairIntoHouseholds` recebeu o parâmetro `city` novo mas o `new Household(...)` não o repassava — `Npc.City` ficava certo, `Household.City` continuava `default`. Corrigido (`city: city` no construtor).
  - `dotnet test --filter "ScenarioLoaderV2Tests|BuildingFootprintAndPlacement|SpatialAddressAndScale|GlobalProjectorTests|WorldCreateEndpointsTests|PopulationGeneratorTests|CityAndBuildingAuthoringTests"` — **81 passed, 0 failed**, ~1s.
  - `npx vitest run` (suíte web completa) — **267 passed** (47 arquivos). `npx tsc --noEmit` — limpo.
  - Bugs 1 e 2 confirmados ao vivo no browser real (API+web rodando via `preview_start`) em sessão anterior.
  - Bug 4/5 confirmado via curl direto na API antes do fix (`GET /npcs/1` mostrava `city: 00000000-0000-0000-0000-000000000000`; `GET /visual/subscribe?scope=World` mostrava `cities:[{bounds:{width:34,height:24}}]` num mundo 20×20) — **não reconfirmado ao vivo depois do fix** (só via teste automatizado `ScenarioLoaderV2Tests`).
  - **Deliberadamente NÃO rodado nesta sessão** (decisão do usuário, não um esquecimento): a suíte ampla `Population|Cities|Periods|Visual|Geography|WorldCreate|WorldSnapshot|GoldenHashes` sem filtro de classe específica trava horas (achado real: pelo menos um teste dentro do padrão "Population" — não tagueado `Category=Scenario` — leva 12+ minutos sozinho, ver timestamps em `/tmp/targeted.log` desta sessão se ainda existir). O filtro por classe específica acima (81 testes, 1s) é o substituto usado. O gate de cenário de verdade (`Category=Scenario`) fica por conta do usuário, ver abaixo.

- **Processos auxiliares**: nenhum de pé ao final desta sessão (API de teste porta 5289 e qualquer `testhost` foram encerrados). Preview Vite (porta 5173) pode ter ficado de pé via `preview_start` — checar `tasklist | grep node` numa sessão nova se for reabrir o browser preview.

- **In-progress**: nenhum — bugs 3, 4 e 5 implementados, testados (filtro estreito) e prontos pra commit nesta mensagem.

- **Next step**:
  1. **Pedido explícito do usuário**: ele mesmo vai rodar o gate completo de cenários da API mais tarde — `bash scripts/test.sh --filter Category=Scenario` (repo root, Git Bash). Não pular nem tentar rodar por ele nesta sessão.
  2. Reconfirmar bug 4/5 ao vivo no browser (criar mundo, ver cidade do tamanho certo + população/NPCs visíveis) na próxima vez que o app rodar de verdade — só foi confirmado via `dotnet test`, não via UI, depois do fix do `Household.City`.
  3. Considerar (não pedido formalmente, só levantado pelo próprio usuário como possibilidade): botão de "Salvar" exigiria endpoint novo — não existe hoje.
  4. Rodar a suíte ampla (`Population|Cities|...`) inteira pelo menos uma vez antes do próximo fechamento de fase, fora do horário de sessão interativa (ela é lenta de verdade, não é só esta sessão) — considerar isolar/marcar como `Category=Scenario` o(s) teste(s) lento(s) achado(s) dentro do padrão "Population" se não estiverem devidamente categorizados.

- **Blockers**: nenhum.
- **Uncommitted files**: nenhum — tudo desta sessão vai commitado junto com esta atualização de STATE.md.
- **Branch**: main
