# ADR-0051: câmera, controle e interação do modo jogador (emenda o ADR-0047; detalha o ADR-0049)

- **Status**: proposto (precisa do aceite do dono; implementado na AD-197d atrás desta proposta)
- **Data**: 2026-11-24
- **Decisores**: dono do projeto (decisão de produto de 2026-11-23); rascunho por agente

## Contexto
O ADR-0047 fixou "Godot 3D isométrico" para todo o cliente. O dono decidiu em 2026-11-23 que "a camada do jogador, a câmera não
será isométrica, será 3ª ou 1ª pessoa; usado como um jogo de RPG mesmo, o jogador anda no WASD ou setas, pode interagir com as
coisas, bater com clique". O ADR-0049 (proposto) registrou essa direção e o contrato geral; faltava decidir o concreto: teclas, o
que o mouse faz, como o passo fica sem atraso, quem diz o que dá para interagir e como as ordens contra a vontade da pessoa
possuída são recusadas. Regra do projeto: o cliente só desenha o que o motor diz.

## Decisão
1. **Câmera** (emenda o ADR-0047 só para quem tem personagem encarnado): ao encarnar, perspectiva em **3ª pessoa** (4,5 células
   atrás, olhos a 1,6), **C** alterna para a **1ª pessoa** e volta; o **mouse olha** (botão direito arrastando, ou sempre com o
   cursor preso), inclinação limitada a −70°..35°. Sair do personagem volta à isométrica. Espectador e admin não mudam.
2. **Andar**: WASD/setas relativos ao olhar da câmera, nas 8 direções da grade, como **intenção** `move {dx, dy}` pelo canal
   persistente (ADR-0048). O motor valida o chão (`PlayerGround`): dentro do mapa, sem parede (planta de cada prédio na grade do
   mundo), entre fora e dentro **só pela porta** (da soleira), sem gente na célula (pela pose: o viajante no ponto da rota, quem
   está num prédio na célula em que o motor o pôs), sem cortar canto de parede. Andar é tomar o rumo: a viagem do motor para e,
   sem ação pedida, o corpo fica à toa. O cliente **prevê** o passo e **corrige** pela posição que o motor responde; recusa volta.
   Segurar a tecla anda um passo a cada 0,35 s (o motor ainda não limita a velocidade pelo corpo: issue).
3. **Interagir (E)**: o motor diz o que o corpo alcança (`reach` no canal, `GET /players/{playerId}/reach`): porta (entrar/sair),
   despensa da casa (pegar/guardar, pelas mãos e pelo estoque reais), banca (comprar/vender pela troca de mercado de sempre), cama
   (dormir), pessoa (conversar, bater e, com o filho dependente, abandonar), a própria casa (vender). O cliente mostra **só** isso;
   `interact {kind, id, verb, resourceId?, quantity?}` é validado contra a oferta do mesmo instante. A conversa aceita abre a
   sessão de chat de sempre.
4. **Golpe no clique**: encarnado, o clique bate no NPC sob o cursor (`strike`); o motor valida o alcance.
5. **Ordens contra o que a pessoa valoriza** (posse comum): uma regra só, sem lista por ação. O peso sai do que a ordem faz:
   max(cuidado com cada pessoa ferida × o quanto a ordem a fere, altruísmo × 0,8 × o pior dano, aversão a risco × 0,8 se tira a
   casa dela). Ferir: dano inteiro no alvo; abandonar o filho: dano inteiro nele; vender a casa: 0,8 em cada um que mora nela e a
   casa dela. A recusa é a disputa do AD-197f (limiar 60 do dono): 409 com o motivo, o mundo intacto, recusa no `players.log` e no
   replay. Posse plena (admin) nunca recusa; a objeção fica com a pessoa presa.

## Alternativas consideradas
- **Manter a isométrica com WASD** (o de antes desta fatia) — rejeitada pelo dono.
- **Lista fixa de ordens recusáveis** (vender casa, abandonar filho, ferir) — cada ação nova pediria uma regra; o peso pelo efeito
  vale para as ordens que vierem.
- **Menu de interações montado no cliente** pelo que ele vê — inventaria possibilidades que o motor não aceita.
- **Posição mandada pelo cliente** — burla parede e replay (já rejeitada no ADR-0049).

## Consequências
- **Positivas**: o jogador anda e age como pessoa, pelos sistemas de qualquer NPC; replay reproduzível; o espectador não muda.
- **Negativas / trade-offs**: dois eventos novos (`HouseSold`, `ChildAbandoned`), duas rotas e dois comandos no canal (acréscimos);
  o passo varre prédios e pessoas da cidade por comando (só com jogador; medido); a casa sem `Pricing` no cenário é vendida pela
  oferta da compradora (metade da carteira do chefe, escolha do modelo).
- **Follow-ups**: feitos na AD-197j: passo no tempo do corpo (recusa antes do fim, gasta sono), escada e andares de cima, fonte,
  lavoura, espólio e furto na oferta, golpe animado, Z prende o cursor, mira no centro. Em aberto: posição contínua entre células
  (uma célula custa ~3 min de mundo a pé: a 1:1 o passo leva minutos reais) e objeto inventado na oferta.
