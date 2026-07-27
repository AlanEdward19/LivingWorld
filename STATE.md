# STATE.md — memória entre sessões e handoff entre áreas

Fonte única de continuidade. Quem entra numa área nova **lê este arquivo primeiro**.
Não duplique conteúdo de `ROADMAP.md`, `AGENTS.md` ou `docs/` aqui — aponte.

## Handoff
- **Última coisa concluída**: **Fase 3B (persistência, tasks 8-14) fechada** — ver AD-028..031 e
  detalhe em git log. `BranchId` em `WorldState`/hash (ADR-0009); `IWorldEventSink` loga
  nascimento/morte sem tocar `WorldState`; `LivingWorld.Infrastructure` ganhou EF Core+SQLite
  (`WorldDbContext`, `IWorldRepository`/`SqliteWorldRepository`, migração `InitialCreate`,
  branch sempre explícito); `PersistentWorldRunner` (snapshot+log na fronteira, zero round-trips
  provado por arquitetura + interceptor); `ReferentialIntegritySweep` por reflexão (achou e
  corrigiu bug real de `Npc.Household` órfão pós-dissolução, ver AD-031); `MonotonicFields`;
  sensor de bytes/NPC/ano. Golden hashes regenerados. ~30 testes novos, gate verde.
- **Próxima unidade**: **Fase 4 (Necessidades e rotina)** — fome, sono, trabalho, moradia,
  deslocamento, utility AI. Ver [docs/roadmap/phase-04-needs.md](docs/roadmap/phase-04-needs.md).
- **Fase 2, Fase 1 e Fase 0 fechadas antes dela** — detalhe em git log, AD-020..023 e ADR-0001..0007.
- **Escopo extra especificado** (não iniciado): fases 15–26 em status `spec`. **Bloqueado até
  a Fase 8 fechar** (AD-010); cada fase tem `## Questões em aberto` (~60 perguntas de design).
  Injeta só `BranchId` na Fase 3 e o primitivo de resolução na Fase 0.
- **Eval gates disponíveis**: `bash scripts/verify.sh` = `check-docs` + `build` + `lint` +
  `test`, todos em 0. `bash scripts/verify-mutation.sh` prova que o gate reprova de
  verdade (3 mutantes do fixture) — não roda no `verify.sh` de rotina por ser caro
  (recompila o repo inteiro 3x em cópias temporárias); rode manualmente quando mexer no
  próprio gate.
- **Harness**: `HARNESS.md` ainda não existe. Gerar via `harness-engineering` agora que há
  código real para sensorear.
- **Budget/limites**: nenhum loop autônomo ativo.

## Decisões (AD-NNN)
Decisão de **arquitetura ou dependência** vira ADR em `docs/adr/`. Aqui ficam as decisões
de **processo e escopo** que não justificam um ADR.

| ID | Data | Decisão | Motivo |
|---|---|---|---|
| AD-001 | 2026-07-26 | Stack .NET 10 / C# | Casa com a arquitetura do escopo e com o alvo Unreal; performance suficiente para 100k+ NPCs. Detalhe em ADR-0001. |
| AD-002 | 2026-07-26 | Bootstrap e roadmap primeiro, implementação só sob comando | Escopo de 13 fases; sem gates e sem objetivo verificável não se libera autonomia. |
| AD-003 | 2026-07-26 | Fase 0 (Fundação) inserida antes da Fase 1 do escopo original | O motor de tempo precisa de solution, fronteiras compiladas e gate verde para ter onde apoiar. |
| AD-004 | 2026-07-26 | Commit automático ao fim de cada fase, só com `verify.sh` em 0 | Preferência do usuário; reduz interrupção sem perder o gate. |
| AD-005 | 2026-07-26 | Teto de 100 linhas por `.md`, imposto por `scripts/check-docs.sh` | Sensor computacional é mais barato e mais confiável que disciplina; protege o progressive loading. |
| AD-006 | 2026-07-26 | Critério de fase segue `rules/eval-criteria.md`; sem limiar sem procedência | Auditoria adversarial reprovou ~50 critérios em 5 classes (tautológico, mágico, sem controle, exige humano, caro demais). |
| AD-007 | 2026-07-26 | Fase 2 vira geografia-dados; cliente web React sai do caminho crítico e vira Fase 14 | O objetivo #1 não precisa de mapa e o objetivo #2 é atendido por CLI/API na Fase 8. Tira npm, OpenAPI e um segundo toolchain do caminho crítico. |
| AD-008 | 2026-07-26 | Log em dois tiers e sensor de bytes/NPC/ano descem da Fase 9 para a Fase 3 | Na Fase 9 o formato do log já está congelado; a decisão de retenção precisa nascer com o log. |
| AD-009 | 2026-07-26 | História vira relato degradável, não log comprimido (ADR-0007) | Ideia do usuário: história real também não é fiel. Resolve o custo de armazenamento e o transforma em feature em vez de dívida. |
| AD-010 | 2026-07-26 | Escopo extra (potência, divindade, tempo, cosmos) entra como **spec**, fases 15–18 bloqueadas até a Fase 8 fechar | Protege o caminho crítico 0–8. Escopo grande antes do objetivo #1 é o jeito mais confiável de nunca fechar o objetivo #1. |
| AD-011 | 2026-07-26 | Universo limitado a **sistema estelar** nesta rodada | Suficiente para alien chegar, colônia existir e céu ter consequência. Galáxia e multiverso ficam fora até haver necessidade real. |
| AD-012 | 2026-07-26 | Roadmap passa a ter **trilhas**; o número da fase é identidade, não ordem | Com 27 fases uma lista linear mente sobre as dependências. Só a trilha núcleo (0–8) é sequencial. |
| AD-013 | 2026-07-26 | Ontogenia é **modelo de desenvolvimento**, não machine learning | ML por NPC seria caro e não-determinístico. Curva por marco + exposição produz o mesmo comportamento observável, determinístico e barato. |
| AD-014 | 2026-07-26 | Pausa é **global e só de admin**; jogadores veem quem pausou | Decisão do usuário. Pausa por sessão exigiria um scheduler por jogador, ou seja, um mundo por jogador — deixa de ser mundo compartilhado. |
| AD-015 | 2026-07-26 | Quatro conflitos entre fases resolvidos por ADR-0014 e ADR-0015, mais emendas em ADR-0012 e ADR-0013 | Três dos quatro eram o mesmo problema: faltava o **critério** de canônico vs. volátil, não a classificação. |
| AD-016 | 2026-07-26 | `LivingWorld.sln` gerado com `-f sln` em vez do default `.slnx` do dotnet 10 | Scripts do gate já referenciam `LivingWorld.sln` pelo nome; `.slnx` quebraria os 3 scripts sem ganho nenhum na Fase 0. |
| AD-017 | 2026-07-26 | `NuGet.config` do repo restrito a `nuget.org` | Feed corporativo (`BDS`) do NuGet global exige auth que não está disponível neste ambiente; sem isolar, todo restore falhava com 401. Config global do usuário não foi tocada. |
| AD-018 | 2026-07-26 | Critério do CS0246 na Fase 0 corrigido para CS0234 (`docs/roadmap/phase-00-foundation.md`) | `LivingWorld` já existe como namespace via `Domain`; falta só o sub-namespace `AI`, que o Roslyn reporta como CS0234, não CS0246. Erro de redação do critério original, não mudança de escopo. |
| AD-019 | 2026-07-26 | `InMemoryCompiler` de teste filtra assemblies `LivingWorld.*` de `TRUSTED_PLATFORM_ASSEMBLIES` | O host de teste inclui todas as `ProjectReference` de `LivingWorld.Tests` (inclusive `AI`) nos "trusted platform assemblies"; sem filtrar, o fixture de fronteira Simulation↔AI compilava com sucesso mesmo sem a referência explícita, e o teste não media nada. |
| AD-020 | 2026-07-26 | Teste de determinismo entre 2 processos reaproveita `LivingWorld.Workers` num modo CLI (`hash <seed> <ticks>`) em vez de criar um projeto console novo | `Workers` já referencia `Simulation`/`Domain` e já é permitido referenciar tudo (rules/implementation.md); projeto novo seria decisão de arquitetura sem ganho — o objetivo é só ter um segundo processo real, não um host novo. |
| AD-021 | 2026-07-26 | Tick do motor é sempre 1 hora; granularidade "diária" de um cenário de teste vem de `HoursPerDay = 1` no `WorldCalendar`, não de uma unidade de tick alternativa | Mantém `WorldClock` com uma única noção de tick (simples, sem branch de unidade); o critério "Yearly roda 10x em 3650 ticks diários" é satisfeito configurando o calendário do cenário de teste, não o motor. |
| AD-022 | 2026-07-26 | Streams de RNG são derivados uma única vez de uma raiz imutável (`WorldRngRegistry`) e persistem por toda a run | Se a chave derivasse de uma raiz que também avança, o resultado dependeria de quantas vezes a raiz foi consumida antes — quebra "adicionar um sistema não desloca os outros" (ADR-0005). |
| AD-023 | 2026-07-26 | Catálogo de geografia (`GeographyCatalog`) guarda só ids válidos de terreno/bioma/recurso — nenhum campo de nome/apresentação | O motor nunca consome nome, só id e peso de custo; nome e apresentação são dado de cliente (Fase 14). Simplifica o critério "nenhum literal de terreno/bioma em C#" a "nenhum nome existe no modelo", em vez de banir strings que o engine teria que guardar sem usar. |
| AD-024 | 2026-07-26 | `NpcId`/`HouseholdId` viram wrapper de `long` monotônico (não `Guid`) | `Guid.NewGuid()` é banido em Domain/Simulation; id precisa nascer determinístico, padrão de `NextEventId`. `CityId`/`LocationId` (ainda não usados) seguem `Guid` até a Fase 8 os consumir de verdade. |
| AD-025 | 2026-07-26 | `PopulationCatalog` ganha `ProfessionType`/`LocationType` desde a Fase 3, mesmo sem `Npc` ter profissão ainda | Mesmo raciocínio de `BranchId` (ADR-0009): pagar agora é um record; retrofitar depois da Fase 5/6 já terem profissão real seria migração de catálogo. `test-scifi` já declara piloto/técnico como id. |
| AD-026 | 2026-07-26 | Propriedade pública computada (`Household.IsEmpty`, `Npc.IsAlive`) leva `[JsonIgnore]` | Sem isso o snapshot serializa um `bool` solto e o mutador genérico do teste canônico/volátil (só sabe long/int/string) devolve nó já anexado ao pai — `System.Text.Json` reprova. Mesmo motivo de `Npc.AgeYears` ser método, não propriedade. |
| AD-027 | 2026-07-26 | Cenário (mapa+população) mora num JSON em `scenarios/*.json` via `ScenarioLoader`; o "default" do gate continua hardcoded em `ScenarioRunner` | Padrão da Fase 2: `MapScenarioLoader` existe e é testado sem estar no caminho do golden hash. `test-scifi.json` prova que o motor lê população de arquivo sem o gate pagar I/O a cada seed. |
| AD-028 | 2026-07-27 | Log de história (Tier A) não vive em `WorldState`/hash — é um buffer em memória (`IWorldEventSink`) drenado só na fronteira de snapshot | Guardar eventos pendentes como campo canônico/volátil de `WorldState` forçaria classificar um buffer que se esvazia a cada save; ficar fora do hash mantém o motor alheio a persistência (Simulation nunca referencia Infrastructure) e prova "zero round-trips" por construção, não só por teste. |
| AD-029 | 2026-07-27 | `EventLogRecord` usa chave composta `(BranchId, Tick, Sequence)` com `Sequence` atribuído pelo repositório, não `RowId` autoincrementado pelo banco | Migração gerada por EF anotava `Sqlite:Autoincrement` — exatamente o recurso exclusivo de SQLite que ADR-0002 proíbe. Sequence determinística no app é portável para Postgres sem migração nova. |
| AD-030 | 2026-07-27 | `CityId`/`LocationId` entram no sweep referencial com resolver que devolve conjunto vazio | Ainda não usados (AD-024) — vazio + nenhuma referência passa por vacuidade; quando a Fase 8 os usar de verdade, o resolver troca para o conjunto real e o teste de cobertura já força isso a acontecer (não deixa passar silenciosamente). |
| AD-031 | 2026-07-27 | `Npc.Household` de um NPC morto é limpo (`LeaveHousehold()`) quando o household dissolve, mas preservado enquanto o household ainda existir | O sweep referencial (task 12) achou o bug: household dissolvido deixava `Npc.Household` de ex-membros mortos apontando para id removido. Preservar a referência enquanto o household existe mantém "onde morava" como dado histórico válido; só limpa no momento exato em que deixa de resolver. |

## Fases
Status por fase vive na tabela do [ROADMAP.md](ROADMAP.md). Não replique aqui.

## Riscos — mitigação aplicada
Cada linha aponta o mecanismo **estrutural** que impede o risco, não o teste que o detecta
depois. Onde o mecanismo já virou task, a fase está nomeada.

| Risco | Mecanismo que o impede | Fase |
|---|---|---|
| Determinismo quebrar silenciosamente | `BannedApiAnalyzers` torna `Random`/`DateTime.Now` **erro de compilação**; determinismo verificado em **dois processos** (pega ordem de `Dictionary`); golden hashes versionados tornam a quebra visível no diff | 0, 1 |
| Hash não cobrir o estado que ele deveria proteger | Hash partido em canônico/volátil + teste **gerado por reflexão**: campo não classificado reprova | 1 |
| Tick não terminar (evento que se re-agenda, decisão que não converge) | Teto declarado de iterações por tick, com abort nomeando o sistema culpado | 1, 4 |
| Genética virar destino | Peso `w_gene` é parâmetro declarado do cenário e auditável; controle de **deriva neutra** substitui limiar mágico; teste contrafactual de household rico vs pobre | 7 |
| Explosão de custo de memória e log | Log em dois tiers desde a Fase 3 + **cânone limitado por comunidade**, que torna o custo independente do tempo (ADR-0007). Sensor de bytes/NPC/ano na Fase 3, seis fases antes de doer | 3, 9 |
| SQLite travar escrita concorrente | Interceptor exige **zero round-trips durante o tick** — se o banco só é tocado no snapshot, trocar de provider é trocar string de conexão | 3 |
| Cliente web atrasar o objetivo #1 | Reordenado: Fase 2 é geografia-dados, mapa visual virou Fase 14 (AD-007) | 2, 14 |
| Conteúdo medieval calcificar em `src/` | Cenário `test-scifi` rodando no gate **desde a Fase 3** — se um cenário alienígena roda cedo, o medieval não enraíza. Elimina a dívida que a Fase 12 carregava | 3 |
| Estado órfão entre subsistemas | Sweep de integridade referencial genérico, dirigido por reflexão sobre todos os tipos de ID — ganha cobertura sozinho a cada fase nova | 3 |
| Verdade histórica vazar para o mundo do jogo | Consultas separadas (verdade vs crença) com teste de mutação: desligar a checagem tem de reprovar o critério | 9, 10 |

## Riscos ainda sem mecanismo
| Risco | Por que segue aberto |
|---|---|
| Custo e latência de LLM em escala | Só dimensionável com provider real; decisão adiada para ADR-0008 na Fase 10 |
| Balanceamento do mundo (economia estável, demografia plausível) | Nenhum gate prova que o mundo é *interessante*, só que é *coerente*. Exige julgamento humano |
| **Escopo extra canibalizar o caminho crítico** | Mitigado por AD-010 (fases 15–18 bloqueadas até a 8), mas o risco é de disciplina, não de mecanismo. Nenhum gate impede alguém de começar a Fase 17 antes da hora |
| Ramificação pura não entregar payoff narrativo | Mudar o passado não afeta a linha de origem (ADR-0008). O valor tem de vir de conhecimento atravessando linhas, perda permanente e comparação — nada disso está provado ainda |
| Potência genérica não caber em alguma fantasia | ADR-0010 aposta num modelo só. Se uma categoria não couber, a saída é esticar o modelo, não forkar — e isso pode ficar feio |
| Catch-up longo estourar a paciência do jogador | ADR-0012 dá o mecanismo, não o orçamento. Voltar a um branch parado há 40 anos pode custar minutos. Pré-aquecimento é hipótese, não solução provada |
| Emergência aberta produzir coisa implausível | ADR-0013 garante que o emergente é determinístico e composto, não que é *bom*. Nenhum gate distingue tecnologia plausível de monstro estatístico |
| 27 fases | O escopo agora é muito maior que o objetivo #1. As trilhas (AD-012) organizam, mas não reduzem. Risco real é diluir esforço antes de fechar o núcleo |
| Admin distraído congela todos | Pausa global é decisão (ADR-0015) e o poder é real. Limite de duração ficou como questão em aberto da Fase 25, sem mecanismo ainda |
| Fidelidade perdida é perdida | Aceitar `LOD` como definição do mundo (ADR-0012) significa que um período que ninguém observou nunca pode ser ampliado. Coerente e irreversível — se incomodar depois, o custo é alto |
