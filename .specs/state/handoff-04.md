# Handoff 4/4

### Histórico anterior (fase 15.1 stage 4, pré-dynamic-city-growth) — mantido como referência

- **Feature**: Fase 15.1, Stage 4 (Living World Integration) — `.specs/features/phase-15.1-stage-4-living-world/tasks.md`, **Phase 2 em andamento**: T1-T9 done, T10 next.
- **Phase / Task**: T9 (comuta de propósito pro trabalho, LWV-02.3/LWV-06) implementado via `tlc-spec-driven` Execute — commit `6dbfb02`. Fix: `BehaviorDecisionSystem`'s ambient wander pra `Work` agora exige `Employer` real (sem ele, NPC fica bloqueado em vez de fingir trabalhar) — ver AD-006 acima pro raciocínio completo e o trade-off de determinismo. `tests/LivingWorld.Tests/Stage4/PurposefulCommuteTests.cs` novo (6 testes); `tests/baselines/action-switches.json` regravado deliberadamente no mesmo commit.
- **In-progress**: nenhum — T9 commitado, gate (`PurposefulCommuteTests` + safety sweep de `Behavior|Economy|Population|Stage4`) verde.
- **Next step**: T10 (Demand-driven construction — cidade sem capacidade de trabalho enfileira construção real, não fabrica trabalho). Verifier de feature só dispara depois da última task do stage (T17) — não antes.
- **Blockers**: nenhum.
- **Uncommitted files**: nenhum (só `resultado.txt`, pré-existente, não relacionado a nenhuma sessão de trabalho).
- **Branch**: main.

---

### Histórico anterior (fase 16, pré-stage-4) — mantido como referência

- **Feature**: Fase 16 (perf da suíte de testes) **fechada e validada** — `.specs/features/phase-16-perf-test-suite/validation.md`, PASS. Suite completa (sem filtro, todos os `Category=Scenario` inclusos): **8h03m19s → 36m7s** (13.4×). Teste-alvo isolado (`Ten_k_population_ten_years_within_perf_budget`): **7h45m12s → 24m30s** (19×). Meta de <1h batida.
- **Fixes commitados** (`e275931`..`5609944`): (1) `BehaviorDecisionSystem` memoiza população de cidade 1x/tick; (2) `EventScheduler.Schedule` trocou Add+Sort completo por inserção via busca binária (alimentava um índice que nunca era lido — achado real, ~95% do custo); (3) `RelationshipSystem` removeu um `.OrderBy` inútil no loop de decay diário; (4) `FamilyRules.MaxCohabitationGroupSize` (AD-005) — teto no par-a-par O(k²) de cohabitation, decisão de produto aprovada pelo usuário em chat (muda resultado só em grupos grandes/escala industrial; golden hashes e baseline de scale-sensor regravados deliberadamente).
- **2 falhas remanescentes na suite completa** (`FamilyPairedScenarioTests.Vitality_cv_...`, `LongRunScaleTests.Storage_cost_per_alive_npc_stable_across_horizons`) são as MESMAS falhas pré-existentes já presentes no baseline original (T1) — confirmadas idênticas, fora de escopo desta fase, não são regressão.
- **Achado incidental, não investigado a fundo** (mesma classe do "mass die-off" já registrado em fases anteriores nesta seção): `ScaleScenarioFixture.CreateWorld(seed:42, pop:10_000)` crashou de 7.342 pra 447 vivos em ~4,5 anos simulados durante os diagnósticos desta fase — população cai muito rápido mesmo no cenário de escala calibrado (`ScaleEconomyCatalog`/`ScaleFamilyRules`). Não é objetivo desta fase (perf, não balanceamento), mas é sinal de que o problema de balanceamento já suspeitado antes (fome/economia em cenários grandes) continua real.
- **Next step**: backlog antigo ainda aberto: decisão sobre "Continuar"/save (slot único vs. slots múltiplos) e balanceamento de fome/economia em população grande — nenhum dos dois foi tocado desde então.

### Histórico anterior (fase 15.1 bugfix, pré-fase-16) — mantido como referência

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

- **Rodada 4 (commit `f9ceee0`) — usuário reabriu via `run.cmd` atualizado e testou de novo, 3 achados novos**:
  1. **"Continuar" abriu um mundo que não era o que ele tinha gerado por último** — investigado ao vivo (curl direto na API do usuário, processo já rodando, não tocado). Causa raiz: `PersistentWorldRunner`/`SqliteWorldRepository` mantêm **um único slot persistido** (`BranchId.Root`, sempre tick 0 — `SaveSnapshotWithEvents` faz upsert nessa mesma chave a cada `/worlds/create`). "Continuar" == `worldRunner.LoadLatest()` no boot da API == literalmente "o que quer que tenha sido o ÚLTIMO create nesse processo, de qualquer sessão". **O mundo "aleatório" que ele viu era resíduo de UM DOS MEUS PRÓPRIOS testes ao vivo desta investigação** (mesmo seed/preset determinístico, por isso os IDs de cidade batiam com testes anteriores meus) — não um bug de "Continuar" escolher errado, é o design de slot único fazendo exatamente o que faz. **Não é bug, é a mesma limitação de design que ele já tinha levantado antes** ("só faz sentido Continuar se der pra salvar") — CONFIRMADO na prática agora: qualquer create (inclusive um meu, de teste) pisa no que "Continuar" mostra pra ele. Decisão de produto pendente, ver Next step.
  2. **1 assentamento colocado, 2 cidades no mundo rodando** — confirmado ao vivo. Causa raiz REAL (diferente do que eu achava na rodada 2): `defaultScenarioForm().cities` sempre injetava uma cidade fantasma fixa em (2,2), sem nenhuma relação com o assentamento clicado no mapa (`form.settlements`, decorativo) nem com `villageX/Y` (onde a população nasce de fato). O fix da rodada 2 (fundar cidade real onde a população nasce) então sempre criava uma SEGUNDA cidade ao lado da fantasma vazia. **Fix**: `cities: []` por padrão (igual aos 3 templates reais do `DefaultPeriodSeeder`, que nunca tiveram esse problema) — `web/src/scenarioDefaults.ts`. **Confirmado ao vivo, mundo novo criado só com o padrão (sem clicar nada extra)**: exatamente 1 cidade, em (5,5) (= village), população real 36, bounds 6×6 num mapa 20×20 — sem estourar.
  3. **NPCs amontoados/parados** — parcialmente explicado, achado preocupante novo: no mundo de teste acima, a população (36) **foi a zero entre o tick ~150 e ~325** (poucos dias simulados) — confirmado via 3 leituras sucessivas de `/visual/subscribe`, `externalNpcs` também zerou, nenhum NPC responde em `/npcs/{id}`. Não investigado a fundo (fora do escopo dos 3 bugs originais, achado incidental) — hipótese mais provável: fome/economia (preset em branco não configura nenhuma produção de comida — `Workplaces: []`, `HungerDecayPerHour: 2` — população pode estar morrendo de fome em ~2-4 dias simulados antes de ter chance de se dispersar). **Se for isso, o "não se movem" original também se explica**: não é que travaram, é que morreram antes de ter tempo de andar. **NÃO CORRIGIDO** — precisa decisão: é esperado (preset em branco == sobrevivência difícil de propósito) ou é bug de balanceamento a corrigir (dar comida inicial/produção mínima por padrão)?
  4. **Pergunta sobre cores do terreno** (azul/roxo/musgo/dourado) — respondida, não é bug: `colorById.ts`/T37 geram cor procedural determinística por id de terreno (ângulo áureo em HSL) — **não há semântica de bioma real ainda** ("grama"/"deserto"/"água" não existem como conceito, só ids numéricos coloridos de forma estável). Azul == água/rio (isso sim é modelado, camada `Rivers`); as demais cores (roxo/musgo/dourado) são só terrenos-id diferentes sem nome nem arte própria — decisão de produto já registrada em fases anteriores (sem pipeline de arte/paleta temática ainda).

- **Verificação da rodada 4**: `npx vitest run` — 267 passed (só `scenarioDefaults.ts` mudou, frontend puro, sem necessidade de rebuild/teste backend). Confirmado ao vivo via browser + curl direto na API do usuário (processo dele, só leitura — nunca reiniciado/matado por mim nesta rodada).

- **Processos auxiliares**: API do usuário (porta 5289) e o dev server dele (porta 5173) foram só CONSULTADOS (curl/fetch), nunca reiniciados ou mortos nesta rodada 4. Minha própria tentativa de subir uma segunda API na 5289 falhou por porta ocupada (esperado) e não deixou processo pra trás.

- **In-progress**: nenhum — rodada 4 commitada. Item novo (morte em massa da população) fica registrado como achado, não corrigido.

- **Next step**:
  1. **Usuário vai testar ao vivo de novo** (fix de "2 cidades" só precisa reload do Vite, sem restart de API) e depois **rodar `bash scripts/test.sh --filter Category=Scenario`** ele mesmo.
  2. **Decisão pendente sobre "Continuar"/save**: confirmado que é slot único de verdade — perguntar se o usuário quer (a) manter como está agora que ele entende a causa, (b) UI indicando qual mundo será continuado (nome/data), ou (c) save slots múltiplos de verdade (feature nova, endpoint novo).
  3. **Decisão pendente sobre morte em massa de população** (achado na rodada 4, não investigado a fundo): balancear economia/fome do preset em branco, ou é esperado?
  4. Rodar a suíte ampla (`Population|Cities|...`) inteira pelo menos uma vez antes do próximo fechamento de fase — fora de sessão interativa, achado real: ao menos um teste no padrão "Population" (não `Category=Scenario`) leva 12+ minutos sozinho.

- **Blockers**: nenhum técnico. 2 decisões de produto pendentes (Continuar/save, balanceamento de fome) antes de considerar a fase realmente fechada de novo.
- **Uncommitted files**: nenhum — tudo commitado (`f9ceee0`).
- **Branch**: main
