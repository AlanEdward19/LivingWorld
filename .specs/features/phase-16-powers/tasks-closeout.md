# Phase 16 — tasks de fechamento

**Parent**: [tasks.md](tasks.md) · **Spec**: [spec-closeout.md](spec-closeout.md)
**Status**: Complete — independently validated

## Plano

```text
T11 -> T12 -> T13
```

## T11 — modos, Resolver e consequências de falha

**Where**: `Simulation/Extraordinary`, autoria API/web, testes Extraordinary
**Depends on**: T10 · **Requirements**: POW-13, POW-14
**Done when**: modos aceitam somente a origem prevista; modo inválido falha no loader;
`ResolutionCheck` é calculado pelo motor com stream do portador; `Guaranteed` não toca esse
stream; custos independem do resultado; parcial escala efeito; toda falha produz consequência
causal e dano declarado altera apenas o portador.
**Tests**: unit/API/web/determinism · **Gate**: Quick
**Status**: Complete (80/80 .NET + 451/451 web; conjunto final)

## T12 — prevalência de cenário e LOD agregado

**Where**: domínio/loader, `ScenarioLoaderV2`, projeção global, editor web
**Depends on**: T11 · **Requirements**: POW-15
**Done when**: prevalência valida e faz round-trip; zero/um têm resultados exatos; seleção é
determinística e conserva o pool; mapa global mostra só contagem conhecida por cidade.
**Tests**: loader/integration/projection/web/determinism · **Gate**: Build
**Status**: Complete (80/80 .NET + 451/451 web; build green)

## T13 — cenários pareados e encerramento

**Where**: `tests/LivingWorld.Tests/Extraordinary`, docs de fase
**Depends on**: T12 · **Requirements**: POW-16
**Done when**: matriz executável cobre mutações declaradas, custo, disabled, conservação,
hereditariedade, cultura e hash; `scripts/verify.sh` passa; verificador independente retorna PASS;
roadmap/spec/tasks ficam concluídos e a fase recebe commit único conforme política do repositório.
**Tests**: unit/scenario/architecture/web · **Gate**: Final
**Status**: Complete (verifier PASS; global 1679/1679 .NET + 451/451 web; types check green)

## Matriz de cobertura

| Critério | Teste mínimo discriminante |
|---|---|
| origem × modo | cada combinação permitida e rejeitada |
| resolução | mesma seed/ids = mesmo resultado; UI não envia resultado |
| custo/falha | sucesso e falha debitam igual; consequência difere |
| prevalência | 0 = nenhum; 1 = todos; pool byte-idêntico |
| LOD | soma materializado + agregado, sem ids/poderes globais |
| causal final | controle/tratamento com mesma seed e assert por tick |

## Gates

- Quick: `bash scripts/test.sh --filter Extraordinary`
- Build: `bash scripts/build.sh`
- Final: `bash scripts/verify.sh`
