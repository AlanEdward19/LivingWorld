# Phase 16 — Tasks avançadas

**Parent**: [tasks.md](tasks.md) · **Status**: Complete (independently validated)

## Plano

```text
T7 -> T8 -> T9 -> T10
```

### T7 — Cinco arquétipos fixos de regressão

**Depends on**: T6 · **Requirements**: POW-09
**Done when**: Vampiro, Lobisomem, Lanterna Verde, Kryptoniano e Velocista passam por aquisição,
manifestação e invocação genéricas, com sinais distintivos assertados e nenhum caso em produção.
**Tests**: scenario theory (5 casos) · **Gate**: Quick · **Status**: Complete

### T8 — Voo e velocidade física

**Depends on**: T7 · **Requirements**: POW-10
**Done when**: modificadores manifestados alteram passos reais por hora; voo ignora terreno, não
paredes/interiores, e pousa em célula válida; posição, colisão, custo e determinismo são provados.
**Tests**: unit/determinism/integration/web · **Gate**: Build · **Status**: Complete

### T9 — Construtos físicos temporários

**Depends on**: T8 · **Requirements**: POW-11
**Done when**: efeito genérico cria footprint ocupante com durabilidade/expiração, registra cadeia
causal, bloqueia células e remove sem vazamento econômico; fixture Lanterna Verde o exerce.
**Tests**: unit/determinism/integration/web · **Gate**: Final · **Status**: Complete (49/49 + 446/446 web)

### T10 — Autoria operacional na web

**Depends on**: T9 · **Requirements**: POW-12
**Done when**: selecionar NPC permite conceder/revogar/invocar descritores do cenário e posicionar
construtos; templates apenas preenchem o editor genérico e toda mutação passa pela API/motor.
**Tests**: API integration + component/serialization · **Gate**: Build · **Status**: Complete (54/54 + 451/451 web)
