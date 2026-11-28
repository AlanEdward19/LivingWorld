# 2º teste de jogo de UX do sandbox do casal (#246, 2026-11-28)

Jogado no build de `a80e6248` (API própria, porta 5471, cliente Godot 1600x900, **Sandbox: casal**), como jogador do Sims: banho, vaso, pia, geladeira, mesa, sofá, TV,
conversa, abraço, beijo, briga (pedidos), `accel` até a noite, troca com G. Capturas em
`/private/tmp/claude-501/-Users-aoliveira-Desktop-Projects-LivingWorld/7392251a-1ec6-4bf2-8382-62ad34632fdc/scratchpad/play/playtest-ux-2/` (só o número, ex. `04`).
Ritmo medido: a 3×, 21 min de jogo levaram ~9 min reais; um dia a 3× são ~8 h reais. **Limite do teste**: não consegui provar o gesto do abraço, do beijo e da briga
na tela (ver P0-2); beijo e briga nem chegaram a rodar porque o parceiro saiu do alcance. Itens **[feito]** foram corrigidos aqui (fim do arquivo).

## P0 — impede jogar como no The Sims
1. **O uso de móvel disparava erro de script no retorno** (sem aviso "✓ ..." do uso; `log`: `Invalid call 'String' constructor` em `player_hud.gd:1671` a cada uso). O motor
   devolve `outcome` como dicionário de efeitos; o cliente fazia `String(outcome)`. Sims: nenhum. Correção **[feito]**: só trata `outcome` em texto (`test_playtest_ux2`).
2. **Gesto social não aparece** (`15-abraco-03/05/09`): "✓ Abraçar" e "Rafael retribuiu o abraço (+5)" saem, mas na tela os dois nunca ficam abraçados: o corpo "vai até"
   (`Indo: Abraçar…`), o parceiro some atrás de parede/móvel e a barra termina. Pior: a partir da cadeira (`14`, `15-abraco-5` antigo) Marina fica presa na mesma célula e o
   parceiro sai da casa (12,5; 12,5). Sims: câmera enquadra os dois, a interação toca animação visível e para só quando acaba. Correção **[feito, parcial]**: câmera a prumo e
   no ponto médio dos dois; falta confirmar o gesto (cliente `npc_actor.gd` + motor: ir até a pessoa não pode travar em móvel).
3. **Câmera escondia o corpo no uso** (`04`, `06`): vista de fora da casa, de lado, com geladeira/fogão no primeiro plano; vaso/chuveiro invisíveis atrás da parede.
   Sims: a câmera de cena mostra o objeto e quem o usa. **[feito]**: vista mais a prumo (`SCENE_PITCH`), depois `40`/`41`.
4. **Um dia inteiro não cabe na sessão**: 3× = 3 s de jogo por segundo real; banho (3 min de jogo) levou ~53 s reais, comer à mesa 30 min de jogo e uma tarde são
   horas. O Sims 3× passa um dia em ~8 min. O botão que pula (`accel`, `sleepfast`) existe no HUD antigo, mas não aparece no HUD novo (só 0/1/2/3). Correção: velocidade 4× (ou
   "pular até algo chamar") nos botões do canto, só sozinho no mundo. **(cliente + motor: `MaxPlayerSpeedValue`)**
5. **Nada pede nada**: às 19:00 (depois de 12 h parado) as barras mais baixas são Bexiga 34% e Diversão 56% (`31`); ninguém passa fome ou sono. Sem urgência não há o que
   cuidar ("rotina da manhã" é opcional). Sims: barras caem rápido o bastante para cobrar a cada ~2 h de jogo. **(motor: `needRates` do cenário)**

## P1 — estraga a experiência
6. **Interagir com o parceiro exige chegar a pé a menos de ~2 m** e o menu só vale ali; Marina a 4,5 m não vê "Interagir ▸" (`offers:Rafael` vazio). Sims: clique na pessoa
   e ela anda até lá. **(cliente: E/clique na pessoa chama a interação com "ir até")**
7. **Menu social desliga tudo com o corpo ocupado** ("você está ocupado agora" nas 11 interações, sentada na cadeira). Sims: enfileira ou interrompe com confirmação. **(cliente + motor)**
8. **O outro vive pouco**: Rafael "ação=<null>" 20 min seguidos, sem lavar-se (higiene 84%) nem comer; às 19:00 os dois têm **as mesmas barras** (fome 76, sede 64...).
   Sims: cada um tem ritmo próprio. **(motor: diferença por traço/idade)**
9. **Duas ofertas para a mesma pessoa**: "Rafael: Conversar" (talk antigo) e "Interagir ▸ Bater papo". Sims tem um menu só. **(cliente: esconder `talk` quando há `social_menu`)**
10. **Cartão dizia "Parado" enquanto tomava banho** (`04`, só a barra de cima dizia). **[feito]** (`41`: "Usar o vaso").
11. **Trocar com G deixa o novo corpo atrás de parede** (`32`: o parceiro vira só uma etiqueta "Marina / Parado" flutuando). Sims: retratos dos moradores sempre à vista, clique para
    trocar. **(cliente: retratos; a câmera da troca precisa de `set_snap` e do enquadramento de cena)**
12. **Rótulo "15 min" do banho com 3 min no relógio**: a duração do catálogo (min) e o relógio do HUD (07:00 → 07:03) não batem, e o relógio fica parado em 07:00 por
    ~1 min reais. **(cliente: relógio relido ao terminar o uso; motor: unidade da duração)**
13. **Primeiro segundo**: agir antes do corpo ser colocado (`where`: `fine=inf`, prédio -1) desfaz o foco da casa e mostra o aviso "O corpo está fora da casa". Ruído, raro.

## P2 — polimento
14. O alvo da mira diz "Mesa" ou "Cadeira" enquanto se usa o vaso (`06`, `10`): a legenda `E ...` deveria sumir durante um uso.
15. "Parou: Sentar e comer." fica por ~20 s sob a barra depois de interromper (`14`, `15`); os avisos empilham 2 linhas.
16. A TV e o abajur de dia parecem blocos escuros (`02`); à noite o abajur acende bonito (`31`), mas o resto do cômodo cai no azul.
17. Árvores em cone repetidas e uma só cor de piso: o quintal não parece lar. Sims: lote cuidado, calçada, vizinho.

## O que falta para ser divertido (as 5 maiores lacunas)
1. **Objetivos e aspirações**: nenhuma meta de curto prazo ("fique com 80% de Diversão") nem aspiração de vida; "Metas" lista habilidade técnica. Sem isso não há "só mais um dia".
2. **Humor e emoções visíveis**: há `mood` no motor (Radiante) mas só no painel da relação; sem ícone de humor no cartão nem no boneco, sem moodlets ("tomou um bom banho").
3. **Carreira e dinheiro**: "Dinheiro 50" não muda; nenhum emprego, salário, conta. Sem gasto nem ganho o jogador não tem escolha. **[feito em #263]**: carreiras e contas em dados, turno fora do mapa, salário do empregador, promoção, jornal de vagas e painel O (AD-229, `docs/domain/careers-and-bills.md`).
4. **Comprar, construir e decorar**: o modo compra do Sims é metade da graça; aqui a casa é fixa (e "Vender sua casa" é a última oferta do E).
5. **Eventos e vizinhos**: sem visita, festa, chamada de emprego, carteiro; o mundo só tem 2 pessoas e uma cerca. Rua e vizinhos (#257) abrem isso.

## Corrigido neste passo (só cliente, `tests/test_playtest_ux2.gd`)
- **F1** `player_hud.interact` e `done_text`: `outcome` pode ser dicionário (todo uso). **F2** `camera_rig.set_scene_framing` (`SCENE_PITCH` -58°, ponto médio com o parceiro),
  ligado por `app._on_player_busy` em uso e interação (inclui o "ir até"). **F3** cartão diz a ação do corpo (`player_frame._doing`). Passos de captura: `speed`, `key`, `offers`, `walkperson`.
- Capturas: `40-vaso-depois`, `41-banho-depois` (depois), `04-banho`, `06-vaso` (antes). Decisão: AD-225.
