# 5º teste de jogo de UX do sandbox do casal (#246, 2026-11-28)

Jogado no build de `d318d5c1` + correções deste passo (API própria na porta 5995, cliente Godot 1600x900, **Sandbox: casal**), **dois dias no mesmo mundo**, em várias rodadas do cliente contra a MESMA API:
passo novo `resume` (`menu;resume;…`: Continuar o mundo atual sem recriar, documentado no README do cliente). Capturas em
`/private/tmp/claude-501/-Users-aoliveira-Desktop-Projects-LivingWorld/7392251a-1ec6-4bf2-8382-62ad34632fdc/scratchpad/play/playtest-ux-5/` (só o nome; as de antes da correção do P0 estão em `old/` e `old2/`).
**[feito]** = corrigido aqui, com teste. **Não provado em tela**: o sofá posto na sala (ver P1-3).

## O que foi jogado (dia 1 → dia 2)
- **Manhã** (`a01-manha`, `a03-cozinhou`): vaso, banho, fogão, mesa; jornal (`a04-jornal`); a roda do prédio lista as vagas com salário, ida e chance (`a05-roda-emprego`, faixa "Tempo pausado (menu aberto)").
- **Emprego**: Candidatar-se leva o corpo ao Escritório; fora do atendimento vira entrevista; às 08:00 **o motor decidiu sozinho** e veio "Contratado" (`a07-entrevista`, `a08-contratada`) [feito, P1-1].
- **Turno**: "Ir agora (E)" aparece (`b01-pre-turno`), "Os dois estão fora" (`b02-os-dois-fora`), pulo até a volta: **+71 de salário**, "Voltou do trabalho. Desempenho do dia: 78%" (`b03-volta`); dia 2: **+72**, contas abertas (Água 17, IPTU 90, Luz 6, vencem dia 4).
- **Compra**: "Comprou Sofá: na loja, em seu nome" (30), entrega "Seu Tomás entrega Sofá por 3: chega amanhã às 08:00"; no dia 2 o móvel chegou ao depósito 45 s depois das 08:00 (`i01-entrega-chegou`, `waitparcel depot`).
- **Social** (`h03-briga`, `h04-abraco-recusado`, `h05-desculpa`): Discutir **-14 de afeto** ("Marina e Rafael discutiram"), Marina "Irritado", momento "Brigou com Rafael −8 · 3 d"; abraço, elogio e desculpa **recusados** ("ainda está magoado com a briga"); a 3ª tentativa de desculpa foi aceita: **"Fez as pazes com Rafael +1 · 24 h"**.
  Elogio repetido recusado ("lembra que você já elogiou agora há pouco"). **Briga e pazes provadas em tela**.
- **Noite**: 19:20 → 05:40 a 300× sem ordem (`g01`–`g03`): nada interrompeu (nenhuma necessidade ficou crítica; sono só 73 → 58).

## P0 — impede jogar como no The Sims
1. **Fechar o jogo desfazia a vida do casal** (achado ao Continuar, `old/f01-menu-social`): o cliente sai do personagem pelo `Logout` do motor, que é o sumiço (`Disappearance`): Marina largava **casa e emprego**
   e ganhava um domicílio só dela (ids 12, 13, 14… a cada saída), Rafael a "procurava", e o G ("passar a ser Rafael") sumia. Correção [feito]: dado `Players.LogoutKeepsLife` (o sandbox liga): sair solta o corpo como a troca (G), casa e emprego ficam;
   o cliente aceita a resposta sem carimbo de sumiço ("Você saiu. Marina volta à rotina, em casa e no trabalho"). `PlayerSwitchTests.Logout_keeps_home_and_job…`, `test_logout_in_a_scenario…`; Continuar depois disso manteve `household 0`, cônjuge e emprego.

## P1 — estraga a experiência
1. **"Entrevista marcada hoje às 08:00" nunca acontecia** (`old/c04`): era só um aviso; a pessoa voltava às 08:25 e nada. [feito] `JobSearched.InterviewAtMs` + `CareerSystem` rodam a entrevista na abertura (`RunInterview`): no empregador decide (contratado/recusado com a chance), longe "perde a vez" e o aviso diz. 2 testes em `Playtest4Tests`.
2. **Desculpa barata**: pedir desculpa sem briga dava +4 de afeto a cada 45 min (afeto 77 → 85 em duas desculpas, `old/r3`). [feito] `social.json` (dado): base −15 e +60 se o alvo tem `quarrel`; sem briga é recusada; teste `An_apology_with_nothing…`.
3. **Móvel no depósito não vai para a sala** (`i03-fantasma`): `buyfind:#0,sala` achou `place:0:0:6:5:270 verde=false` e o clique não pôs; sem lugar válido o motor não diz "onde cabe" nem tenta o próximo. Não corrigido (motor + cliente do modo B).
4. **Dia 2 às 08:08 aparece "Noite" e a cena fica escura** (`i05-pre-turno`; 18:01 do dia 2 também "Noite", o do dia 1 era "Entardecer"): o nascer/pôr do sol do motor andou ~1 h de um dia para o outro (ou o cliente leu o clima velho). Não corrigido, a investigar.
5. **O parceiro passa a noite fora e parado** (`theother`): Rafael (ninguém conduz) ficou 07:00–08:10 parado em (12,12), fora da casa, sem dormir (sono 64, "Diversão 99" da TV), e Marina não o alcança andando em linha reta (só o clique "ir até" resolve).
6. **Aviso velho** "Pulando até a volta do turno…" ficava às 18:02, já em casa (`old/e02-casa`). [feito] some quando o salto acaba (`test_the_skip_to_return_note…`).

## P2 — polimento
1. **Pressão do dia fraca**: em 22 h sem ordem a Fome foi a 79, o Sono a 58 e o livre-arbítrio nunca agiu; só a Diversão (13) e a Bexiga (28) chegaram perto; depois de 9 h no trabalho Diversão 0! e Bexiga 0! (`j01`) sem consequência.
2. **Câmera de rua na porta** (`a08-contratada`): colada ao portal, a cabeça cobre a tela e o rótulo "Escritório" gigante e borrado; a roda sobrepõe o rótulo do prédio (`c01` antigo). **Casa por fora**: o "Porta: Entrar" só existe na célula exata da soleira (a 1,5 m não aparece), e clicar na porta da própria casa não leva para dentro.
3. "Tomar banho · 2 min" mostra "ocupado" por mais de 30 min de mundo no `needs` (rótulo velho); dica "Você está entediado → Fogão"; "Ida 40 min" no jornal contra ~5 min reais de caminhada; aviso "interview … 08:00" fica no livro depois de contratada.
4. "Cômodo feio −1" aparece dentro do Mercado (`old/c05`): a beleza esperada do salão da banca é da casa.

## Notas (4º teste → 5º)
- **UX 7,0 → 7,0.** Subiu: vagas com salário e chance na roda, "Tempo pausado (menu aberto)" visível, "Ir agora" e "Os dois estão fora", entrega "amanhã às 08:00", briga e pazes legíveis no cartão ("Brigou com Rafael ×2 −8 · 3 d"). Segurou: a câmera de rua na porta, o escuro do dia 2, a roda sobre o rótulo.
- **Jogabilidade 5,5 → 6,0.** O laço rotina → jornal → candidatura → entrevista → turno → salário → compra → entrega fecha dois dias sem trava de tela; falta a pressão (necessidades) e posicionar o móvel comprado.
- **Realismo 6,0 → 6,5.** Briga que custa afeto e trava gestos por dias, desculpa só com motivo, elogio repetido recusado, pazes que rendem; falta a vida do parceiro (parado fora a noite toda) e o ritmo das necessidades.

## Já é "nível The Sims aceitável"? **Ainda não**
Aceitável pede os três juntos; hoje: **UX nível Sims: não** (câmera de rua e luz do dia 2 quebram a ilusão); **interações realistas: quase** (o social tem custo e memória, mas o parceiro não vive a noite);
**jogabilidade aceitável: quase** (o dia fecha, mas nada empurra o jogador a dormir, comer ou ir ao banheiro, e o móvel comprado não chega à sala). O que mais pesou: um dia inteiro sem ordem não gerou uma única decisão para o jogador, e o 1º achado (sair do jogo desfazia o casal) mostra que continuar um mundo
ainda não estava no caminho testado; agora está (`resume`).

## Próximas 5 prioridades
1. **Pressão do dia e parceiro vivo** (P1-5, P2-1): ritmo de sono, fome e bexiga em dado, `need-critical` por sono/higiene, Rafael que dorme, come e vai ao trabalho sozinho. (dado + motor)
2. **Dia e noite certos** (P1-4): sol por data do motor sem salto de 1 h, luz de cômodo à noite e o clima relido ao Continuar. (motor + cliente)
3. **Comprar até o fim** (P1-3): "onde cabe" no motor, posicionar do depósito com o caminho e o clique; contas (IPTU 90 contra 88 de saldo) com o "Pagar" na roda. (motor + cliente)
4. **Câmera de rua e porta** (P2-2): braço mínimo na soleira, rótulo de prédio menor, "clicar na porta da casa entra", roda que não cobre o prédio. (cliente)
5. **Honestidade de rótulos** (P2-3/4): "Ida" do jornal pelo caminho real, avisos velhos saem do livro, banho "ocupado" certo, dica de diversão coerente, beleza esperada de prédio comercial. (dado + cliente)

## Corrigido neste passo
P0 (sair do jogo), P1-1 (entrevista), P1-2 (desculpa), P1-6 (aviso do pulo), passo `resume` (e `hmdebug`) do `--steps`, AD-237. Testes: `PlayerSwitchTests`, `Playtest4Tests` (3 novos), `test_player` (2 novos), 823 do cliente verdes.
