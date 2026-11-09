# Verificação: #112 fatia E (indicadores honestos da cidade)

## Rodada 1 (resumo)
- Alvo `4704388f`. **FAIL**: M7a/M7b (morto ainda em `world.Npcs` entrando no numerador/denominador da
  média) e M11 (`health` do delta de escopo com a soma) sobreviviam. Itens 2 a 6 passaram.

## Rodada 2
- **Veredito: FAIL** (1 lacuna de teste com mutante sobrevivente que muda a saída do contrato)
- Alvo: `worktree-agent-a933e20dff663f1e4` @ `8c8c31b0` (contém a `main` `b49a35d6`); `git diff main...HEAD`.
- Verifier independente, 2026-11-09, máquina dividida (load 7 a 13). Mutações numa cópia (`git archive
  HEAD` no scratchpad), nunca no worktree. Laço de mutação: 98 testes Unit das classes de cidade/escopo/visual.

### 1. Health como média: requisito atendido; uma lacuna no projetor
- Código: `CityPopulationQuery.Health` = `(double)HealthSum / Population`, 0 sem vivos. O delta de escopo
  (`LivingScopeState.BuildIndicators`) e o `CityProjector` chamam o mesmo `Health`.
- Conta à mão: vivos 70 + 51 + pool (3, soma 200) = 321 / 5 = **64,2**; com morto de 100 no numerador
  seria 84,2 e no denominador 53,5. A sonda (`CityProjector.Build`, JSON Web) deu `"health":64.2`.
- API real (`/visual/subscribe?scope=City`, cidade canônica, 20 moradores): `"health":97.9`.
- M7a, M7b e M11 da rodada 1 agora **reprovam** (1 teste cada: `Health_leaves_a_dead_resident_out...` e
  `LivingScopeIndicatorsTests...`).
- Mutantes próprios **mortos**: Q1 divisão inteira (4), Q2 cidade vazia = 100 (2), Q3 numerador com vivos
  de outra cidade (2), Q4 denominador sem pool (4), Q5 `Math.Round` na consulta (4), Q6 pool fora do
  numerador (4), Q7 denominador +1 (5), P1 projetor com `HealthSum` (1), L1 `Math.Floor` no delta (1),
  L2 delta com `Population` (1), L3 delta sem `health` (1).
- **Sobrevivente P5**: `CityProjector.Build` com `Math.Floor(CityPopulationQuery.Health(world, cityId))`.
  Passou em `test.sh --all` (Unit 3775 ok, 4 skip) e na Integration (451 ok). Entrada concreta e válida:
  a cidade 70/51 + pool(3, 200) sai `"health":64` em vez de 64.2; a cidade canônica sairia 97 em vez de 97.9.
  Motivo: `Build_includes_the_six_indicators_matching_CityPopulationQuery` usa um mundo com média inteira, e
  o contrato JSON só confere o tipo `number`. Truncar era justamente o defeito antigo (saúde `long`).
- Correção sugerida: no teste do projetor, uma cidade com média fracionária e `Assert.Equal(64.2,
  indicators.Health, 9)` (ou a mesma igualdade com a consulta nesse mundo).

### 2. Teste por reflexão: PASS
- Fontes em produção: `src/LivingWorld.Api/Visual/Projection/CityIndicatorSources.cs`.
- Mortos: S1 sem fonte de Security (3), S2 `Measured("Inexistente")` (1), S3 campo novo `long? Felicidade`
  sem fonte (3), S5 fonte órfã (1), S6 issue `"depois"` (1).
- S4 (Security declarada `Measured(Housing)` com o valor ainda nulo) sobrevive. Não muda a saída do
  contrato (o projetor continua mandando nulo); só metadado. Não reprova.

### 3. Nulos no contrato: PASS
- API real e sonda: `"security":null,"education":null,"infrastructure":null` (literal `null`, não omitido).
- `tests/contracts/shape.city-subscribe.json` declara `security?/education?/infrastructure?: number`, coerente
  com `TypedOptionals` em `ApiContractTests`. Mortos: P2 Security = contagem de prédios (2), P3 Education = 0
  (2), P4 Infrastructure = Housing (2). Nota: o teste de contrato aceita tanto nulo quanto ausente.

### 4. Simulação e hash: PASS
- `grep` em `src/LivingWorld.{Simulation,Domain,Workers,Infrastructure}`: nenhum leitor de
  `CityPopulationQuery.Health/HealthSum`, `CityIndicators` ou `IndicatorUpdate`. Leitores só na Api.
- Nenhum golden, baseline ou cenário no diff (`git diff main...HEAD --name-only`).
- Hash: `GoldenHashesTests` (LongRunning) passa no `--full` sem golden regravado.
- Os métodos alterados (`Health`, `HealthSum`, os três removidos) estão **fora do tick**; por isso
  `compare-simulation-cost.sh` **não se aplica** e não foi rodado.

### 5. Cliente: PASS
- `bash scripts/client-test.sh` exit 0: **288 passed, 0 failed**.
- `test_city_panel_shows_dash_for_unmeasured_indicators_and_health_as_mean` cobre nulo e ausente ("—") e a
  média com 1 casa. Mutantes do cliente mortos: ausente vira 0, nulo vira "0", saúde `int`.
- FPS de stress não remedido (o README registra antes 74,6 e depois 72,3 de mediana; a ficha não abre no `stress`).

### 6. Integração: PASS
- `main` (`b49a35d6`, parser da #64) é ancestral do HEAD; o merge `8c8c31b0` só conflitou em
  `docs/adr/README.md`, que tem as linhas 0040 e 0042. `bash scripts/check-docs.sh`: OK.
- `plan.md` agora existe; AD-123 e ADR-0042 consistentes.

### 7. Gate
- `bash scripts/verify.sh --full` exit 0: Unit 3752 (3 skip), Scenario 23 (1 skip), Integration 451,
  LongRunning 146 (8 skip), `verify: OK`. Nenhum teste de custo falhou. O gate passar não supre a lacuna P5.

### Não verificado
- FPS do cliente (não remedido). Hash fora do test host (o CLI de hash parou na rodada 1).
- LongRunning não rodado com o mutante P5 (só Unit --all e Integration).
