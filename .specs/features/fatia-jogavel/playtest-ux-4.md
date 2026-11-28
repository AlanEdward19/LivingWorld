# 4º teste de jogo de UX do sandbox do casal (#246, 2026-11-28)

Jogado no build de `054cd267` (API própria, cliente Godot 1600x900, **Sandbox: casal**, passos de `dev_steps.gd`) como jogador do Sims: manhã (vaso, banho, fogão, mesa), jornal e candidatura,
G (Rafael), "Ir agora" ao turno, pulo até a volta, compra (B) com entrega do Seu Tomás e nas mãos, abraço/elogio/desculpa, 60× e 300× até a madrugada do dia 2. Capturas em
`/private/tmp/claude-501/-Users-aoliveira-Desktop-Projects-LivingWorld/7392251a-1ec6-4bf2-8382-62ad34632fdc/scratchpad/play/playtest-ux-4/` (só o nome). **Limite**: cada rodada nasce num mundo novo e a roda do E
pausa o mundo enquanto aberta (#240), então o dia 1 e meio saiu em três rodadas, não numa só; **briga não foi provada em tela** (Rafael "ocupado com um móvel" às 07:34, e depois a
pessoa conduzida estava na loja). **[feito]** = corrigido aqui.

## Câmera de interior (parte 1) [feito]
- **Antes** (`antes-*`): enquadrava a casa inteira (7x6 células) pela caixa do andar: de lado, parede de dentro tampando o corpo, corpo fora do centro, cômodo pequeno visto de longe.
- **Depois** (`depois-*`): o cliente lê `rooms` do motor (`interior_view.room_boxes`); `interior_camera.focus` acha o cômodo do corpo (o mais perto na soleira) e `fit_arm` põe os quatro cantos do chão
  (+0,5 m) no campo de visão com o corpo no centro; olhar mais a prumo em cômodo pequeno (−56° a −40°); a parede com o ponto mais perto do corpo do lado da câmera cai (`_nearest_on`);
  o braço muda no máximo 5 u/s entre cômodos (sem arranco). Capturas: quarto (`depois-4-quarto`), sofá/sala (`depois-5-sala-sofa`), banheiro (`depois-2-banheiro`: o cômodo todo, com a porta e o corredor).
- Testes: `test_interior_camera` (cômodo certo, quatro cantos na tela 16:9 nos dois cômodos e em três giros, atravessar a porta sem salto), 810 do cliente verdes.

## Nota (3º teste → 4º)
- **UX 7,0/10 (6,5 → 7,0).** Subiu: a câmera de dentro (cômodo inteiro, corpo no centro), o "Ir agora (E)" aparecendo com a janela do turno (`b03-turno`), o aviso "Hora do trabalho em 1 h" que
  desce o 300× a 1×, a loja com preço, estoque, peso e "cabe nas mãos / precisa de carro ou entrega" (`c09-loja-catalogo`), "Chegou: Escritório" e "Contratado" na tela (`e01-escritorio`). Caiu/ficou: a câmera
  de rua em frente a um prédio vira a cabeça do corpo e o rótulo "Escritório" gigante (`e01`); o dia e a noite de 60×/300× passam sem pausa para o que o corpo parado deixou de fazer.
- **Jogabilidade 5,5/10 (5 → 5,5).** Subiu: o laço trabalho → salário (+72 em `career`), compra → entrega/mãos → depósito funcionam e dizem o que falta (`fetch:0 (chegue até a loja)`); a pessoa que ninguém
  conduz vai ao trabalho sozinha ("Marina saiu para o trabalho", `df.log`). Caiu/ficou: candidatar-se é uma trilha escondida (P1-1); a entrega às 18:48 diz "chega em 788 min"; com o corpo sem ordem a noite
  inteira, higiene 19, diversão 15, bexiga 4 e nada o empurra a agir.
- **Realismo das interações 6,0/10 (5,5 → 6,0).** Subiu: a nota começa em 64–66 e cai sem estímulo (Marina 49 às 18:35, 43 às 21:42), não passa de 100; banho +2, abraço +2, cozinhar +1; pedir desculpa sem briga não rende mais
  "Fez as pazes" [feito]. Ficou: 30 min de banho + abraço já dão "Radiante 83"; elogio/abraço aceitos sempre (+5 afeição) sem custo; "Beijar" e "Discutir" recusados por "ocupado com um móvel".

## P0 — impede jogar como no The Sims
1. **Nenhum novo.** O que parecia congelar o mundo (relógio parado após "ir até o prédio", `e4`) é a roda do E aberta pausando o tempo (#240): consistente com o The Sims, mas **não avisa** — ver P1-3.

## P1 — estraga a experiência
1. **Candidatura escondida** (`e01`): o jornal termina com o aviso "2 vaga(s): Auxiliar administrativo, Monitor escolar", mas "Candidatar-se" só existe na roda do prédio do emprego, e nada diz isso.
   Correção: o aviso e o card da carreira dizem "vá até o Escritório (clique) e use Candidatar-se"; o `job` entra na roda do jornal. (motor: texto em `CareerWork`; cliente: cartão)
2. **Entrega que chega "em 788 min"** (`e` log, 18:48): loja fechada à noite; o rótulo em minutos não diz que é amanhã 08:00. Correção: o motor devolve a hora de chegada ("amanhã 08:12") e a UI a mostra.
3. **Roda aberta pausa o mundo sem aviso** (`e4`, relógio 0,0×): quem clica num prédio e deixa a roda aberta vê o relógio parar. Correção: faixa "⏸ pausado pela roda" no topo (`speedinfo` já sabe).
4. **Corpo sem ordem some da cabeça do jogador**: o corpo conduzido não se cuida (bexiga 4 às 07:02 do dia 2) e a 300× nada interrompe. Correção: aviso `need-critical` com `stopKinds` (como o turno) que desce o tempo.
5. **Câmera de rua no portal** (`e01`): com o corpo colado à porta, a câmera fica sobre a cabeça. Correção: braço mínimo de rua maior perto de parede (cliente).
6. **Interações com a pessoa ocupada** (`a07`/`a08`): "Beijar" e "Discutir" recusam "ocupado com um móvel" e o menu não oferece "esperar/chamar". Correção: oferecer "Chamar" (pedir que levante) ou ir até quando o uso acabar.

## P2 — polimento
1. O corpo visto de cima é uma "bolinha" (chapéu e cabelo) em cômodo pequeno: a pose e o rosto não se leem (`depois-2-banheiro`). 2. "Cômodo feio −1" fixo no banheiro (`depois-2`): sem como consertar no banho (a loja ensina no B).
3. Cartão "Tranquilo · Parado" com a pessoa no portal do trabalho depois de "Contratado". 4. Escuro às 18:39 no modo B (`c08`). 5. Rótulos de tempo "· 30 min" do jornal e "3 min" do banho trocam por pessoa.

## Já está no "nível The Sims aceitável"? Não ainda
Passou do "protótipo": câmera, turno, compra e carreira fecham um dia. Falta o **fluxo guiado** (candidatura, entrega com hora certa, roda que pausa), a **pressão do dia** (necessidades que interrompem o tempo)
e a **vida social com custo** provada em tela (briga e pazes).

## Próximas 5 prioridades
1. Fluxo do emprego guiado (P1-1): vagas na roda do jornal, "Candidatar-se" com o caminho, e o card "Ir ao trabalho" com tempo de viagem. (motor + cliente)
2. Pressão e interrupção do dia: `need-critical` em `stopKinds`, prioridade do dia ("fome 20%") e relógio que pára em evento (P1-4). (motor, dado)
3. Compra com hora e rota: entrega com hora de chegada, "Ir à loja" pela roda, levar o móvel nas mãos até o depósito com o caminho desenhado (P1-2). (motor + cliente)
4. Social com custo e pedido de atenção: "Chamar"/esperar, briga que dura e pazes com custo (P1-6), humor cuja nota não chega a "Radiante" em 30 min. (dado)
5. Luz e câmera de rua/noite: braço de rua perto de paredes, luz de cômodo à noite, corpo legível de cima (P1-5, P2-1, P2-4). (cliente)

## Corrigido neste passo
- **Câmera de interior por cômodo** (AD-235, acima). **"Fez as pazes"** só de quem brigou: `MomentTrigger.Needs` (dado `moods.json`; `MoodTests.Making_up_only_happens_to_people_who_fought`).
- Passos de captura: `rooms` (cômodos e enquadramento) e `closemenu` (`dev_steps.gd`); `useverb` agora imprime os usos do objeto.

## Corrigido no 5º passo (#268, AD-236)
- **P1-1 candidatura**: ler o jornal abre as vagas na roda (cargo, empregador, salário, ida, chance) com "Candidatar-se"; o motor leva o corpo ao empregador (`ir até`) e responde na chegada: contratado, recusado com a chance, ou entrevista marcada para a abertura (`Careers.rules.applyOpenHour`).
- **P1-2 entrega**: "Seu Tomás entrega Sofá por 3: chega amanhã às 08:00" (hora do mundo, `CareerWork.When`), no preview, na ficha (`dueText`) e no preparo da loja; "Ir à loja" na roda do E de qualquer lugar.
- **P1-3 pausa**: faixa "⏸ Tempo pausado (menu aberto)" com a roda ou outro popup aberto.
- **P1-4 necessidade crítica**: `NeedCritical` (dado, limiar por necessidade) vira aviso `need-critical` em `stopKinds` (300× desce a 1×) e o livre-arbítrio (U liga/desliga) atende sozinho quando o jogador não deu ordem: 03:05 do dia 2, "Você está com a bexiga cheia (14%)", "Marina cuidou de si: Usar o vaso", bexiga 20 → 89.
- **P1-5 câmera de rua**: colada à fachada, a mola sobe até `STREET_VIEW_M` do corpo em vez de grudar na cabeça. **P1-6 ocupado**: "Chamar Rafael" (o motor sorteia: afeição, humor, sono, briga) e "Esperar terminar (hoje às 07:20)".
- **P2**: realce suave no chão do corpo conduzido de cima; banheiro medido contra a beleza esperada (`Home.rules.expectedBeauty`), sem "Cômodo feio" fixo; cartão diz "Indo: Escritório", não "Parado".
- **Realismo**: elogio repetido recusado ("lembra que você já elogiou agora há pouco"), abraço logo após outro gesto recusado ("carinho demais em tão pouco tempo"), briga trava os gestos por dias, "Radiante" só a partir de 80 (banho + abraço + elogio = 75, "Bem").
