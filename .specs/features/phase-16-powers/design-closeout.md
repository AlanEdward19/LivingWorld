# Phase 16 — design de fechamento

**Spec**: [spec-closeout.md](spec-closeout.md)

## Resolução e disponibilidade

`ExtraordinaryInvocationEngine` recebe uma origem (`Authored` ou `Triggered`). Um validador puro
cruza origem, `Mode` e manifestação antes de preparar custos. A borda web sempre usa `Authored`;
sistemas futuros usam a entrada `Triggered`. `Passive` continua sendo lido diretamente pelos
adaptadores de locomoção, metabolismo, senescência e apresentação, sem invocação repetida.

Para `ResolutionCheck`, o engine chama o `Resolver` dramático existente com stream
`extraordinary-resolution-{carrier}-{power}-{invocation}`. Capacidade vem de vitalidade e
predisposição do portador; dificuldade vem da maior magnitude declarada e do estado do alvo.
Essa fórmula fica em uma função pura e testada, até existir domínio de maestria específico.
`Guaranteed` retorna sucesso antes de solicitar qualquer stream.

Falhas permanecem dados. Todo token vira evento causal; o adaptador semântico estreito
`carrier.health:N` aplica dano permanente. Resultado parcial escala efeitos numéricos e não
escala custo. O plano inteiro é validado antes da primeira escrita.

## Prevalência e LOD

`ExtraordinaryScenarioData.Prevalence` é canônico e opcional no JSON. Depois de cidades e
população existirem, um seeder percorre cidades/ids ordenados e usa stream derivado da cidade.
Selecionados recebem `ExtraordinaryCarrierState` sem retirar ids do pool. Estado agregado nasce
dormant e é resolvido normalmente ao materializar.

`GlobalCityMarker.KnownCarrierCount` conta portadores vivos materializados da cidade mais ids
portadores ainda presentes no pool. Nenhum nome ou `PowerId` é projetado no LOD global.

## Gate final

Testes unitários discriminam cada ramo. Cenários pareados reutilizam a mesma seed e comparam
direção/ conservação, sem valor demográfico exato. O gate curto cobre dez seeds; horizontes
longos ficam `Category=Scenario` para o `verify.sh`/nightly conforme `rules/eval-criteria.md`.
