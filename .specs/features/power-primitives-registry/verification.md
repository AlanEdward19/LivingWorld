# Verificação independente: issue #64 (registro de primitivas)

Base `b29ed9f2`, HEAD verificado `2e8a1dca`. Critério 7 (sensor de escala) adiado por decisão do orquestrador; rota HTTP e `ROADMAP.md` fora de escopo.

**Veredito: FAIL.** Três FAILs: divergência carga x invocação, regras de sintaxe sem teste com poder, ramo doméstico da locomoção sem teste. Paridade de comportamento e critérios 1, 2, 3, 5 e 6 passam.

## Por critério

| Critério | Resultado | Evidência |
|---|---|---|
| 1. Paridade de hash | PASS | Nenhum arquivo em `tests/golden` ou `tests/baselines` no diff. `test.sh --project longrunning` com filtro Golden/Determinis: 17 passam, 1 pulado, rc 0 |
| 1b. Paridade por mecânica | PASS | Diferencial próprio: mesma bateria sobre export de `b29ed9f2` e sobre HEAD. 50.976 casos (token x efeito/custo x nível de atributo 60/3/98 x 3 alvos x resolução total/parcial), comparando resultado, atributos, estoque, portadores, agenda e eventos: `cmp` idêntico. Locomoção fora dessa bateria: conferida por leitura do diff |
| 2. Poder novo só por dados | PASS | `DataOnlyPowerTests`; mutações c2-area, c2-household, c2-costlog reprovam |
| 3. Atributo num lugar só | PASS | grep próprio sem literal fora de `NpcStatAccessors.cs`; c3-switch, c3-colon (`"carrier.health:"`), c3-engine reprovam |
| 4. Token inexistente recusado | PASS | `npc.mana:+5` recusado citando descritor e token; c4-effects, c4-costs, c4-msg-descr, c4-msg-token, c4-kind reprovam |
| 4b. Carga x invocação (ponto central) | **FAIL** | Ver F1 |
| 4c. Regras de sintaxe com teste | **FAIL** | Ver F2 |
| 5. Catálogo por reflexão | PASS | c5-unreg, c5-noprim, c5-describe, c5-newclass (classe nova fora do registro) reprovam |
| 6. Base sem poder | PASS | Golden seed 42/100 ticks igual; c6-base3 (seed+1 no `RunAndHash`) reprova |
| 7. Sensor de escala | adiado | Não medido, conforme instrução |
| Caminho de erro `/periods/{id}/catalog` | PASS | `PeriodsEndpoints.cs:96-97` devolve 400 com o erro; teste de integração reprova ao remover o guarda |
| Paridade testada na locomoção | **FAIL** | Ver F3 |
| Nota em `phase-16-powers.md` | PASS | Substituída; 53 linhas; `check-docs` OK |

Demais chamadores do loader: `RuntimePlan`, `/worlds/create` (400) e `LoadWorld` tratam o `Result`. `/worlds/start` devolve 404 com o erro, mapeamento já existente. `Workers/Program.cs` lança na partida, também já existente.

## F1. A carga recusa tokens que a invocação aceita

Harness próprio: 2.832 casos (token x efeito/custo) pela carga real (`ExtraordinaryScenarioLoader.Load`) e pela invocação real (`ExtraordinaryInvocationEngine.Invoke`), em mundo favorável com 3 variantes de alvo. Em `b29ed9f2` a carga aceitava todos.

| Veredito | Efeito | Custo |
|---|---|---|
| carga OK, invocação OK | 303 | 30 |
| carga recusa, invocação recusa | 963 | 1.386 |
| carga OK, invocação recusa | 5, todos dependentes do mundo (Fact, habilidade, NPC, célula) | 0 |
| **carga recusa, invocação aceita** | **145 (54 com efeito real aplicado)** | 0 |

Nenhum token é aceito na carga e recusado por regra independente do mundo. A direção oposta falha. As 54 linhas com `ExtraordinaryEffectApplied` caem em três classes:

1. **Forma tipada `chave:tipo:n`.** `ScaleTypedMagnitude` reduz o tipo para toda mecânica (`ExtraordinaryInvocationEngine.cs:250`), mas só `PrimitiveSyntax.Amount` conhece a forma. Recusados e funcionais na invocação: `npc.force-action:fire:1`, `gravity.target:fire:2`, `luck.curse:fire:10`, `luck.curse:10:fire:100`, `mind.alter-trait:agreeableness:fire:-5`, `mind.erase-memory:fire:5`, `mind.implant-memory:fire:5`, `matter.transmute:1:2:fire:3`, `skill.copy:fire:1`, `environment.temperature:0:-5:fire:10`, `dimension.portal:0:0:3:fire:3`, `appearance.impersonate:fire:5`, `bond.share:health:fire:1`, `control.possess:fire:1`.
2. **`dimension.pocket-store:<qualquer>`.** `PreparePocketStore` não valida o argumento e usa quantidade 1 (`DimensionMechanic.cs:33`); o catálogo exige inteiro positivo (`DimensionMechanic.cs:24`). `:abc`, `:0`, `:-1` movem estoque de verdade. `ExtraordinaryPrimitiveCatalogTests.cs:121` fixa a recusa, e o comentário em `:95` ("a carga recusa o que toda invocação recusaria") é falso para essa linha.
3. **Mecânica que não confere a chave.** `npc.teleportx:5`, `npc.teleport :5`, `npc.teleport:5:1`, `npc.force-actionx:1`, `construct.createx:1x1:10:5:stone`, `construct.create :1x1:10:5:stone`, `matter.transmute :1:2:3`, `combat.strike:8:1`, `combat.engage:3:1`. `IExtraordinaryMechanic.cs:41` exige delimitador; o resolvedor só `StartsWith` no prefixo.

**Catálogo e invocação não compartilham a regra por inteiro.** Compartilham em luck, fauna, flora, gravity, skill, construct, matter, environment, area, bond.share e control.possess. Divergem em pocket-store, force-action, teleport, combat, attribute, movement e no seletor `area:`.

**Precisa mudar:** reduzir a forma tipada na validação, num único ponto (`ExtraordinaryPrimitiveCatalog.cs:36` ou `IExtraordinaryMechanic.cs:42`), e alinhar as classes 2 e 3 com uma regra só, usada pela carga e pela invocação. Apertar a invocação muda comportamento; afrouxar a carga preserva. Atualizar a linha 121 do teste e cobrir as três classes com token real na carga e na invocação.

## F2. Regras de sintaxe sem teste com poder

Trocar a regra por `_ => true` não reprova nenhum teste. Oito casos conferidos com filtro `FullyQualifiedName~Extraordinary` (418 testes): `npc.teleport`, `npc.clone`, `combat.strike`, `mind.alter-trait`, `mind.erase-memory`, `gravity.target`, `fauna.infect-vector`, `attribute.fertility`, e ainda `AmountExact`. Com o filtro do catálogo (51 testes), sobrevivem também `npc.split-on-death`, `combat.engage`, `mind.read-memory`, `mind.implant-memory`, `luck.capacity-bonus`, `skill.learn-rate`, `attribute.perception`, `attribute.reaction-speed`, `movement.speed-multiplier`, `PositiveNumber` (`movement.flight:0`) e o `headOk` (`IExtraordinaryMechanic.cs:41`).

Sem isso uma mecânica pode perder a validação na carga em silêncio. `AmountExact` é o caso mais claro: sem ele `transfer.health:foo:5:from-target` passa na carga e sempre falha na invocação.

**Precisa mudar:** uma linha malformada por `Primitive` em `Malformed_effect_arguments_are_refused_on_load`, derivada da mecânica ou escrita à mão. Uma verificação por reflexão de que todo `Head` tem caso malformado fecha a classe.

## F3. Custo doméstico na locomoção sem teste

Remover `household!.Withdraw(resource, amount)` (`ExtraordinaryLocomotion.cs:136`) passa nos 418 testes. É o ramo reescrito de `default:` para `else`. Por leitura o comportamento é o mesmo, mas a issue pede paridade dessa mecânica e os goldens não exercitam poderes.

**Precisa mudar:** teste de movimento com custo `household.resource.<id>:n`, conferindo o débito.

## Mutações

Cada uma restaurada com `git checkout -- <arquivo>`; `git status` limpo ao final.

- **Reprovaram (critérios):** c2-area, c2-household, c2-costlog, c3-switch, c3-colon, c3-engine, c4-effects, c4-costs, c4-msg-descr, c4-msg-token, c4-kind, c5-unreg, c5-noprim, c5-describe, c5-newclass, c6-base3, api.
- **Reprovaram (paridade):** carrier-avail, carrier-pay, carrier-amount, npc-target, npc-scale, max, tab-hunger, tab-thirst, tab-social, tab-sleep, tab-health, tr-swap, tr-avail, tr-debit, tr-credit, bond-dir, bond-read, bond-prop, loco-avail, loco-pay, loco-sum.
- **Reprovaram (sintaxe):** central (38 linhas), force-action, reincarnate-range, flag, nonempty, area, oath, amount-signed de carrier e transfer, typed-off.
- **Sobreviveram:** as de F2 e F3, mais as não bloqueantes abaixo.

## Observações não bloqueantes

- `p-clamp-add` e `p-clamp-sub`: mutantes equivalentes, porque `SetHealth` e `LazyNeed.WithValue` já limitam.
- `p-tr-scale`: linha inalterada no diff; a parcial de transferência é coberta pelo diferencial, sem teste próprio.
- `c6-base2` (largura em `DefaultMap`) não muda o hash: não é o gerador usado por `RunAndHash`. Substituída por c6-base3.
- 91 tokens recusados na carga não têm efeito na invocação (`attribute.*` malformado, `movement.*`, `area:radius` sem valor, `flora`/`fauna`/`luck.capacity-bonus` tipados). Divergência literal, sem efeito no token. O plano só registra a decisão para `movement.*`; vale decidir junto com F1.
- A decisão do plano sobre `movement.*` ("no-op silencioso") é imprecisa: `ExtraordinaryLocomotion.cs:102` usa qualquer efeito `movement.` para ativar custos de movimento.

## `verify.sh --full`

rc 1. docs OK; Unit rápido 2529 passam, 3 pulados; Unit Scenario 20 passam, 1 pulado; Integration 451 passam; LongRunning 141 passam, 8 pulados, **1 falha**: `RuntimeHistoryCostTests.Enabled_history_costs_at_most_three_percent_more_per_npc_tick` (razão 1,0465; teto 1,03). Load average 43 por outras worktrees. Isolado, reprovou uma vez e passou na segunda; o diff não toca esse caminho. Tratado como ruído de ambiente, mas o gate não ficou verde nesta rodada e precisa ser refeito com a máquina livre.
