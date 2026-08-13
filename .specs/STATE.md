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

- **Feature**: Fase 15.1 (VTT frontend redesign) fechada e validada (`.specs/features/phase-15.1-vtt-frontend-redesign/validation.md`, PASS). Sessão atual: bugfix pós-fechamento no fluxo de "criar mundo"/simulação ao vivo, relatado pelo usuário em 2 rodadas. **Fase ainda NÃO fechada de novo pelo usuário** — ele mesmo vai testar e rodar o gate de cenário antes disso.
- **Phase / Task**: Fora do fluxo `tlc-spec-driven` normal — bugfix direto, sem tasks.md próprio. Investigação com API+web reais rodando (curl direto + `preview_start`/`Claude_Browser`), não só leitura de código — 2 dos 3 root causes desta sessão só apareceram testando ao vivo.

- **Rodada 1 (bugs 1-2, commit `3aac17e`) — completa e confirmada ao vivo**:
  1. Cancelar criação não voltava ao menu, "abria um mundo" — `App.tsx`: `cancelCreatingWorld`/`hasEnteredWorldRef`.
  2. Nome digitado nunca chegava na API (`createWorld()` não enviava `name`) → sempre "Name é obrigatório"; "Começar" também passou a exigir nome preenchido.

- **Rodada 2 (bugs 3-4-5, commit `2776a2b`) — completa, testada por filtro estreito, NÃO confirmada ao vivo pelo usuário ainda**:
  3. Header mostrava "Criar mundo" já dentro de um mundo rodando — `App.tsx`: agora só existe a partir do `StartMenu`; dentro do jogo só "Cancelar" (durante criação) ou nada.
  4. **População/assentamentos nunca apareciam** (nem NPCs, nem cidade real) — `PopulationSeeder` sempre criava `Npc`/`Household` com `City = default`, invisíveis em toda projeção. Afetava 100% dos mundos criados (branco ou template), bug pré-existente à fase 15.1. Fix: `ScenarioLoaderV2.LoadWorld` funda/reaproveita uma cidade real em `population.Village` antes de semear, `CityId` propagado até `Npc`/`Household`.
     - **Regressão pega na própria verificação**: `PairIntoHouseholds` recebeu o `city` mas não repassava pro `new Household(...)` — `Npc.City` certo, `Household.City` ficava default. Corrigido no mesmo commit.
  5. Cidade renderizada maior que o mapa — `CityBoundsResolver` usava tamanho fixo 34×24 (herdado de um placeholder de grid local antigo).

- **Rodada 3 (bugs achados agora, POR COMMITAR) — 3 itens do usuário nesta mensagem**:
  - **5b — cidade AINDA maior que o mapa, confirmado ao vivo via curl** (`GET /visual/subscribe?scope=World` num mundo 20×20/pop 150 mostrou `bounds: {width:25,height:25}` — meu fix da rodada 2 escalava só por população, sem considerar o mapa: `sqrt(150)*2≈24.5`. **Fix**: `CityBoundsResolver.Resolve` ganhou `mapWidth`/`mapHeight`, multiplicador caiu de ×2 pra ×1, e o lado nunca excede `min(mapWidth, mapHeight)` — chamada em `GlobalProjector.cs` passa `world.Map.Width/Height`. Teste novo: `City_bounds_never_exceed_the_smaller_map_dimension...` reproduz exatamente o caso "Cidade média" (20×20, pop 150) que estourava.
  - **NPCs amontoados e parados, não se movem** — 2 causas, uma real bug crítico:
    - **Causa raiz confirmada**: `run.cmd` nunca setava `TICK_LOOP_ENABLED=true` — sem essa env var, `TickLoopService` nunca roda como `IHostedService` (só é ativado assim em `Program.cs:95`, desabilitado por padrão pra nenhum teste ganhar tick sozinho). Resultado: **o relógio da simulação NUNCA avança no app real, mesmo clicando Play** — só nos testes que chamam `RunOneCycle()` direto. **Fix**: `run.cmd` agora seta a env var na janela da API. Usuário precisa reabrir via `run.cmd` (ou setar a env var manualmente) pra ver efeito — a API antiga continua sem tick loop até reiniciar.
    - **Causa secundária, não é bug**: todo NPC seedado nasce na MESMA célula (`VillageX/VillageY`, um único ponto) — é assim que `PopulationSeeder`/`PopulationGenerator` sempre funcionaram, não é regressão desta sessão. Ficam "amontoados" até o tick loop rodar de verdade e sistemas de comportamento os moverem. Com o fix acima, isso deve resolver sozinho ao longo do tempo simulado — **não mexi na lógica de dispersão inicial**, só destravei o motor.
  - **"Continuar" sem indicar save real** — usuário pediu pra desabilitar já que "hoje não tem [salvar]". **Investigado, NÃO alterado**: hoje EXISTE persistência de fato — `Program.cs:40` (`worldRunner.LoadLatest()`) carrega o último snapshot salvo no SQLite ao subir a API, e `WorldCreateEndpoints` salva a cada `/worlds/create`. Ou seja "Continuar" já reconecta a um mundo persistido de verdade entre reinícios da API — não é um botão morto. **Decisão pendente do usuário**: ele quis dizer "não tem save manual/slots explícitos" (aí a leitura async é diferente — vale considerar UI melhor, ex. mostrar "última vez salvo" ou nome do mundo persistido) ou realmente achava que não persistia nada? Não desabilitei o botão porque a premissa ("hoje não tem") parece factualmente incorreta — perguntar antes de remover uma funcionalidade que já funciona.

- **Arquivos alterados nesta sessão, POR COMMITAR** (`git status` no momento da pausa):
  - `run.cmd` (TICK_LOOP_ENABLED=true)
  - `src/LivingWorld.Api/Visual/GlobalProjector.cs` (passa map width/height pro resolver)
  - `src/LivingWorld.Domain/Cities/CityBoundsResolver.cs` (cap por mapa)
  - `src/LivingWorld.Domain/Cities/SpatialBoundsResolver.cs` (idem, assinatura)
  - `tests/LivingWorld.Tests/Cities/BuildingFootprintAndPlacementTests.cs` (teste novo do cap por mapa)
  - `tests/LivingWorld.Tests/Geography/SpatialAddressAndScaleTests.cs` (assinatura nova)
  - `tests/LivingWorld.Tests/Visual/GlobalProjectorTests.cs` (assinatura nova)

- **Verificação feita nesta rodada**:
  - `dotnet build LivingWorld.sln` — verde.
  - `dotnet test --filter "ScenarioLoaderV2Tests|BuildingFootprintAndPlacement|SpatialAddressAndScale|GlobalProjectorTests|WorldCreateEndpointsTests|PopulationGeneratorTests|CityAndBuildingAuthoringTests"` — **82 passed, 0 failed**, ~2s (filtro estreito, por instrução explícita do usuário: **checar a tag antes de rodar testes de API, não rodar o que não precisa** — nenhum filtro amplo/sem tag foi rodado nesta rodada).
  - **NÃO rodado**: web (`vitest`/`tsc`) — nenhum arquivo `web/` mudou nesta rodada 3. `Category=Scenario` — fica com o usuário, conforme pedido dele.
  - **NÃO reconfirmado ao vivo no browser** — usuário disse que quer testar ele mesmo depois que eu terminar.

- **Processos auxiliares**: nenhum de pé ao final desta sessão (matei minha própria tentativa de subir a API na 5289; o processo do usuário, se algum, não foi tocado).

- **In-progress**: nenhum — tudo desta rodada implementado e testado (filtro estreito), pronto pra commit.

- **Next step**:
  1. Commitar os arquivos listados acima.
  2. **Usuário vai testar ao vivo** (reabrir via `run.cmd` atualizado — precisa da nova env var pra o tick loop rodar de verdade) e depois **rodar `bash scripts/test.sh --filter Category=Scenario`** ele mesmo.
  3. Decisão pendente (não bug): o que fazer com "Continuar" — hoje já persiste de verdade (single-slot automático via SQLite), perguntar se o usuário quer (a) manter como está, (b) UI indicando o que será continuado, ou (c) save slots múltiplos de verdade (feature nova).
  4. Rodar a suíte ampla (`Population|Cities|...`) inteira pelo menos uma vez antes do próximo fechamento de fase — fora de sessão interativa, achado real: ao menos um teste no padrão "Population" (não `Category=Scenario`) leva 12+ minutos sozinho.

- **Blockers**: nenhum.
- **Uncommitted files**: listados acima, prontos pra commit.
- **Branch**: main
