# STATE.md — memória entre sessões e handoff entre áreas

Fonte única de continuidade. Quem entra numa área nova **lê este arquivo primeiro**.
Não duplique conteúdo de `ROADMAP.md`, `AGENTS.md` ou `docs/` aqui — aponte.

## Handoff
- **Última coisa concluída (2026-09-01)**: Fase 28 fechada — cognição inspecionável, LOD
  observacional (mundo/cidade/interior), compressão de estado frio, painel web "ver o cérebro",
  sandbox de decisão. Branch `feat/phase-28-cognition` com gate verde; worktrees intermediárias
  `LivingWorld-28-t*` removidas; efêmeros spec-driven (tasks/validation de fases entregues,
  parallel-execution) limpos.
- **Próxima unidade**: merge de `feat/phase-28-cognition` em `main` (PR) ou iniciar próxima fase
  do roadmap conforme prioridade.
- **Última coisa concluída (2026-08-23)**: `dynamic-city-growth` FixT17 — causa raiz real da saga
  de hoje "cidades coladas/muralhas sobrepostas/população oscilando". `FoundingSitePicker.Pick`
  excluía a cidade-mãe de QUALQUER checagem de espaçamento (não só do gap `AbsorptionRingCells`) —
  como a mãe é quase sempre a única cidade próxima no instante da fundação, isso deixava o anel 1
  aceitar o primeiro candidato, produzindo bounds literalmente sobrepostos (confirmado ao vivo:
  mãe `Origin(4,4) 3x3`, filha `Origin(3,3) 3x3`). FixT8–FixT16 (mesmo dia) corrigiram sintomas
  reais mas secundários (clamp entre cidades, re-check no disparo, decline fora do mapa, guarda
  anti-poaching, histerese de migração, piso de tamanho mínimo) sem tocar essa causa raiz. Fix:
  mãe continua isenta do gap `AbsorptionRingCells`, mas não mais isenta de overlap de área — `Pick`
  agora rejeita qualquer candidato cujos bounds de fundação (população 0) sobreponham os bounds
  atuais da mãe. `bash scripts/test.sh --filter "Category!=Scenario&FullyQualifiedName~Cities"` —
  238 passed, 0 failed. Commit `6c335fa`. Detalhes:
  `.specs/features/dynamic-city-growth/tasks.md` (FixT17).
- **Última coisa concluída (2026-08-22)**: Fase 15.1 Stage 4 **T18–T28** (mapa + animações)
  + re-verify **PASS**. Prédios nas coords da API; andaime; pawn+overlays; rotas de
  migração; fundação visível; `NpcAnimationCatalog` + gate. Validação:
  `.specs/features/phase-15.1-stage-4-living-world/validation.md` (142 .NET + 402 Vitest
  Stage4; 3/3 sensores mortos). **Sem commit.** `verify.sh` não rodou (agente paralelo
  em testes History/Llm/etc.). UAT visual adiado (usuário ausente).
- **Adiado (T7)**: LWV-05.4 HUD de período — sem período canônico em `WorldState`.
- **Antes**: T12–T17 rest/água/comida/cultivo; T9–T11 commute/construção/migração.
- **Próxima unidade**: `bash scripts/verify.sh` quando o reparo de testes paralelo
  terminar; commit da stage; nightly `Category=Scenario` com o usuário.
- **Integração do extraordinário (2026-08-12)**: decisão **mista**, sem fase numerada nova
  para "super-heróis"/"identidade secreta" — distribuída entre Fases 16, 17, 23, 24, 25, com
  toques em 10, 12, 22. ADR-0010 + `docs/domain/powers.md`. Ver `ROADMAP.md` linhas 16/23.
  Gap-check fases 0–9: `BranchId` (Fase 3) e primitivo de resolução (Fase 0) já existiam.
- **Decisão de escopo (2026-07-29)**: **Fase 14 (Unreal) adiada**; Fase 15 é VTT 2D
  realtime (resolução por foco visual; tick global contínuo).
- **Dívida Fase 9**: PERF-12 hasher; `LongRunScaleTests` só nightly.
- **Gate**: `bash scripts/verify.sh`. No Windows, Git bash. Sem `Category=Scenario` no rotina.
- **População default ~40/100** após calibração econômica (Fase 5) — coerência, não "interessante".
- Fases 0–8 fechadas (git log, AD-020..048, ADR-0001..0007). Fases 16–27 em `spec` (AD-010).
- **Fase 13**: períodos = startpoints dinâmicos; autoria de catálogo sem runtime de IA interno.
- **Eval**: `verify.sh`; `verify-mutation.sh` manual. Harness: [`HARNESS.md`](HARNESS.md).
- **Budget**: nenhum loop autônomo ativo.

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
