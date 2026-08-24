# ADR-0021: Potência estende movimento e ocupação existentes

- **Status**: aceito
- **Data**: 2026-08-24
- **Decisor**: Alan

## Contexto

A Fase 16 precisa representar voo, velocidade física e construtos. Tratá-los como animação faria
a web mentir sobre posição/colisão; criar motores paralelos por arquétipo violaria o ADR-0010.

## Decisão

Potência declara modificadores sobre dois responsáveis existentes:

- movimento continua autoritativo por célula e hora; velocidade aumenta passos reais, voo troca
  custo/caminhabilidade de terreno, mas não concede intangibilidade estrutural ou interior;
- construto é ocupação temporária canônica, não `Building`: declara footprint, durabilidade e
  expiração, entra na colisão e sai por evento causal;
- nomes de arquétipo permanecem apenas em fixtures; produção interpreta efeitos genéricos.

## Consequências

- replay, hash e web observam as mesmas posições e ocupações;
- mudança de manifestação no ar exige pouso determinístico válido;
- construtos não cunham recursos nem participam da economia sem adaptador futuro declarado;
- pathfinding/progressão de rota precisará expor passos intermediários, não teleporte ao final.

## Alternativas rejeitadas

- só reduzir `TicksBetween`: muda duração, mas ainda teleporta e não prova velocidade física;
- sprite voador/rápido sem estado: apresentação diverge da simulação;
- prédio temporário: mistura construção econômica com ocupação extraordinária transitória.
