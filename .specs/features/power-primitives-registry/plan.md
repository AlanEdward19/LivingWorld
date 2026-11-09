# Registro de primitivas do extraordinário (issue #64)

**Status**: parser único implementado (ADR-0040) na branch `worktree-agent-a23e4ef1c39710db9`; falta medição de escala e novo Verifier independente (ambos do orquestrador). Rota pública fora do escopo.

## Problem

Efeitos e custos já são tokens de dados, mas o vocabulário de atributo do NPC (`health|hunger|thirst|sleep|social`) estava duplicado em `switch` em `CarrierCostMechanic` (2), `NpcStatMechanic`, `TransferMechanic` (3), `BondMechanic` (2) e `ExtraordinaryLocomotion` (2). Atributo novo = editar 5 arquivos. Não havia como listar o que o motor aceita, e token inválido só falhava na invocação (nota de reabertura da Fase 16, ADR-0010).

## Flow

1. `NpcStatAccessors` (novo): tabela única chave → leitura no tick, escrita bruta no tick, faixa. Lookup por `Dictionary.AlternateLookup<ReadOnlySpan<char>>`, delegates `static`: a busca na tabela não aloca. A chave do modo de falha `carrier.health` é pré-computada (`HealthFailureKey`), sem concatenar por invocação.
2. As cinco mecânicas usam a tabela e **mantêm o clamp de cada uma**: `npc.*` e crédito de `transfer.*` limitam (`AddClamped`); `carrier.*`, débito de `transfer.*` e locomoção subtraem sem clamp próprio (`SubtractRaw`, o setter do NPC limita); `bond.share` limita (`Clamp`).
3. `IExtraordinaryMechanic.Primitives` (novo): cada mecânica declara `ExtraordinaryPrimitive<T>` com UM parser tipado e sem efeito (`Parse(token): Result<T>`), usado pela carga (`Matches`) e pela invocação (`Prepare*`, leitores de efeito, `AreaTargetResolver`). Estado (saldo, NPC existente, Fact, `mind.commune` com fantasma) fica só na invocação. `Listing` é o que o catálogo mostra; parser sem listagem é token tolerado (ADR-0040). As que dependem de atributo derivam da tabela.
4. `ExtraordinaryPrimitiveCatalog.Describe(registry)` enumera o registro por reflexão (`GetType()` + `Primitives`); sem lista paralela. `Validate` aceita o token só se o prefixo resolve a uma mecânica do tipo certo (efeito/custo) e algum `Primitive` casa.
5. `ExtraordinaryScenarioLoader` chama `Validate` para `Effects` e `Costs` (depois das demais validações, para não mudar a precedência de erros). `FailureModes` seguem texto livre.

## Impact

| Front | What changes |
| --- | --- |
| domain | nenhuma mudança em `PowerDescriptor` nem no formato do cenário |
| stored data | nenhuma; snapshots e hashes idênticos (goldens sem regravação) |
| behavior | loader recusa token desconhecido na carga (mensagem cita descritor e token) |

## Relations

None - nothing new persisted.

One-way constraints: nenhuma rota/DTO público nesta fatia (decisão do dono); o modo de falha `carrier.health` continua só para saúde (`NpcStatAccessors.Health`), sem generalizar.

## Surface

None - nothing consumed outside. `ExtraordinaryPrimitiveInfo` é tipo interno da Simulation para a futura rota `GET /authoring/extraordinary/primitives` (#46-5).

## Landing

Dono decide: rota de primitivas, fechar a Fase 16 no `ROADMAP.md`. Orquestrador: sensor de escala único, `verification.md`, `STATE.md`, PR.

## Criteria (EARS)

1. Quando os goldens e pins rodam, o sistema deve reproduzir todos os hashes sem regravação (`verify.sh --full`, `GoldenHashesTests`).
2. Quando um cenário declara poder inédito só com tokens existentes (`area:radius:3`, `npc.thirst:-20`, `household.resource.9:2`, `carrier.sleep:5`), concedido e invocado, o sistema deve aplicar efeitos e custos e registrar `ExtraordinaryEffectApplied`/`ExtraordinaryCostPaid` sem C# novo (`DataOnlyPowerTests`).
3. Se existir literal de atributo (`"health"`, `"carrier.hunger"`, ...) em `Simulation/Extraordinary/` fora de `NpcStatAccessors.cs`, o teste de arquitetura deve falhar (`NpcStatTableArchitectureTests`; mutação: switch injetado é detectado).
4. Quando o cenário traz `npc.mana:+5`, o loader deve recusar com mensagem que cita o descritor e o token (`ExtraordinaryPrimitiveCatalogTests`, também efeito em custo e vice-versa).
5. Toda classe concreta de `IExtraordinaryMechanic` da assembly deve estar no registro e no catálogo; teste reprova se faltar (mutação: registro sem `NpcCloneMechanic` é detectado).
6. Com `Extraordinary.Enabled = false` (cenário padrão), o hash de 100 ticks deve igualar o golden (`DataOnlyPowerTests`; longos em `GoldenHashesTests`).
7. Sensor de escala (<= 103% tempo, <= 102% alocação): **não medido aqui** (outras worktrees em paralelo geram ruído); o orquestrador mede uma vez ao final com `compare-simulation-cost.sh`. Mitigação de projeto: resolução sem alocação por acesso (item 1 do Flow).

## Decisões de risco (para o dono)

- Fixtures de teste com tokens narrativos (`movement:construct`, `fatigue:2`, `movement:speed`) em `ExtraordinaryScenarioLoaderTests` e `ExtraordinaryIntegrationTests` foram trocados por tokens reais: o loader agora os recusaria. Nenhum arquivo de `scenarios/` tem bloco `Extraordinary`, então nenhum cenário foi afetado.
- Regra (ADR-0040, decisão do dono): carga e invocação compartilham o parser; a carga aceita exatamente o que a invocação aceita sintaticamente. A única redução fora do parser é a forma tipada `chave:tipo:n` (`AllowTyped = false` para seletor de área, lido cru). Lixo que a invocação aceita sem efeito (`movement.*`, valor de `attribute.*`, `area:<não seletor>`, `dimension.pocket-store:<qualquer>`) passa na carga e está listado no ADR. Fecha F1 e F2 da rodada 3; F3 coberto por `PrimitiveParserValueTests` e testes de comportamento.
- `bond.oath:<token>` exige consequência não vazia cujo prefixo resolve a uma mecânica; o argumento interno continua validado só na invocação.
- `GET /periods/{id}/catalog` de template salvo que não valida mais devolve 400 com o erro (antes seria exceção); sem mudança de contrato.
- AD-124 em `docs/decisions-log.md`.
