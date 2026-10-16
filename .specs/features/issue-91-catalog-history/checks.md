# Issue 91: evidências da implementação

## Adequação dos testes
Testes seguem `rules/tests.md`; cada cenário abaixo cobre um requisito da spec.
A mesma tabela dá o mapeamento de requisito → prova e prova → requisito.

| Requisito | Prova e resultado esperado |
|---|---|
| HIST91-01: catálogo ligado | CatalogHistoryTests.cs:30, `Assert.True(host.Current.HistoryRules.Enabled)` nos cinco períodos. |
| HIST91-01: fatos reais | CatalogHistoryTests.cs:32, `Assert.NotEmpty(host.Current.Facts)` após três anos. |
| HIST91-01: relatos | CatalogHistoryTests.cs:34, `Assert.NotEmpty(reports!)` na API. |
| HIST91-01: crenças | CatalogHistoryTests.cs:37, `Assert.Contains(beliefs!, belief => belief.ReportId == report.Id.Value)`. |
| HIST91-01: biografia e crônica | CatalogHistoryTests.cs:40 e :42, `Assert.NotEmpty(...Claims)`. |
| HIST91-02: fato íntegro antes do sink | RuntimeHistoryTests.cs:33 e :34, `Assert.Single(world.Facts)` e igualdade de tick/kind/payload/cidade/participante no callback do sink. |
| HIST91-02: significância e proveniência | RuntimeHistoryTests.cs:36 e :37, significância ≥ limiar, origem `runtime-test`, causa `7`. |
| HIST91-02: desligado/irrelevante | RuntimeHistoryTests.cs:51, `Assert.Empty(world.Facts)` em ambos os casos. |
| HIST91-02: evitar duplicação | RuntimeHistoryTests.cs:64, `Assert.Empty(world.Facts)` para metadados e produtores explícitos, mesmo com limiar zero. |
| HIST91-03: conversão sem sink | RuntimeHistoryTests.cs:75–78, morte real em sistema de tick gera fato, relato e slot no cânone. |
| HIST91-03: consumo causal | RuntimeHistoryTests.cs:84, `Assert.Equal(40, Score(ctx) - Score(ctx with { RelevantBeliefs = [] }), precision: 8)`. |
| HIST91-03: cache | RuntimeHistoryTests.cs:100–108, troca de cânone com mesmo tamanho atualiza crença; leitura preserva hash; snapshot reconstrói crença; outro mundo não herda cache. |
| HIST91-04: dois processos | CatalogHistoryDeterminismTests.cs:12, `Assert.Equal(first, second)`, com fatos e relatos >0 (:15–16). |
| HIST91-04: outra seed | CatalogHistoryDeterminismTests.cs:17, `Assert.NotEqual(parts[0], ...Split(';')[0])`. |
| HIST91-05: custo | RuntimeHistoryCostTests.cs:44, `Assert.True(after / before <= 1.03, ...)`. |

Caminhos completos dos testes: `tests/LivingWorld.Tests.Integration/World/`,
`tests/LivingWorld.Tests.Unit/History/`, `tests/LivingWorld.Tests.LongRunning/`
e seu subdiretório `Performance/`.
Sem testes novos fora desses requisitos; nenhum teste existente foi removido ou relaxado.

## Sensor de custo
1.000 NPCs, 720 ticks, cinco pares alternados após um par de aquecimento.
Medianas: desligado 0.876685, ligado 0.820151 µs/NPC-tick; razão 0.935514 ≤1.03.
A diferença abaixo de 1 é ruído de medição, não promessa de aceleração.
A medição inicial (~2.94) falhou e motivou filtragem e cache, mantendo o mesmo teto.

## Regressão reproduzida
Sem a correção do carregador, a criação pela API deixa HistoryRules.Enabled falso.
Com apenas o carregador ligado, os cinco períodos falham em `Assert.NotEmpty(Facts)`
após três anos. Ligar o gravador central resolve a causa independente do sink.
O Verifier registra a discriminação em scratch e o resultado independente em validation.md.

## Baseline histórico
A develop original passa no baseline de colapso. A comparação isolada dos eventos
antes/depois mostrou somente dois ReportConverted extras na seed 11 e um na seed 17,
sem remoção ou alteração dos eventos anteriores. Baseline regenerado pelo comando
existente `LW_RECORD_HISTORY_COLLAPSE=1 bash scripts/test.sh --scenarios --filter
Record_history_collapse_when_explicitly_requested`, preservando o teste e suas asserções.

## Gates
- `bash scripts/verify.sh`: 2352 passaram, 3 skips existentes, zero falhas.
- `bash scripts/test.sh --project integration`: 448 passaram, zero falhas.
- Sensor de dois processos: 1 passou; sensor de custo: 1 passou.
