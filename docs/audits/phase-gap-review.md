# Revisão de fases fechadas — lacunas (2026-10-11)

Auditoria estática (sem rodar testes) das fases `fechada`: 0–9, 11, 13, 28. Cada item
abaixo é lacuna real vs. `docs/roadmap/phase-NN-*.md`. Fechadas nesta branch: **[x]**.
Itens que exigem decisão de escopo/arquitetura (AGENTS.md → "PERGUNTE QUANDO") ficam em **D**.

> **Antes de "corrigir" qualquer item que toque comportamento do NPC, economia, população ou testes de escala, leia a
> seção "Decisões já tomadas" abaixo.** Vários critérios "mais fracos" são consequência de mudanças deliberadas
> (AD-077..088) e reverter o código para casar com o roadmap quebraria a vila.

## Fechadas nesta branch
- [x] F2 — `Cities[]` fora do grid rejeitada no load (`CityScenarioLoader`).
- [x] F3 — hash canônico distinto por `BranchId`; teste de reflexão: todo método de `IWorldRepository` recebe `BranchId`.
- [x] F11 — `LlmResponseValidator` rejeita `Intent` fora de `AllowedIntents`.
- [x] F11 — tick imune a provider que cai: hash igual com e sem o turno (`ConversationOrchestratorTests`).
- [x] F3 — nenhum evento de emprego/casamento/cortejo após a morte (`NoEventsAfterDeathTests`); mundo vazio por 1000 ticks tem hash estável e reproduzível.
- [x] F8 — todo NPC vivo numa cidade existente a cada dia, 1 ano (`EveryNpcHasACityTests`).
- [x] F2 — hash de mapa: mesma seed igual, seed distinta difere, literal preso da seed 42 (`MapHashTests`).
- [x] Pins de `OptimizationEquivalenceTests` regravados (seeds 1 e 2): o hash atual é idêntico em 4a86dbf (commit que gravou as pins) e no HEAD, em Debug e Release, então as pins nunca bateram nesta máquina; determinismo em 2 processos segue verde.

## Fechadas na segunda rodada (AD-089, medidas antes/depois)
- [x] F4 sede/exaustão têm consequência (morte por sede; saúde por privação de sono). Social em 0 segue sem consequência.
- [x] F6 tutoria roda de verdade (mestre por oficina, formatura, treino de `TeachingSkill`).
- [x] F7 evento Help e eixos Respeito/Dívida; Trade e Betrayal seguem sem fonte (mercado é NPC↔oficina; traição é Fase 23).
- [x] F1 `ExampleCounterSystem` fora da produção. [x] F11 expiração de sessão sem id canônico.
- Perf igual (0,642 → 0,617 µs/NPC-tick, ruído ±0,04; alocação e snapshot idênticos); vila −3,6% no ano 10.
- Não feito (precisa spec): madeira/pedra/profissões, Trade no log, snapshot binário real.

## Abertas — critério mais fraco que o roadmap (sem AD registrando o desvio)
- F4 histerese 20/20 virou média+folga; teto p99 tautológico (`BehaviorDecisionSystemHysteresisTests`).
- F5 preço sob escassez: "todo tick" → "nunca menor e maior algum dia" (`ScarcityPriceCausalTests`).
- F5 salário não pago: pagamento parcial, não "byte-idêntico".
- F3–F5 gates de 10 anos/todo tick → 1 mês/amostragem diária; conservação em 20 NPCs.
- F7 FAM-32/33/34/35: critério reescrito; `OverlapCoefficient` sem chamador; baseline só de 1 valor.
- F6 testes "20 seeds" vácuos (nenhum sistema de skill lê RNG); herança de skill tautológica.

## Abertas — entregue como stub (sem consumidor causal)
- F0 perfis Agregado/Raro; perfis de variância não vêm do cenário.
- F1 `ExampleCounterSystem` ainda em produção e no hash; calendário/teto de iterações não declarados no cenário.
- F2 `NeighborsOf`/`CellsOf` sem consumidor.
- F4 sede/sono/social em 0 sem consequência (só fome).
- F5 ferro sem consumidor; receitas sem inputs; `Mint/Destroy` sem sistema (imposto/tesouro); transações fora do event log.
- F6 `AssignMentor` sem chamador (tutoria nunca roda); qualidade não entra em preço; sem espiral oficina/aprendiz.
- F7 eventos Help/Betrayal/Trade e eixos Respeito/Dívida nunca disparados; hereditariedade de personalidade (AD-034) nunca feita.
- F8 limiares de fundação (4 de 5 vacuamente true); segurança/educação/infra = contagem de prédios; `memories` sempre `[]`.
- F9 `BinarySnapshotWriter`/`IncrementalHasher`/`RescheduleBatchParallel` sem uso em produção; orçamento de perf não vem do cenário.
- F11 consequências de `ProposedActions`/`Intent`/`MemoryCandidates` sem consumidor; guarda de rede não armada no gate; expiração de sessão em scheduler descartável.
- F13 `SkillBiases`/`Descriptors` sem consumidor; HUD de período adiado (sem `PeriodId` no `WorldState`).
- F28 sandbox sem rota/script; trace sem campo de traço/memória/descartada; trace só em necessidade urgente; delta de snapshot só em testes.

## Abertas — ausentes
- F0 teste de arquitetura "Api sem regra de domínio"; `verify-mutation.sh` fora do gate.
- F1 asserção ADR-0014 (campo canônico lido por decisão); folga de 50% do teto de iterações em baseline; hash por stream.
- F2 hash de mapa em 2 processos; hash sem camada de geografia.
- F3 extinção emite evento; nenhum evento após morte do NPC; replay de log a partir de snapshot.
- F5 recursos madeira/pedra; profissões lenhador/curandeiro/professor.
- F7 teste de linhagem (≥ floor(esperado/2) linhagens completas).
- F8 contagem independente (`COUNT(*)`); recomputo periódico; teste "zero NPC sem cidade por 10 anos"; trabalho de construção; papel de líder.
- F9 gate 100 anos / 10k×10 anos registrado; sweep em 10k; par on/off para lazy-need, wake, cold archive.
- F11 segredo de B fora do contexto de A; tick imune a provider lento/que lança; mutation pair do hash.
- F13 teste de 10 anos por período; 20 seeds de evolução; baseline de horizonte com braço controle.
- F28 tabela de pesquisa SEMPRE-TICK/LAZY/SÓ-AGREGADO; baseline pré-compressão.

## D — decisão necessária antes de implementar
- Migração: roadmap F8 diz "mesmo tick"; código faz viagem+chegada. Mudar roadmap ou código?
- FAM-32: efeito de seleção medido é o inverso do exigido (AD-080/082). Ajustar critério (ADR) ou modelo?
- Wiring de snapshot binário/delta em persistência (muda formato persistido).
- Recursos/profissões novos no `default` (muda goldens e calibração, AD-046).
- Remover `ExampleCounterSystem` (muda todos os goldens).
- Specs `.specs/features/phase-{04,05,07,08,09,11}` com tabelas "Pending" — marcar Verified só após os itens acima.

## Decisões já tomadas (não reverter sem decisão nova)
| Item da revisão | O que mudou e por quê | Onde está registrado |
|---|---|---|
| F4 histerese 20/20 → média | A rotina guia o dia (trocas marcadas pelo relógio, iguais nos dois braços); a histerese só atua nas decisões por urgência. Medido: −0,74 de ~110 trocas/dia, z = −4, 15/20 seeds. **"20/20" não é atendido** | AD-088, `phase-04-needs.md` |
| F5 salário parcial, não "byte-idêntico" | Caixa **zero** continua evento + saldos idênticos (teste `…empty_treasury…`). Caixa parcial paga a mesma fração a todos; antes os últimos por id ficavam sem renda e a vila passava fome | AD-082, `phase-05-economy.md` |
| F3–F5/F9 horizontes menores | Orçamento de tempo dos testes de escala (`Ten_k` 14 dias, `Population_100x` 30 dias, saturação de snapshot em 12 anos); `Category=Heavy` só com `--heavy`/`--nightly`. O sensor de crescimento achou um vazamento real (lote colhido ficava no snapshot) | AD-083, AD-086, `longrunning-budget.md` |
| F7 FAM-32 | **Aberto, não resolvido**: com a vila saudável CV(real) < CV(neutro) por 0,0041 (z = −4,0, 160 seeds). O roadmap reprova isso; AD-080 tratou como "efeito esperado" por engano | AD-088, spec da Fase 7 |
| F7 nascimentos | Tolerância da contagem de nascimentos 100% → 150%: natalidade bruta ~45‰ com a população sobrevivendo | AD-082, `FamilyPairedScenarioTests` |
| F3 mortalidade inicial | `ConditionalInitialMortality` ligada: adulto não morre 1 h depois por sorteio de idade menor que a atual | AD-077 |
| F1 `ExampleCounterSystem` | Continua em produção e no hash; só ganhou `[LivingCapability]` (diagnóstico). Remover muda todos os goldens (D) | AD-084 |
| F1/F9 estrutura | Sistemas se declaram (`LivingCapability`/`RunsAfter`); `BehaviorDecisionSystem`, `WorldState` e `ScenarioRunner` são `partial` por responsabilidade (mesmas linhas) | AD-084, AD-087 |
| Cliente web | Removido (`web/`, `web-demo/`, gerador de tipos, testes que liam TypeScript). Contrato em `tests/contracts/` | ADR-0022, AD-085 |

## Falha das pins de hash (reproduzia em develop e em 4a86dbf)
`OptimizationEquivalenceTests.Default_scenario_ninety_days_keeps_the_recorded_hashes` (seeds 1 e 2) **passa** no HEAD
desta branch (corrigido acima): os pins foram regravados no commit do lote colhido (AD-083, que muda o hash canônico de propósito; AD-081
é que garantia hashes idênticos). Se falhar em outro checkout, é build antigo — rode `bash scripts/build.sh` antes.

## Achados desta revisão sobre a própria revisão
- A branch falhava `lint.sh` (ordem de imports em `BranchHashTests`); corrigido. CI (`ci.yml`) roda `verify.sh`.
- O agente rodou só análise estática: nenhum item acima foi reproduzido por teste, exceto os três fechados.

## Achados novos desta rodada
- Abrir sessão de conversa consome um id de evento canônico (`TickContext.ScheduleEvent` → `NextEventIdAndAdvance`) mesmo com
  scheduler descartável (API). Hoje inofensivo (mundo efêmero); com tick real, abrir conversa muda o hash. **D**: expiração
  preguiçosa (sem evento) ou aceitar o id como entrada do jogador.
- Não feito de propósito (muda hash/calibração da vila ou formato persistido; critério do usuário: sem custo de perf e sem
  perder realismo): sede/sono em 0 com consequência (campo canônico novo), tutoria viva (`AssignMentor`), Help/Trade,
  recursos/profissões novos, snapshot binário em produção, remoção do `ExampleCounterSystem`.
