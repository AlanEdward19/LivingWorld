# Combate do jogador (#234, épico #228)

O golpe do corpo conduzido tem alcance em metros, tempo e defesa; quem apanha decide o que faz.
Tudo é do motor: o cliente só anima o que o motor devolve. Código: `Simulation/Players/PlayerCombat.cs`.

## Golpe (`strike`)
- **Alcance**: distância em metros entre os corpos (pose fina de quem anda solto, senão o centro da
  célula de 1,5 m) até `StrikeReachMeters` (2,2 m: toda célula vizinha, até a diagonal). Fora, 400 com a
  distância e o alcance na mensagem.
- **Recarga**: `StrikeCooldownMs` (900 ms) dividido pela capacidade de força do corpo (0,8 a 1,2).
  Antes dela, 409 "ainda se recupera do golpe (faltam N ms)". Recusa não entra no registro.
- Fere pelo caminho da briga (`Violence.Fight`: dano pela força, ferida ou morte, testemunha, mágoa,
  vingança de parente, justiça). `CombatTiming = false` volta ao golpe imediato de uma célula.

## Defesa (`dodge`, `block`)
- Janela curta (`DodgeWindowMs` 350, `BlockWindowMs` 700) e recarga da guarda (`GuardCooldownMs` 1200).
- **Esquiva** evita o golpe com chance `DodgeChance` (0,7) pelo corpo e pelo cansaço (sorteio
  `ctx.OneShot`); exausto (sono < `JumpMinSleep`) não esquiva. **Bloqueio** deixa passar só uma fração
  (`BlockPasses` 0,3 ÷ força do corpo, de 0,1 a 0,9) e baixa a chance de golpe mortal.
- Vale para o golpe que cai dentro da janela no instante marcado (`atMs`), não quando o motor o resolve.

## Reação de quem apanha (só para o atingido)
Decisão pura de traços (aversão a risco, estabilidade, impulsividade), medo da psique, saúde, força
relativa, propensão a briga e cuidado pelo agressor (`PossessionWill.CareFor`); sorteio só desempata ±0,05:
- **Revidar** (forte, corajoso, briguento, sem apego): marca um golpe pendente depois de
  `RetaliateWindupMs` (600 ms; 0,5x impulsivo a 1,5x calmo); o NPC desperta na hora e golpeia pelo mesmo
  caminho (`Fight` com `defense`, sem abrir caso novo) se o agressor ainda está ao alcance, senão erra.
- **Fugir** (fraco, assustado, ferido): afasta-se até 2 células pelas regras de chão do passo do jogador.
  Com extroversão >= 40 e quem se importa por perto (`HelpCare`), **pede ajuda**: o amigo marca revide.
- **Recuar** (apego ou hesitação): 1 célula. Dentro de prédio o NPC não se afasta.
- Consequências: mágoa, relação e psique pela briga; memória do pedido de ajuda e de quem acudiu.

## O que o cliente recebe
- `strike` 200: `outcome` (Hit), `damage`, `lethal`, `reaction`/`reactionMs`, `readyInMs`, `pose`.
- `dodge`/`block` 200: `pose`; `pose.combat` (só para quem já lutou): `strikeReadyInMs`, `guard`,
  `guardMs`, `guardReadyInMs`, `lastIncoming` {outcome Hit|Dodged|Blocked|Missed, damage, by, agoMs},
  `reachMeters`. Os `walk` seguintes trazem o mesmo `pose`: é por ele que o revide chega ao cliente.
- Canal: `{"command":"dodge"}`, `{"command":"block"}`; rotas `POST /players/{id}/dodge|block`.

## Reação dos NPCs na projeção (`npcs[].observable.combat`)
Campo **opcional** (omitido sem briga, sem custo no delta): `retaliate`, `flee`, `callHelp` ou `recoil`, enquanto
`ReactionUntilMs` não passou (`ObservableProjector.CombatOf`, só leitura, sem RNG). Mudar a reação muda o observável.

## Cliente Godot (`client/godot`)
- **Comandos**: `player_dodge`/`player_block` (ws, como `walk`). Clique bate; botão direito ou **F** bloqueia (segure: renova
  a guarda quando o motor deixa); **Ctrl/Alt** ou duplo toque numa direção esquiva. Q, E, I/Tab, C, J seguem livres.
- **Estado**: `Scenes/combat_view.gd` guarda só o que o motor disse (recarga, guarda, alcance, último golpe recebido, sem repetir).
- **Corpo** (`npc_actor.gd`): golpe em três tempos (antecipação, impacto, recuo); ferida, esquiva, bloqueio e recuo são poses
  distintas; guarda visível enquanto `guard`. Pela projeção: fugir = correr, revidar = golpe, pedir ajuda = gesto de chamar.
- **Retorno** (`feedback.gd`): número de dano **sobre o alvo** só com `outcome` Hit e `damage` > 0; esquiva/bloqueio têm som
  próprio e nenhum número; recusa só o som neutro e o motivo do motor em aviso discreto.
- **HUD**: barra fina de recarga sob a mira, palavra "Guarda", anel de alcance (`reach_ring.gd`) quando a mira está numa pessoa.

