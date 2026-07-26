# ADR-0001: Monólito modular em .NET 10, uma solution, camadas por projeto

- **Status**: aceito
- **Data**: 2026-07-26
- **Decisores**: Alan

## Contexto
O Living World precisa simular milhares a milhões de indivíduos com tempo de execução
previsível, servir uma API, workers de background, um cliente web e, mais tarde, Unreal.
O escopo tem 13 fases; a maior parte do risco está no motor, não na topologia de deploy.
Um único desenvolvedor toca todos os módulos.

## Decisão
Vamos usar **.NET 10 / C#** num **monólito modular**: uma solution `LivingWorld.sln` com
um projeto por camada — `Domain`, `Simulation`, `AI`, `Infrastructure`, `Api`, `Workers`,
mais `Web` (fora do .NET, ver ADR-0003) e `Unreal` depois.

Direção das dependências, imposta por referência de projeto e por teste de arquitetura:
`Domain` não referencia nada · `Simulation` → `Domain` · `AI`/`Infrastructure` → `Domain`
· `Api`/`Workers` → todos. **`Domain` e `Simulation` nunca referenciam `AI`.**

`Domain` é puro: sem I/O, sem EF, sem HTTP.

## Alternativas consideradas
- **Microsserviços desde o início** — a simulação é um laço fortemente acoplado sobre um
  estado compartilhado; distribuí-lo agora paga latência de rede e complexidade
  operacional para resolver um problema de escala que ainda não existe.
- **Projeto único** — sem fronteira compilada, `Domain` acumula EF e HTTP em poucas
  semanas. A referência de projeto é o sensor mais barato que existe contra isso.
- **Rust / C++** — melhor teto de performance, mas custo de desenvolvimento maior e
  integração pior com web e ferramental. .NET 10 entrega folga suficiente para o alvo.

## Consequências
- **Positivas**: fronteiras verificadas pelo compilador; um comando builda tudo; o
  monólito modular é ponto de partida natural para extrair um serviço depois, se preciso.
- **Negativas / trade-offs**: escalabilidade horizontal do motor fica adiada; um worker
  pesado e a API compartilham o mesmo ciclo de release.
- **Follow-ups**: teste de arquitetura na Fase 0 que falha o build se a direção das
  dependências for violada. ADR novo se algum módulo precisar escalar sozinho.
