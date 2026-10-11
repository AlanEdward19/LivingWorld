# ADR-0022: Godot como motor gráfico; cliente web aposentado

- **Status**: aceito (substitui o ADR-0003)
- **Data**: 2026-10-10
- **Decisores**: Alan

## Contexto
O cliente web (React+TS, `web/` e `web-demo/`) deixou o desenvolvimento lento: segundo
toolchain (Node/npm) dentro do gate, tipos TS gerados a partir do OpenAPI (build da API +
processo HTTP a cada `verify.sh`), e um mapa 2D/VTT que cresceu junto com a simulação.
O produto precisa de um motor gráfico de verdade (cenas, câmera, sprites, animação), não de
uma página web. A Fase 14 (Unreal) segue adiada.

## Decisão
Vamos usar **Godot** como cliente gráfico. O contrato continua sendo a API do motor
(`LivingWorld.Api`, projeções em `Api/Visual`): o motor segue dono do estado e o cliente só
lê/renderiza. O cliente web sai do gate (`build.sh`, `test.sh`, `verify.sh`); o código em
`web/` e `web-demo/` fica no repo, congelado, até decisão explícita de remoção.
A spec do cliente Godot é o próximo passo e substitui o escopo da Fase 15.

## Alternativas consideradas
- **Manter o web** — descartado: custo de integração alto e nenhum ganho para o objetivo
  de motor gráfico.
- **Unreal agora** — adiado (Fase 14): peso e curva maiores do que o necessário para o
  primeiro cliente visual.

## Consequências
- **Positivas**: gate sem Node; `verify.sh` mais curto; cliente alinhado ao que o produto precisa.
- **Negativas / trade-offs**: tipos do cliente deixam de ser gerados do OpenAPI até a spec
  do Godot definir o formato do contrato (C# do Godot pode referenciar projetos .NET direto).
- **Follow-ups**: spec do cliente Godot; decidir remoção de `web/`, `web-demo/` e
  `scripts/generate-web-types.sh`.
