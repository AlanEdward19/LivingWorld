# Validação independente — issue #92

Data: 2026-10-18. Spec: `spec.md`. Base: `5d8e45c` + diff local.
Verifier: subagent independente; não autor dos testes. Escopo: dois testes HTTP.

## Critérios rederivados e evidência
Prefixo T = `tests/LivingWorld.Tests.Integration/Visual/PooledNpcContractTests.cs`.

| AC | Resultado esperado | Evidência e assertion | Veredito |
|---|---|---|---|
| AC-1 | Eating produz food tipado inteiro | T:135 seleciona Eat; T:138 exige presença; T:141 `Assert.True(errors.Count == 0)` | PASS |
| AC-2 | Avanço horário limitado e falha sem refeição | T:132 `hour < 72`; T:134 `Clock.Tick`; T:146 `Assert.Fail` | PASS |
| AC-3 | Filho contém pais autorados e IDs corretos | T:170–174 presença e contrato; T:175 e T:176 `Assert.Equal` com Ana/Beto | PASS |
| AC-4 | Tipos incorretos são rejeitados | T:141/T:174: três mutantes mortos por assertions específicas | PASS |
| AC-5 | Gate focado dura poucos segundos | T:125/T:151 executados: 4/4 em 5 s no scratch, sem skips | PASS |

Citações completas: `tests/LivingWorld.Tests.Integration/Visual/PooledNpcContractTests.cs:141`
e `tests/LivingWorld.Tests.Integration/Visual/PooledNpcContractTests.cs:174`.
O contrato exige resourceId numérico e pais como objeto com value numérico;
`Check` percorre os opcionais preenchidos em T:35 e tipos em T:49–58.
“Poucos segundos” não define teto numérico; observado 5 s sob concorrência,
compatível com a intenção da issue. Não se trata de garantia de tempo em CI.

## Necessidade e suficiência
Refeição cobre AC-1/2; família cobre AC-3; nenhum teste sem requisito.
Presença evita aprovação vacuosa dos opcionais; igualdade evita pai/mãe trocados.
Família usa cenário aceito pela issue; IDs reais são comparados após POST HTTP.
Erro HTTP falha via GetStringAsync/IsSuccessStatusCode; sem skip nem polling real.
Busca exausta tem falha explícita. Nenhum teste anterior foi removido/enfraquecido.

## Sensor em scratch
Cópia de arquivos rastreados em `/tmp/lw92-sensor-independent`, já removida.
Endpoint GET real serializado com as mesmas JsonOptions configuradas do host.
Baseline sem mutação: 4 passaram, 0 falharam, 0 pulados (5 s).
Mutantes selecionados por env apenas no scratch; cada execução usou scripts/test.sh.

| Mutante | Resultado da assertion | Estado |
|---|---|---|
| food.resourceId número → string | `$.food.resourceId: esperado number`, T:141 | morto |
| motherId objeto → número | `$.motherId: esperado objeto`, T:174 | morto |
| fatherId.value número → string | `$.fatherId.value: esperado number`, T:174 | morto |

Cada mutante: 1 falhou, 1 passou, 0 skips; exit 1 por assertion, sem erro de build.
Logs: `/tmp/lw92-sensor-{baseline,food,mother,father}.log`.
Porcelain capturado antes em `/tmp/lw92-sensor-porcelain-before`.
Depois: só STATE.md mudou por coordenação; conjunto de arquivos src/tests e diff
byte a byte iguais ao baseline `/tmp/lw92-sensor-diff-before`; nenhum stash usado.

## Gates e desempenho
- Antes: 2 testes focados / 1 s (`/tmp/lw92-before.log`).
- Depois autor: 4 testes / 1 s; confirmado independente: 4 testes / 5 s.
- Integração: 450 passaram, zero falhas/skips, 2m2s (`/tmp/lw92-integration-all.log`).
- Gate final `bash scripts/verify.sh`: PASS, `verify: OK`; build e lint verdes.
- Unit: 2459 passaram, 0 falhas, 3 skips existentes para regravação manual de
  baselines (Periods.ZZZ_record_baseline, History.Record_history_collapse_baseline,
  History.Record_history_index_baseline), fora do diff; duração 1m26s.
- Produção e cliente sem alterações: FPS/custo NPC-tick idênticos por construção.
- Mudança cirúrgica, sem dependências, abstrações nem escopo extra.
- Regras: `rules/tests.md`; assertions agrupam um único contrato lógico por teste.
- UAT visual não aplicável. Nenhuma lição nova: nenhum mutante sobreviveu.

**Result**: PASS. Overall: pronto; gate final e integração verdes.
Sensor: PASS 3/3; critérios funcionais cobertos; sem gap de implementação.
