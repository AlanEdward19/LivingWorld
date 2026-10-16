# ADR-0027: identidade do mundo (`GET /world/info`) e pausa de admin visível a todos

- **Status**: aceito
- **Data**: 2026-10-15
- **Decisores**: usuário (pediu resolver as dívidas das telas do cliente, issue #77)

## Contexto
Três lacunas do cliente Godot pediam contrato novo: (1) a faixa "Pausado por X desde <data>: <motivo>" (ADR-0015) só aparecia para
quem pausou, porque ator e motivo só saem das rotas de admin; (2) o nome dos recursos nas vendas vem do catálogo do período, mas
nenhuma rota dizia de que período o mundo em andamento nasceu; (3) os rótulos dos eixos de tecnologia estavam fixos no cliente,
embora o cenário os configure (`EmergenceRules.AxisNames`).

## Decisão
- `GET /simulation/status` ganha campos opcionais `pausedBy`, `pauseReason`, `pausedAtTick`, `pauseMaxTicks` (nulos fora de pausa
  de admin). Aditivo: clientes antigos ignoram.
- Nova rota `GET /world/info` → `{ name, periodId, technologyAxes[] }`.
- `POST /worlds/create` aceita `periodId` opcional. `WorldState.PeriodId` é **volátil** (rótulo, fora do hash canônico), como
  `Name`: entra no snapshot por reflexão e snapshots antigos sem o campo reidratam com vazio. Sem migração.
- Contratos congelados passam a ter **campo opcional tipado**: chave terminada em `?` pode faltar ou vir nula, mas se vier com valor
  tem de cumprir a forma (antes `"null"` aceitava qualquer coisa).

## Alternativas consideradas
- Guardar o período só no cliente — não sobrevive a trocar de máquina e não cobre mundo criado por outro cliente.
- Persistir o período numa coluna nova — migração e mudança de formato de snapshot sem ganho: o campo é só rótulo.

## Consequências
- **Positivas**: todo jogador vê quem pausou e por quê; mundo continuado nomeia recursos; eixos vêm do cenário.
- **Negativas / trade-offs**: ator e motivo da pausa ficam públicos (decisão do ADR-0015: a faixa é para todos); mundo criado antes
  desta mudança, ou sem `periodId`, segue com período vazio (recursos como número).
- **Follow-ups**: se o período precisar ser canônico (ex.: regras por período), reabrir com ADR e migração.
