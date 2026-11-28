# 3º teste de jogo de UX do sandbox do casal (#246, 2026-11-28)

Jogado no build de `19627aa1` (API própria, porta 5981, cliente Godot 1600x900, **Sandbox: casal**, passos de `menu_steps.gd`/`dev_steps.gd`) como jogador do Sims:
banho, jornal de vagas, candidatura, ida ao trabalho dos dois, salário, cozinhar e comer, B (comprar), beijo, briga e pazes, 300× até a noite, dormir, o outro vivendo
sozinho. Capturas em `/private/tmp/claude-501/-Users-aoliveira-Desktop-Projects-LivingWorld/7392251a-1ec6-4bf2-8382-62ad34632fdc/scratchpad/play/playtest-ux-3/` (só o número).
**Limite**: o dia 2 e 3 inteiros não foram jogados (cada rodada de captura nasce num mundo novo; 1 dia a 300× são ~5 min reais): vi o dia 1 completo (07:00 a 22:00)
e o amanhecer do dia 2 pelo motor. Gesto de beijo/abraço não foi provado em tela (o passo `walkperson` esgotou 30 s sem chegar; ver P1-3). **[feito]** = corrigido aqui.

## Nota
- **UX 6,5/10.** Entrada boa: casa de dia, relógio, velocidades, cartão com humor e momentos, barra do uso ("Tomar banho — 8% · faltam 4 min", `12`), toasts claros ("Contratado",
  "Marina saiu para o trabalho. Volta 14:15", `35`), roda social legível (`18`), modo B com loja e beleza do cômodo (`55`: o melhor da fatia). Perde por: dia que corre sem
  aviso a 300×, câmera cheia de parede, dois bonecos iguais, cartões que se sobrepõem, o fim de turno sem o corpo em casa.
- **Jogabilidade 5/10.** Há laços (trabalhar, pagar, cozinhar, desejos), mas nada pressiona: aos 100% de humor às 07:20, sem fome nem sono até a tarde; os desejos somem sem
  novos ("desejos: []" à tarde, `dL`); dinheiro 98 sem em que gastar a não ser móvel; um dia útil a 300× é "esperar 80 s".
- **Realismo das interações 5,5/10.** Beijo, abraço, discussão e pazes entram com efeito e momento (`Brigou -3`, `Fez as pazes +2`); o outro come, vê TV e vai ao trabalho
  sozinho. Mas brigar não custa nada (afeição -6, depois +6 com um pedido de desculpa de 10 s), "Irritado" com nota "Radiante 100", e comer à mesa com fome 100% rende momento.

## P0 — impede jogar como no The Sims
1. **O dia útil estoura o turno do outro a 300×** (`33`, `34`): Marina "vai ao trabalho", a câmera passa a Rafael (que também tem turno às 09:00), a 300× a janela de 1 h dura
   12 s, ele falta ("Faltou ao trabalho (1 de 3). Sem pagamento." às 10:37) e 3 faltas demitem. O aviso `shift-soon` só sai na hora exata: quem assume o corpo depois
   (a troca da câmera) nunca o recebe. **[feito]** `player_hud`: ao abrir a janela do turno (`job.canGo`, inclui o corpo assumido já dentro dela) o tempo desce à primeira
   velocidade do motor e avisa "Hora do trabalho: ... E: Ir ao trabalho" (`35`; `test_careers`). Falta (motor): o aviso na hora exata deveria valer para quem assume depois e
   o jogador poder acelerar de novo não deveria bastar para ser demitido sem uma confirmação ("Pular o turno de Rafael?").
2. **Depois do turno a câmera ficava no corpo ausente** (`51`, `62`): com Marina de volta às 14:15, o HUD seguia em Rafael ("Trabalhando", comer/cozinhar silenciosamente
   inúteis) até as 17:40. **[feito]** a volta de outra pessoa (aviso `returned`) com este corpo fora leva a câmera a quem está em casa.

## P1 — estraga a experiência
1. **Corpo reaparece fora da casa e o E principal é "Vender · Sua casa"** (`62`): a volta do turno põe a pessoa na soleira, olhando a fachada; o motor deixa a venda por último, mas
   o cliente mira a casa. Sims: ao voltar do trabalho o Sim entra sozinho. Proposta: reaparecer dentro (porta) ou mirar a casa oferecer "Entrar", e vender só no B → "Minha casa".
2. **Humor satura e não pesa**: Marina 100 "Radiante" aos 20 min; "Irritado" com nota 100; sem desejos depois do primeiro dia. Proposta (motor, dado em `moods`): nota 0-100 pela
   média dos momentos com peso por necessidade, desejos novos a cada marco, brigas que duram (confiança cai mais que sobe).
3. **Não dá para ir a pessoa/lugar com um clique** (`walkperson` 30 s sem chegar, `80`): o beijo saiu com Rafael no canto da tela, sem os corpos juntos. O Sims anda até o alvo
   (#246 tinha "ir até" no E; falta para a roda social a distância e o mercado, que só se alcança por WASD, `67`).
4. **Dois bonecos iguais** (`18`, `62`): mesmo chapéu, casaco e calça; só o rótulo distingue. Proposta: aparência do cenário por pessoa (dado) — o criador já tem.
5. **Cartões se sobrepõem** (`18`): o painel da relação ("Rafael / Casados") cobre o cartão da Marina ("Marina" sob o texto). Proposta: empilhar acima do cartão (cliente).
6. **Casa vazia sem explicação** (`51`): com os dois fora, o ator controlado é o "ausente"; o cartão diz "Trabalhando" mas a cena mostra a cozinha e o E continua com "Cadeira".
   Proposta: cartão "no trabalho até 17:40" sobre a cena e roda desligada (o motor já responde 409).
7. **Comer à mesa sem fome rende momento** ("Refeição caseira +2" com fome 100%, `dI`): dá para empilhar humor; o uso deveria recusar ou render menos (dado: `needBelow`).
8. **Câmera de interior sem equilíbrio**: ora de fora e de lado (`35`, `19`), ora colada na parede com o corpo fora do centro; no `42` ("Paredes: inteiras") o jogador não vê nada.
   Proposta: enquadrar a pessoa e a primeira parede com corte automático (cutaway) no pitch alto; `V` como override.

## P2 — polimento
1. Rótulos de prédio se sobrepõem na rua ("Loja de móveis" e "Mercado" viram "Ljarcadoóvei", `67`). 2. B fora de casa não diz nada (silencioso, `dL`).
3. Lista de momentos longa no cartão ("+2 momentos", `19`). 4. "Ofício nível 2 de 10" / "Morador nível 0" em Metas (`03`): jargão de sistema. 5. Escuro ao entardecer: os bonecos
viram silhuetas na porta (`62`). 6. Durações do catálogo trocam por situação (TV 13/9/16 min, mesa 1 a 3 min): confunde o rótulo. 7. Comer à mesa leva 1 min de jogo (não parece refeição).

## Próximas 5 prioridades
1. Ir até: clique em pessoa/móvel/lugar (e "Ir ao mercado") com pathfinding no cliente; sem isso o casal social e a compra de comida não fluem. (cliente + motor)
2. Humor com stakes (P1-2): nota que cai e que dura, briga com consequência, desejos e metas novas por dia e por semana. (motor, dado)
3. O turno sem armadilha (P0-1): aviso para quem assume depois, "Pular turno?" e parada do tempo em eventos (chegada de conta, turno, fome crítica). (motor + cliente)
4. A volta do trabalho em casa e a cena com os dois fora (P1-1, P1-6): corpo entra pela porta, cartão "no trabalho", câmera acompanha o carpool. (cliente + motor)
5. Aparência e luz: um rosto/roupa por pessoa, luz de cômodo à noite, câmera de interior com cutaway e corpo centrado (P1-4, P1-8, P2-5). (cliente + dado)

## Corrigido neste passo (só cliente, `tests/test_careers.gd`, 784 testes do cliente verdes)
- `player_hud._note_career`: tempo cai ao abrir a janela do turno (`shift_speed_target`, primeira velocidade do cenário, sem número no código) e avisa quem assumiu o
  corpo já dentro da janela; a volta de outra pessoa com este corpo fora chama `switch_body` (`follow_home_requests`). Decisão: AD-231.

## Corrigido em #267 (AD-234)
- **P0-1**: o aviso do turno vale a quem assume depois (`CareerWork.NoticeOnTakeover`); `Careers.rules.stopKinds` (dado) manda descer o tempo; "Ir agora" e "Pular para a volta do turno" (`accelerate` com `untilReturn`).
- **P1-1**: a volta do turno põe o corpo conduzido dentro de casa; a venda da casa só do menu dela (nunca na soleira). **P1-2**: humor (`scoreSwing`, `emotionCap`, `falloff`, `scoreMin/Max`, `wishDailyPool`), briga que custa.
- **P1-3**: clique = ir até/interagir (`POST /players/{id}/go`); golpe no R. **P1-4**: aparência autorada por pessoa e chapéu pelo traje. **P1-5**: relação empilhada. **P1-6**: cena "Os dois estão fora".
- **P1-7**: `minShare` (comer sem fome). **P1-8**: câmera de interior sobre a casa. **P2-1/2**: rótulos sem sobreposição; B fora de casa avisa.
