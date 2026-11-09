# Verificação independente: issue #64 (rodada 3)

Base `b29ed9f2`, HEAD verificado `735a02a3`. Critério 7 (`compare-simulation-cost.sh`) adiado por decisão do orquestrador, não rodado. Rota HTTP e `ROADMAP.md` fora de escopo.

**Veredito: FAIL.** Três FAILs: a carga aceita token que toda invocação recusa (F1), a carga recusa token com efeito real (F2) e 17 mutantes de regra alcançável sobrevivem (F3). Paridade de comportamento e critérios 1, 2, 3, 5 e 6 passam. `verify.sh --full` sai 0.

## Critérios

| # | Critério | Veredito | Evidência |
|---|---|---|---|
| 1 | Paridade de hash | PASS | `git diff --name-only b29ed9f2...HEAD` sem golden/baseline (0 linhas). `test.sh --project longrunning --filter Golden\|Determin`: 17 passam, 1 skip (`ZZZ_record_golden_hashes`), saída 0 |
| 1b | Paridade de comportamento | PASS | Diferencial empírico, seção abaixo: 69.317 linhas idênticas entre base e HEAD |
| 2 | Poder novo só por dados | PASS | `DataOnlyPowerTests`; mutantes `C2-area-off`, `C2-cost-event`, `C2-effect-event` reprovam |
| 3 | Atributo num lugar só | PASS | `git grep -i` de literal de atributo em `Simulation/Extraordinary` no HEAD: 0 ocorrências. Switch injetado, `"carrier.health:"`, `"hunger"` e `"bond.share:sleep"` em subpasta reprovam |
| 4 | Validação na carga | **FAIL** | `npc.mana:+5` é recusado citando descritor e token (`C4-*` reprovam), mas a regra de produto diverge: F1 e F2 |
| 5 | Catálogo por reflexão | PASS | `C5-unregistered` e `C5-empty-prims` reprovam |
| 6 | Base sem poder | PASS | Hash de 100 ticks igual ao golden; `C6-base-hash` (mutação em `LazyNeed`) reprova |
| 7 | Sensor de escala | adiado | Não medido, por instrução |
| Doc | Nota em `phase-16-powers.md` | PASS | Nota de reabertura substituída pelo estado real |
| Erro | Caminho de erro da API | PASS | `PeriodsEndpoints.cs:96-98` devolve 400; `API-400` reprova em `PeriodsEndpointTests`. Demais chamadores de `LoadWorld`/`Validate` testam `IsSuccess` |

## Paridade de comportamento (critério 1b)

Mesmo harness temporário no HEAD e em `git archive b29ed9f2`, invocando o motor real. Saída por linha: resultado, 5 atributos do portador e do alvo, `CanonicalHash` e eventos. Cobre cerca de 5 mil tokens em 3 variantes com `Success` e `PartialSuccess`; grade de 5 atributos x valores 0/1/37/99/100 x magnitudes até `int.MaxValue` para `npc.*`, `transfer.*`, `bond.share`, `carrier.*`, tipados e com vulnerabilidade; 1.000 casos de `ExtraordinaryLocomotion.Advance`. `cmp`: **idêntico** (69.317 linhas). Inclui `CarrierCost`, `NpcStat`, `Transfer`, `Bond`, `Locomotion` e os parsers extraídos de `Construct`, `Matter` e `Skill`.

## Carga x invocação (5.172 tokens)

| Carga | Invocação | Linhas | Leitura |
|---|---|---|---|
| aceita | aceita | 833 | coerente |
| recusa | recusa | 4.079 | coerente (mensagens revisadas, nenhuma dependia do mundo) |
| aceita | recusa | 40 | 8 são F1; os demais dependem do mundo (NPC, Fact, skill, alcance, mapa) |
| recusa | aceita | 220 | 4 têm efeito direto (F2); 216 são `movement`, `attribute`, `area` sem efeito na invocação |

## FAILs

**F1. A carga aceita token que toda invocação recusa.** `area:radius:fire:3` e `area:region:fire:2` passam na redução tipada (`IExtraordinaryMechanic.cs:41`) para `area:radius:3` (`AreaSelectorMechanic.cs:13-14`). A invocação lê o token cru em `AreaTargetResolver.Resolve` e falha sempre: "seletor de área inválido". Ajuste: a forma tipada não vale para seletor de área.

**F2. A carga recusa token com efeito real.**
- `matter.transmute :1:2:3`: a invocação aplica (trim em `TrimEntries`), a carga recusa pela fronteira do `Head`. Falta `LooseHead: true` em `MatterTransmuteMechanic.cs:18`, como em `construct.create`.
- `foresight.preview: ` e `foresight.preview:\t`: a invocação só exige comprimento maior que o prefixo e registra a previsão. `NonEmptyArgument` recusa branco (`ForesightMechanic.cs:41`, `IExtraordinaryMechanic.cs:98`).
- `movement.flight:Infinity` e `movement.speed-multiplier:Infinity`: o consumidor aceita (`Resolve` deu `CanFly = True`, `SpeedMultiplier = ∞`), a carga recusa por `double.IsFinite` (`IExtraordinaryMechanic.cs:92`). Fora da pendência registrada no plano. Ajuste: aceitar, ou o dono registra a exceção.

**F3. Regra alcançável cuja quebra nenhum teste detecta.** Sobrevivem em Unit `--all` e Integration, com filtro `Extraordinary|Behavior|CausalChain|Authoring`:

| Mutante | Local | Caso sem teste |
|---|---|---|
| `T-clamp-off` | `NpcStatAccessors.cs:17` | `AddClamped` com `int.MaxValue`: correto 100, mutante 0 (provado com teste temporário) |
| `TR-noscale` | `TransferMechanic.cs:64` | `PartialSuccess` não reduz a transferência |
| `BD-carrier-sign` | `BondMechanic.cs:80` | portador com valor maior que o parceiro |
| `CO-parts`, `CO-w`, `CO-blank` | `ConstructMechanic.cs:72,74,78` | sexto segmento, largura 9, aparência em branco |
| `MT-rate`, `MT-head`, `MT-origin` | `MatterTransmuteMechanic.cs:63,60,61` | taxa 0, `matter.transmutex:1:2:3`, origem negativa |
| `SK-copy` | `SkillMechanic.cs:109` | `skill.copy:-1` |
| `S-transfer-exact-signed` | `TransferMechanic.cs:34` | `transfer.health:-5:from-target` |
| `S-household-signed` | `HouseholdResourceCostMechanic.cs:12` | `household.resource.9:-2` |
| `S-teleport-signed` | `TeleportMechanic.cs:15` | `npc.teleport:-5` |
| `S-strike-signed`, `S-engage-signed` | `CombatMechanic.cs:28-29` | `combat.strike:-8`, `combat.engage:-3` |
| `S-clone-signed`, `S-split-signed` | `NpcCloneSplitReincarnate.cs:100,128` | `npc.clone:-1`, `npc.split-on-death:-2` |

## Mutações

Cada uma restaurada com `git checkout -- <arquivo>`; `git status` limpo ao final, harness temporário removido.
- **Por `Primitive`** (63 heads, hook temporário executando os testes reais): 126 mutações always-true e always-false, 125 reprovam. A sobrevivente, always-true de `dimension.pocket-store`, é equivalente: a fronteira do `Head` já garante a regra.
- **Por fonte** (92 válidas): 68 reprovam e 24 sobrevivem. 17 são F3; 7 são não bloqueantes (abaixo). Cinco mutantes que não compilavam ou não casavam foram refeitos em forma válida.

## Observações não bloqueantes

- Mutantes equivalentes ou sem efeito observável: `TR-room` (`AddClamped` já limita), `S-transfer-exact-typed` (`Amount` e `AmountExact` são a mesma função), `S-reinc-lower`, `S-force-signed`, `R-S-oath-len`, `NS-unknown` (só texto), `S-boundary-off` (fora do caso de `matter.transmute` em F2, a sintaxe já confere a chave).
- `movement.*` fora de `flight`/`speed-multiplier`, `attribute.*` malformado e `area:radius` sem valor: recusados sem efeito na invocação. Decisão pendente do dono no plano; `ExtraordinaryLocomotion.cs:102` usa qualquer `movement.*` como ativador de custo.
- Limítrofe, classificado como dependente do mundo: `dimension.portal:0:0:3:-5` e `mind.implant-memory:-5` passam na carga e falham na consulta ao mapa e aos Facts. Coordenada ou id negativo não ocorre nos mundos gerados, mas `WorldMap` não o proíbe. O dono pode preferir recusar na carga.
- `POST /worlds/start` com template que deixou de validar devolve 404 com o erro (`WorldStartEndpoints.cs:23-24`), tratamento já existente.

## `verify.sh --full` (saída 0)

| Suíte | Passou | Falhou | Skip |
|---|---|---|---|
| check-docs, build, lint | OK | 0 | 0 avisos de build |
| Unit rápido | 2536 | 0 | 3 |
| Unit `Scenario` | 20 | 0 | 1 |
| Integration | 451 | 0 | 0 |
| LongRunning sem `Heavy` | 142 | 0 | 8 |

Os skips listados são gravadores de baseline (`ZZZ_record_*`, `Record_*`). Nenhum teste de limite de custo falhou.
