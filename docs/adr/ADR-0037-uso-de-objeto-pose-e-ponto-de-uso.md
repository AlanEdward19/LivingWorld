# ADR-0037: Uso de objeto: pose e ponto de uso declarados, objeto em uso informado pelo servidor

- **Status**: aceito
- **Data**: 2026-10-16
- **Decisores**: Alan (issue #47, fatia 2; proposta aprovada em chat antes do código)

## Contexto
A fatia 1 (ADR-0034) deu forma de dado aos objetos. Falta ligar o objeto ao NPC que o usa: onde o corpo encosta e que pose faz.
Hoje a pose vem só da ação (`animation_catalog.gd`) e a posição só da célula do servidor.

## Decisão
- **Descritor**: cada `usePoints[]` ganha `action` (id de `ActionType` que o ponto atende, opcional) e `pose` (opcional, do vocabulário
  do cliente: `idle|walk|sleep|work|eat|socialize|buy|power`; o "lie" da issue é `sleep` aqui). Id de ação ou pose desconhecidos são recusados
  na borda, com o caminho. Um teste confere que `ObjectPoses.Known` é igual a `POSES` do `animation_catalog.gd`.
- **Servidor**: cada ocupante do snapshot e do delta de interior ganha `usingObject` (id do catálogo; omitido se nenhum). É o primeiro objeto, na ordem
  da mobília do tipo de prédio, com um ponto de uso que atende a ação atual do NPC. Puro e determinístico: sem catálogo, ninguém "usa" nada.
  Mudar só o objeto em uso conta como `Moved` no delta. Não entra no estado do mundo nem no hash.
- **Cliente**: `animation_catalog.pose_for(ação, pose_do_objeto)` resolve a pose (a declarada se o cliente a conhece, senão a da ação).
  A `interior_view` coloca o NPC no ponto de uso da peça (térreo; fila por id de NPC, volta ao início se faltar peça), vira para `facing`
  e dispensa a cama própria do NPC. Sem `usingObject`, sem peça colocada, outro andar ou ponto de outra ação: posição e pose de sempre.
- **Posição das peças** segue no cliente (célula escolhida pela semente do prédio); a fatia só liga o uso à peça.
- **Mobília padrão**: cama (dormir), mesa (comer), banco (socializar), bigorna e balcão (trabalhar), com ação, pose, offset e facing.

## Alternativas consideradas
- Servidor posiciona as peças — seria mover a disposição inteira para o motor; fora do escopo e sem consumidor causal ainda.
- Pose só no cliente por categoria de objeto — repetiria a regra no código; o dado do cenário manda.
- Novo `ActionType` por objeto — proibido (AD-012).

## Consequências
- **Positivas**: o NPC dorme de fato na cama, come na mesa; mods e invenções declaram uso sem código.
- **Negativas / trade-offs**: ainda só apresentação (efeitos e requisitos são da fatia 3); `usingObject` escolhe pelo tipo de prédio, não pela peça exata.
- **Follow-ups**: fatia 3 (interações e efeitos aplicados pelo motor, que passam a consumir os pontos de uso).
