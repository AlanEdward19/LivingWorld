# Diagnóstico — por que não está jogável (2026-11-27)

Lido no código do branch `feat/gameplay-aaa`. Caminhos relativos à raiz.

## O personagem volta ou não anda
- O motor só move o corpo quando chega um comando de andar (`PlayerWalk.cs:52`), contando no
  máximo 300 ms de mundo por comando (`PlayerRules.cs:171`, `WalkHold`).
- O cliente manda um comando por vez a cada 100 ms ou mais (`player_hud.gd:731-787`) e prevê a
  velocidade em tempo real; o tick do motor anda mais devagar que o relógio quando o ciclo
  demora (`TickLoopService.cs:58-63`). O cliente passa à frente e é puxado de volta
  (`player_body.gd:16`, `:132`).
- Colisões diferentes: o cliente bate só em painéis finos de parede (`wall_panels.gd`); o motor
  recusa parede como célula inteira, porta só em linha reta, quina na diagonal e célula cheia de
  gente (`PlayerGround.cs:126-148`). O cliente entra, o motor recusa, o corpo volta.

## Troca mundo → cidade → casa
- Toda troca refaz tudo: nova assinatura, limpa a loja e reconstrói terreno, prédios e NPCs
  (`session.gd:24-28`, `world_view.gd:156-171`); a casa é uma cena à parte (`interior_stage.gd`).
- O interior só aparece depois de pose → assinatura → snapshot (`app.gd:1012-1018`); no meio a
  figura some e a câmera fica num ponto fixo (`app.gd:1040-1070`).

## Casa minúscula
- Casa inicial 3×3 células (4,5 m), as outras 3 a 4 por lado (`BuildingFootprintGenerator.cs:55`);
  a borda inteira é parede (`:67`). Sobra 1 célula de piso, que vira escada
  (`BuildingLayoutGenerator.cs:29-50`): dentro só cabem a porta e a escada. Sem móveis.
- O cliente desenha um cômodo de 4,5 m com paredes finas; o motor recusa os passos nas células
  de parede, e o corpo volta.

## O visual muda ao entrar
- Na rua a figura recebe roupa do ofício, chapéu, escala da idade e cabelo
  (`npc_renderer.gd:210-221`). Dentro de casa, `interior_view.gd:473` e `:515` não aplicam nada
  disso e caem no corpo base com cor da camisa pelo id (`npc_actor.gd:212-219`); o interior nem
  recebe o dado observável (`InteriorProjector.cs:13-15`).

## O que já existe e será reaproveitado
Móveis posicionados pelo motor (`FurniturePlanner`, `HomeFurniture`), uso de objeto com efeito
(`ObjectUse`), tarefa com duração e interrupção (`BodyTask`), metas e guia, figura procedural com
andar e IK (`npc_rig.gd`), câmera 3ª/1ª pessoa, conversa com fronteira da LLM e provedores
(`RuleBasedLlmProvider`, `OllamaLlmProvider`). Faltam: cômodos (#226), higiene e diversão, objetos
de banho/cozinha/diversão, fila de ações, compra e arrumação de móveis, provedor por jogador.
