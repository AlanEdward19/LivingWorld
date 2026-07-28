# STATE.md — memória entre sessões e handoff entre áreas

Fonte única de continuidade. Quem entra numa área nova **lê este arquivo primeiro**.
Não duplique conteúdo de `ROADMAP.md`, `AGENTS.md` ou `docs/` aqui — aponte.

## Handoff
- **Última coisa concluída**: **Fase 7 Execute** — recalibração de `FamilyRules`/
  `ScenarioRunner.DefaultFamilyRules` (AD-064) + **T28–T30** em `FamilyPairedScenarioTests`
  (bootstrap `|r|` canal ambiental, distância ambiente/genoma, contrafactual rico/pobre vs
  genomas extremos). Golden hashes (`tests/golden/world-hashes.json`) regravados — mudança
  legítima de regra. Antes: T23–T26 (incesto ±, invariantes de nascimento, contagem FAM-26,
  baseline população), T20–T22 (coverage, deriva neutra, contrafactual household), T31 (sensor
  de hash).
- **T27 (FAM-32) fica BLOQUEADO** — tensão estrutural, não peso errado: ver AD-064 em
  `docs/decisions-log.md`. Fase 7 fecha com 30/31 tasks; T27 exigiria mudança de produção em
  `CourtshipSystem` (decompor `NeutralDriftEnabled`), fora do escopo de recalibração.
- **Próxima unidade**: decidir se Fase 7 fecha com T27 documentado como débito técnico
  (mesmo padrão de SKILL-11 na Fase 6) ou se abre uma task nova de produção para decompor
  `NeutralDriftEnabled`. Spec/tasks em `.specs/features/phase-07-family/{spec,design,tasks}.md`.
- **Gate local**: encerre `LivingWorld.Workers.exe` (PID pode travar build da solution) e rode
  `bash scripts/verify.sh` — nesta sessão o gate foi validado via `dotnet test` com DLLs
  copiadas para `tests/` (26 testes focados + golden OK após baseline novo).
- **Fase 9 nova (Escala e armazenamento)** — inserida depois da 8; antigas 9–26 viraram 10–27
  (AD-049, arquivos renomeados). Spec pronta em
  [.specs/features/phase-09-scale/spec.md](.specs/features/phase-09-scale/spec.md) (PERF-01..17,
  6 blocos), roadmap em [docs/roadmap/phase-09-scale.md](docs/roadmap/phase-09-scale.md).
  Medição que a motivou: custo por tick ≈ `0,12 µs × entidades + 0,3 µs × vivos` (paga por NPC
  morto), 150–320 B alocados por NPC-tick, snapshot JSON ~900 B/NPC com >50% mortos em 2 anos
  ⇒ 10k NPCs × 100 anos ≈ 1,4 h de CPU e ~35 GB. Falta Design/Tasks/Execute. **Cuidado**: o
  cenário default colapsa para ~130 vivos saindo de 1.000 ou 5.000 — medir escala nele mede NPC
  morto; PERF-01 pede cenário de escala com demografia estável.
- **Risco de balanceamento (STATE.md "Riscos ainda sem mecanismo") ficou mais concreto na Fase
  5**: população do cenário default estabiliza em ~40/100 NPCs (não extingue, mas bem abaixo do
  inicial) depois de calibração manual e empírica de salário/preço/capacidade — nenhum gate
  automatizado prova que esse patamar é "bom", só que as invariantes de conservação e a direção
  causal se sustentam nele. Fase 6/7 herdam essa base; se a população cair mais com novos
  sistemas, é o mesmo tipo de ajuste manual, não um bug de conservação.
- **Fase 4, Fase 3, Fase 2, Fase 1 e Fase 0 fechadas antes dela** — detalhe em git log, AD-020..037 e ADR-0001..0007.
- **Escopo extra especificado** (não iniciado): fases 16–27 em status `spec`. **Bloqueado até
  a Fase 8 fechar** (AD-010); cada fase tem `## Questões em aberto` (~60 perguntas de design).
  Injeta só `BranchId` na Fase 3 e o primitivo de resolução na Fase 0.
- **Eval gates disponíveis**: `bash scripts/verify.sh` = `check-docs` + `build` + `lint` +
  `test`, todos em 0. `bash scripts/verify-mutation.sh` prova que o gate reprova de
  verdade (3 mutantes do fixture) — não roda no `verify.sh` de rotina por ser caro
  (recompila o repo inteiro 3x em cópias temporárias); rode manualmente quando mexer no
  próprio gate.
- **Harness**: [`HARNESS.md`](HARNESS.md) gerado — inventário dos 4 componentes, controles por
  categoria e eval gates disponíveis (`scripts/verify.sh` gate principal, `--filter
  Category=Scenario` e `verify-mutation.sh` manuais/caros — rodar cenário longo só quando
  necessário, é lento). Liberado spec-driven + loop para Fase 5.
- **Budget/limites**: nenhum loop autônomo ativo.

## Decisões (AD-NNN)
Log completo em [docs/decisions-log.md](docs/decisions-log.md) — AD-001..048 (não duplique
aqui; a Fase 5 registrou AD-039..048, ver lá).

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
| Explosão de custo de memória e log | Log em dois tiers desde a Fase 3 + **cânone limitado por comunidade**, que torna o custo independente do tempo (ADR-0007). Sensor de bytes/NPC/ano na Fase 3, seis fases antes de doer; teto por NPC **vivo** (não por NPC já existido) e snapshot delta/binário na Fase 9 | 3, 9, 10 |
| SQLite travar escrita concorrente | Interceptor exige **zero round-trips durante o tick** — se o banco só é tocado no snapshot, trocar de provider é trocar string de conexão | 3 |
| Cliente web atrasar o objetivo #1 | Reordenado: Fase 2 é geografia-dados, mapa visual virou Fase 15 (AD-007) | 2, 15 |
| Conteúdo medieval calcificar em `src/` | Cenário `test-scifi` rodando no gate **desde a Fase 3** — se um cenário alienígena roda cedo, o medieval não enraíza. Elimina a dívida que a Fase 13 carregava | 3 |
| Estado órfão entre subsistemas | Sweep de integridade referencial genérico, dirigido por reflexão sobre todos os tipos de ID — ganha cobertura sozinho a cada fase nova | 3 |
| Verdade histórica vazar para o mundo do jogo | Consultas separadas (verdade vs crença) com teste de mutação: desligar a checagem tem de reprovar o critério | 10, 11 |

## Riscos ainda sem mecanismo
| Risco | Por que segue aberto |
|---|---|
| Custo e latência de LLM em escala | Só dimensionável com provider real; decisão adiada para ADR-0008 na Fase 11 |
| Balanceamento do mundo (economia estável, demografia plausível) | Nenhum gate prova que o mundo é *interessante*, só que é *coerente*. Exige julgamento humano |
| **Escopo extra canibalizar o caminho crítico** | Mitigado por AD-010 (fases 16–19 bloqueadas até a 8), mas o risco é de disciplina, não de mecanismo. Nenhum gate impede alguém de começar a Fase 18 antes da hora |
| Ramificação pura não entregar payoff narrativo | Mudar o passado não afeta a linha de origem (ADR-0008). O valor tem de vir de conhecimento atravessando linhas, perda permanente e comparação — nada disso está provado ainda |
| Potência genérica não caber em alguma fantasia | ADR-0010 aposta num modelo só. Se uma categoria não couber, a saída é esticar o modelo, não forkar — e isso pode ficar feio |
| Catch-up longo estourar a paciência do jogador | ADR-0012 dá o mecanismo, não o orçamento. Voltar a um branch parado há 40 anos pode custar minutos. Pré-aquecimento é hipótese, não solução provada |
| Emergência aberta produzir coisa implausível | ADR-0013 garante que o emergente é determinístico e composto, não que é *bom*. Nenhum gate distingue tecnologia plausível de monstro estatístico |
| 28 fases | O escopo agora é muito maior que o objetivo #1. As trilhas (AD-012) organizam, mas não reduzem. Risco real é diluir esforço antes de fechar o núcleo |
| Admin distraído congela todos | Pausa global é decisão (ADR-0015) e o poder é real. Limite de duração ficou como questão em aberto da Fase 26, sem mecanismo ainda |
| Fidelidade perdida é perdida | Aceitar `LOD` como definição do mundo (ADR-0012) significa que um período que ninguém observou nunca pode ser ampliado. Coerente e irreversível — se incomodar depois, o custo é alto |
