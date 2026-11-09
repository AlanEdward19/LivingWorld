# Verificação: #112 fatia E (indicadores honestos da cidade)

- **Veredito: FAIL** (2 lacunas de teste com mutante sobrevivente que muda a saída do contrato)
- Alvo: `worktree-agent-a933e20dff663f1e4` @ `4704388f`, base `b29ed9f2` (`git diff main...HEAD`)
- Verifier independente (não escreveu o código), 2026-11-09. Máquina dividida (load 8 a 108).
- Mutações rodadas numa cópia (`git archive HEAD`), nunca no worktree; cliente mutado e restaurado com `git checkout`.

## 1. Health como média: requisito atendido, cobertura incompleta
- Código: `CityPopulationQuery.Health` = `(double)HealthSum / Population`, 0 sem vivos (`CityPopulationQuery.cs:32-40`).
- Conta à mão (teste temporário): vivos 33+34+35 + pool(2, HealthSum 101) = 203/5 = **40,6**. Um morto (90)
  na cidade e um vivo (5) em outra cidade ficam de fora. Passou. Cidade só com morto: 0. Passou.
- Resposta real da API (`/visual/subscribe?scope=City`, 20 moradores): `"health":97.9`.
- Mutantes **mortos** (filtro das classes de teste da cidade): M1 divisão inteira, M2 denominador só
  materializados, M3 pool fora do numerador, M4 pool fora dos dois termos, M5 sem guarda de zero (NaN),
  M6 inclui residente de outra cidade, M10 projetor usando `HealthSum`. Cada um reprova 1 ou 2 testes.
- **Sobreviventes** (aplicados juntos; `test.sh --all` 2485/2485 e integration 450/450 passaram):
  - **M7b denominador conta mortos**: `Population` dentro de `Health` passa a ser
    `Npcs.LongCount(n => n.City == city) + pool`. Esse é um "denominador errado" e nenhum teste o pega.
  - **M7a numerador conta mortos**: `HealthSum` sem o filtro `IsAlive`.
  - **M11 delta de escopo com a soma**: `LivingScopeState.BuildIndicators` usando `new("health", HealthSum)`.
    O ADR-0040 diz que o `health` dos deltas também vira média; `LivingDeltaContractTests` só conta 6 indicadores.
- Entrada concreta e válida para M7: rodada real (`ScenarioRunner.Create(7)`, 40 anos). No ano 40 a cidade
  tem 79 vivos e 66 mortos ainda em `world.Npcs`, com `City` igual e `Health > 0` (o `ColdTierArchive` só
  arquiva depois). Real **100,000**; M7b **54,483**; M7a **181,392**. Houve 80 amostras assim em 2 seeds.
- Entrada para M11: qualquer cidade com mais de 1 morador; na cidade canônica, delta 1958 contra 97,9.
- Correção sugerida: teste com morador morto na cidade (saúde > 0) no numerador e no denominador, e um assert
  de que o `health` de `LivingScopeProjector.Build`/delta é igual a `CityPopulationQuery.Health`.

## 2. Teste por reflexão: PASS
- A lista de fontes está em produção: `src/LivingWorld.Api/Visual/Projection/CityIndicatorSources.cs`.
- R1 adiciona `long? Felicidade = null` sem fonte: reprova `Every_indicator_has_a_declared_source` (mais 2).
- R2 remove a fonte de Health: reprova 3 testes. R3 `Measured("Inexistente")`: reprova 1. R4 Housing
  `NotMeasured` com tipo não anulável: reprova 2. R5 fonte órfã `"Orfa"`: reprova `No_declared_source_is_orphaned...`.

## 3. Nulos no contrato: PASS
- Resposta real com 4 prédios: `"security":null,"education":null,"infrastructure":null` (JSON literal `null`).
- `tests/contracts/shape.city-subscribe.json` declara `security?/education?/infrastructure?: number`, o que
  bate com o null real via `TypedOptionals`. M8 (Security = contagem de prédios) e M9 (Education = 0)
  reprovam 2 testes Unit e `ApiContractTests.City_and_interior_shapes_match_the_committed_contract`.

## 4. Simulação e hash: PASS
- Grep em `src/`: `Health`, `HealthSum` e os três removidos só são lidos em `LivingWorld.Api`. Nenhum
  sistema da Simulation lê os valores nem `CityIndicators`/`IndicatorUpdate`.
- Nenhum golden, baseline ou cenário no diff. `RunAndHash` com base e HEAD (no processo de teste):
  (42, 2000), (7, 8760) e (5, 26280) deram hashes canônico e volátil idênticos.
- `CityPopulationQuery` como classe é usada no tick (`Population`, `Housing`), mas esses métodos não mudaram.
  Os métodos alterados (`Health`, os três removidos) estão fora do tick.
  Por isso `compare-simulation-cost.sh` **não se aplica** e não foi rodado.

## 5. Cliente: PASS
- `bash scripts/client-test.sh`: exit 0, **288 passed, 0 failed**.
- Sonda headless de `CityPanel.show_city`: null e ausente mostram "Segurança — · Educação — · Infraestrutura —".
  A saúde aparece com 1 casa (70.3, 64.2, 12.345 vira 12.3), sem artefato de float.
- Mutantes do cliente mortos: CM1 null vira "0", CM2 `indicators[key]` (ausente quebra), CM3 saúde truncada.
- Stress (README): antes 76,1/73,6/74,6 (mediana 74,6), depois 72,3/71,9/73,6 (mediana 72,3), -2,3.
  Método coerente: 3 rodadas antes e 3 depois, `stress:500`, mesma máquina, medianas conferidas.
  O `stress` (`dev_steps.gd:140`) não abre a ficha da cidade. Não remedi.

## 6. Gate: PASS
- `bash scripts/verify.sh --full` exit 0: Unit 2465 (3 skip), Scenario 20 (1 skip), Integration 450,
  LongRunning 142 (8 skip), `verify: OK`. Nenhum teste de custo falhou.

## Não verificado / notas
- Não existe `plan.md` em `.specs/features/city-indicators-honest/`.
- `dotnet <Workers.dll> hash 42 2000` ficou parado (CPU 0%) fora do test host, também na base `b29ed9f2`.
  Por isso o hash foi comparado dentro do processo de teste. Isso não é atribuível a esta fatia.
- AD-121 colide com a `main` (renumerar na mesclagem): não reprova.
