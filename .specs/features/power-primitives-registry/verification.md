# Verificação independente: issue #64 (rodada 4)

Base `95482602` (= `main` = merge-base), HEAD verificado `ea4867d1`. Rota HTTP e `ROADMAP.md` fora de escopo. ADR-0040 será renumerado (não reprova).

**Veredito: PASS.** F1, F2 e F3 da rodada 3 fechados; carga e invocação usam o mesmo parser; invocação idêntica à `main` para todo token aceito; `verify.sh --full` sai 0; custo 100,96% CPU e 99,98% alocação.

## Critérios

| # | Critério | Veredito | Evidência |
|---|---|---|---|
| 1 | Paridade de hash | PASS | `git diff --name-only main...HEAD`: nenhum golden/baseline. `GoldenHashesTests` e determinismo passam no `--full` (LongRunning 146/0/8) |
| 1b | Paridade de invocação | PASS | Diferencial empírico abaixo: 0 diferença em token aceito |
| 2 | Poder novo só por dados | PASS | `DataOnlyPowerTests`; mutante `C2-carrier-cost-noop` reprova (`Novel_power_made_only_of_existing_tokens...`) |
| 3 | Atributo num lugar só | PASS | `git grep` de literal de atributo em `Simulation/Extraordinary` fora de `NpcStatAccessors.cs`: 0. `C3-literal-injected` (switch com `"hunger"`) reprova |
| 4 | Validação na carga | PASS | `ExtraordinaryPrimitiveCatalogTests.cs:64-68` (descritor e token na mensagem). `C4-loader-no-validate` reprova 872 testes |
| 5 | Catálogo por reflexão | PASS | `C5-registry-missing-clone` reprova (`A_valid_token_of_every_mechanic...`, `Every_...`) |
| 6 | Base sem poder | PASS | Hash de 100 ticks em `DataOnlyPowerTests` e goldens longos passam sem regravação |
| 7 | Sensor de escala | PASS | `compare-simulation-cost.sh`: cpuRatio 1,0096, allocationRatio 0,9998 (tetos 1,03/1,02) |
| ADR | Regra do parser único | PASS | `IExtraordinaryMechanic.cs:91-110` (`Parse`/`Accepts`/`Matches`), `ExtraordinaryPrimitiveCatalog.cs:33-40`; `Prepare*` chamam o mesmo `Parse` |

## Unicidade (item 2)

`grep Split|StartsWith|TryParse|IndexOf` em `Simulation/Extraordinary`: nos caminhos de carga e invocação o que resta é despacho por prefixo, mensagem de erro (`CombatMechanic.cs:46`, `HouseholdResourceCostMechanic.cs:43`) ou a redução tipada do motor (`TrySplitTypedMagnitude`, citada no ADR). Os leitores citados no ADR (Gravity, Attribute, Skill, Luck, Flora, `AreaTargetResolver`) e `FloraMechanic.PlantInArea` chamam os parsers. Separei carga e invocação com 7 mutantes, e todos reprovam: `SEP-validate-prefix-only` (740 testes), `SEP-load-no-space` (75), `SEP-combat-own-read`, `SEP-foresight-own-read`, `SEP-matter-own-read`, `SEP-movement-refuse-unknown`, `SEP-area-resolver-typed`. Leitores secundários pré-existentes: ver Observações.

## Diferencial empírico (itens 4 e 5)

Harness temporário idêntico no HEAD e numa `git worktree add --detach main` (removida ao fim), com o motor real. Foram 3.855 tokens: a lista do `LoadInvocationParityTests` mais variações, e 77 heads x 38 sufixos. Cada token roda como efeito com `Success` e com `PartialSuccess`; os de `carrier.`/`household.` rodam também como custo. São 685 casos de `ExtraordinaryLocomotion.Advance`. A saída registra resultado, 5 atributos do portador e do alvo, `CanonicalHash`, eventos e 22 leitores de efeito: 8.932 linhas.
- **Invocação main x HEAD:** 1.917 linhas aceitas na `main`, todas idênticas. Locomoção: 685 linhas idênticas. Só diferem 58 linhas, sempre de token recusado nos dois: texto do erro da invocação (abaixo) e artefatos de TAB no TSV. Os `out` de `TryParse*` internos diferem quando o parse falha, mas nenhum chamador usa o valor nesse caso.
- **Carga x invocação (HEAD):** carga recusa e invocação aceita: **0**. Carga aceita e invocação recusa: 52 (efeito) e 23 (custo), todas dependentes do mundo: NPC/Fact/skill ausente, saldo, alcance, estoque. Uma exceção: `gravity.target:Infinity` (observação).
- **Item 5:** toda ressalva do ADR é aceita pela invocação na `main`: `movement.qualquer|flight|flight:Infinity|flight:abc|flight:-1|.`, `attribute.strength[:abc]`, `attribute.perception:x`, `area:radius`, `area:foo:3`, `dimension.pocket-store[:abc|:-1]`, `npc.teleportx:5`, `npc.force-actionx:1`, `combat.strike:8:1`. `area:radius:` e `area:radius:fire:3` são recusados nas duas.

## F1, F2 e F3 da rodada 3 (refeitos)

Cada mutante foi aplicado num arquivo só, testado com `scripts/test.sh --filter FullyQualifiedName~Extraordinary` (1.545 testes, base verde) e restaurado com `git checkout`. **31/31 reprovam.**
- F1: `F1-area-typed` (`allowTyped: true`) e `F1-matches-ignores-allowtyped` reprovam (10 testes cada).
- F2: `F2-matter-strict-head`, `F2-foresight-blank` (trim) e `F2-movement-finite` (`IsFinite`) reprovam.
- F3, 15 dos 17: `T-clamp-off`, `TR-noscale`, `BD-carrier-sign`, `CO-parts`, `CO-w`, `CO-blank`, `MT-rate`, `MT-head`, `MT-origin`, `SK-copy`, `S-transfer-exact-signed`, `S-household-signed`, `S-teleport-signed`, `S-strike-signed`, `S-clone-signed`, todos reprovam. Não refeitos: `S-engage-signed` e `S-split-signed`, o mesmo padrão de `S-strike-signed` e `S-clone-signed` (o teste afirma os dois).
- Critérios: `C2`, `C3`, `C4` e `C5` reprovam (acima).

## `verify.sh --full` (saída 0; carga 19,9 no início e 4,8 no fim)

check-docs OK, build 0 aviso/0 erro, lint OK. Unit rápido 3744/0/3; Unit `Scenario` 23/0/1; Integration 451/0/0; LongRunning sem `Heavy` 146/0/8. Os skips são gravadores de baseline (`ZZZ_record_*`, `Record_*`). Nenhum teste de custo falhou.

## Custo (rodado depois do `--full`, sem paralelo; carga 4,2 a 7,3)

before µs/NPC-tick 0,913/0,829/0,837 (mediana 0,837), alocação/tick mediana 258.835; after 0,928/0,839/0,845 (mediana 0,845), mediana 258.784. Saída 0.

## Observações (não bloqueantes)

- **Leitor secundário de custo fora do parser (pré-existente, fora do ADR):** `ExtraordinaryLocomotion.TryPayMovementCosts` (`ExtraordinaryLocomotion.cs:108-150`) relê o custo com `LastIndexOf`/`int.TryParse` e um `TryHouseholdResource` duplicado. O custo tipado `carrier.hunger:fire:5` passa na carga e a invocação o paga, mas a locomoção recusa e o NPC não se move (`Moved = False`). É idêntico à `main`, e alinhar mudaria o comportamento. O dono deve registrar no ADR ou abrir um follow-up. Agregadores só de magnitude, também pré-existentes: `NpcInstantiationMechanic.DeclaredMagnitudeSum`, dificuldade em `ResolveDeclaredOutcome`, `FailureModes`.
- **`gravity.target:Infinity`** passa na carga e na invocação nos dois checkouts (paridade), mas depois `WorldSnapshot.CanonicalHash` lança `ArgumentException`, porque JSON não aceita infinito (`IncrementalHasher.cs:106`). O bug é pré-existente e agora fica alcançável por cenário válido; recomendo follow-up.
- **Mensagem de erro da invocação mudou** em token recusado nos dois: `environment.temperature*`, `flora.growth-rate*`, `npc.clone*`/`npc.split-on-death*`/`npc.reincarnate*` sem `:n` dão agora "Effects: alvo não suportado" sem o token (`HeadMismatch`), e a chave de `npc.reincarnate:a:b` mudou. O payload de `ExtraordinaryUseFailed` difere da `main`; o hash não muda. Contradiz a frase do ADR "mensagens foram mantidas", mas a carga já recusa esses tokens.
