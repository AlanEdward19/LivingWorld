# ADR-0015: Pausa global de admin e separação da trilha de auditoria

- **Status**: aceito
- **Data**: 2026-07-26
- **Decisores**: Alan

## Contexto
Duas colisões entre a Fase 25 (jogadores) e a Fase 26 (console e modo god):

1. **Pausa por sessão é impossível** num scheduler único. Ou a pausa é global, ou
   multiplayer e modo god não coexistem no mesmo branch.
2. **Trilha de auditoria dentro do event log** faria ação de admin entrar no hash canônico.
   Dois mundos idênticos em que um admin apenas *olhou* teriam hashes diferentes, e a
   comparabilidade entre mundos morre.

## Decisão

### Pausa é global e exclusiva do administrador
Um administrador pausa **o branch inteiro**. Todo jogador conectado àquele branch para junto
e vê na tela quem pausou e desde quando. Velocidade segue a mesma regra.

Isso não ameaça o hash porque a Fase 1 já decidiu que **pausa e velocidade são estado do
hospedeiro, não do mundo**: não entram no snapshot, logo não podem alterar o hash por
construção. O teste que já existe (reflexão sobre os campos serializados) protege isso.

Branches diferentes pausam de forma independente — cada um tem relógio próprio (ADR-0012).

### Ação de admin se divide em duas, e só uma toca o mundo
| Tipo | Exemplos | Onde vive | Hash |
|---|---|---|---|
| **Leitura** | busca, inspeção, visão de verdade, pausa, velocidade | store de auditoria **separado** | fora |
| **Intervenção** | reescrever um fato | evento de gênese do branch filho (ADR-0008) | dentro, do **filho** |

O store separado **não** abre mão do append-only: o teste que exige que `UPDATE` e `DELETE`
falhem é propriedade da tabela e se aplica igual à tabela de auditoria.

Intervenção não precisa de trilha paralela porque ela **já é** um fato do mundo: ramifica, e
o evento de gênese do branch registra quem interveio, quando e sobre qual fato — a certidão
de nascimento da linha. A mãe fica byte-idêntica, e a comparabilidade se preserva porque se
comparam mães.

## Alternativas consideradas
- **Pausa por sessão** — cada jogador no seu ritmo e exige um scheduler por sessão, ou seja,
  um mundo por jogador. Deixa de ser mundo compartilhado.
- **Auditoria dentro do event log** — herda append-only de graça e envenena o hash canônico
  com ações que não mudaram nada.
- **Sem auditoria de leitura** — mais barato e inaceitável: modo god vê a verdade histórica,
  e olhar sem registro é exatamente o que não se quer poder fazer.

## Consequências
- **Positivas**: multiplayer e modo god coexistem sem scheduler novo; o hash continua
  comparável entre mundos; intervenção ganha proveniência de graça, dentro do mundo.
- **Negativas / trade-offs**: um admin distraído congela todos os jogadores do branch — é
  poder real e precisa de aviso claro e provavelmente de limite de duração; e a auditoria
  passa a viver em dois lugares (store separado para leitura, log do filho para
  intervenção), o que exige juntar os dois para responder "o que o admin X fez".
- **Follow-ups**: Fases 25 e 26 registram a decisão; limite de duração de pausa fica como
  questão em aberto da Fase 26.
