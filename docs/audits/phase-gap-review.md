# Revisão de fases fechadas — lacunas (2026-10-11)

Auditoria estática (sem rodar testes) das fases `fechada`: 0–9, 11, 13, 28. Cada item
abaixo é lacuna real vs. `docs/roadmap/phase-NN-*.md`. Fechadas nesta branch: **[x]**.
Itens que exigem decisão de escopo/arquitetura (AGENTS.md → "PERGUNTE QUANDO") ficam em **D**.

## Fechadas nesta branch
- [x] F2 — `Cities[]` fora do grid rejeitada no load (`CityScenarioLoader`).
- [x] F3 — hash canônico distinto por `BranchId`; teste de reflexão: todo método de `IWorldRepository` recebe `BranchId`.
- [x] F11 — `LlmResponseValidator` rejeita `Intent` fora de `AllowedIntents`.

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

## Falha pré-existente em `develop`
`OptimizationEquivalenceTests.Default_scenario_ninety_days_keeps_the_recorded_hashes` (seeds 1 e 2)
falha sem nenhuma mudança desta branch: hashes gravados estão defasados. Regravar exige
decidir se a mudança de hash foi intencional (AD-081/083 diziam "hashes idênticos").
