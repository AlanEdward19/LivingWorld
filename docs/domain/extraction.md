# Extração: um padrão só para toda ação que rende item (#243, épico #228)

Cortar árvore, minerar, colher, trabalhar numa oficina: a ação **rende item aos poucos**, a fonte é **estado do mundo e finita**, e quem
extrai (o corpo do jogador ou o NPC da obra) usa a **mesma fonte** e a esgota. Hoje o padrão está ligado à árvore; o minério entra no mesmo
molde quando o mapa tiver veio (o mapa e os cenários de hoje não têm minério nem pedra como recurso; `Rocks` é só enfeite do cliente).

## O padrão
1. **Rendimento aos poucos.** A unidade `k` sai da fonte e vai para as mãos em `início + k × ritmo`, o ritmo por unidade dos NPCs da obra
   (`WorkHaul.CutHours`). É um evento agendado por unidade (`BodyTaskSystem`, `body-task`): vale dentro da hora e nos ticks de 1 h do salto de
   tempo (cada unidade entra no tick que passa o instante dela). Nada é entregue de antemão e `interrupt` não devolve nem completa: deixa o
   que já saiu. A sessão dura até a árvore acabar, no limite do que cabe nas mãos; ficar o período todo derruba a árvore.
2. **Fonte com estado finito.** A árvore natural é regra (`PlayerNature.TreeAt`, mesmo `cell_hash` do `scenery.gd`): custo zero para o mapa
   inteiro. Só as **tocadas** ficam no livro `WorldState.Trees` (`TreeLedger`: célula, madeira que sobrou, desde quando cresce), ordenado por
   (linha, coluna), no snapshot e no hash; nulo (e fora do hash) enquanto ninguém corta. Madeira inteira: 30–70% de `HouseMaterialUnits`
   (6 a 14 un.), pelo mesmo sorteio do porte da árvore no cliente. Estágio: `whole` (inteira), `cut` (em parte), `stump` (toco).
3. **Volta a crescer.** 1 unidade a cada 30 dias do calendário, conta pura do livro e do relógio (`PlayerNature.Left`), sem varredura. Regra
   simples enquanto a flora por planta (#199) não existe; lá ela troca por espécie, idade e estação. A árvore que voltou inteira sai do livro.
4. **Coesão com os NPCs.** `WorkHaul.WoodsFor` escolhe a árvore viva mais perto do lote (até 40 células, alcançável a pé); cada unidade que o
   NPC corta sai da madeira dela (`PlayerNature.Take`) e, esgotada, a viagem segue para a viva mais perto. Célula sem árvore do motor (mata
   do mapa, borda da cidade) corta como antes: o mundo nunca trava por falta de árvore.
5. **O cliente desenha o estado do motor.** `GET /world/trees` traz só as tocadas (`x, y, left, capacity, stage`); `scenery.apply_trees` põe
   copa menor na cortada e, no toco, copa zero e tronco baixo; o cliente não põe árvore inteira onde o motor diz toco.

## Contrato (só aditivo)
`busy.units`, `busy.done`, `busy.resourceId` na pose (coleta); `GET /world/trees`; a oferta `nature` só existe para árvore com madeira.

## Mira do E
A ação principal é a do alvo sob o olhar da câmera (`PlayerFrame.aimed_first`); a pessoa ao lado segue na roda (segurar E).
