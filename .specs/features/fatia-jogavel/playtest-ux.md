# Teste de jogo de UX do sandbox do casal (#246, 2026-11-28)

Jogado ao vivo, como jogador novo, no build de `24d98647` (API própria na porta 5461, cliente Godot 1600x900, botão **Sandbox: casal**).
Capturas em `/private/tmp/claude-501/-Users-aoliveira-Desktop-Projects-LivingWorld/7392251a-1ec6-4bf2-8382-62ad34632fdc/scratchpad/play/playtest-ux/`
(abaixo só o número do arquivo, ex. `02` = `02-entrou.png`). Itens marcados **(#255)** são do agente que mexe em cenário, catálogo, necessidades,
barras, roda do E, fila e "andar até o móvel": não foram tocados aqui. Corrigido neste passo: ver o fim (**F1–F5**).

## P0 — impede jogar como no The Sims

1. **Começa à meia-noite, no inverno, no escuro** (`02`, `03`). Cena quase preta, móveis viram blocos azuis, rosto e cômodos ilegíveis. Sims: começa de
   manhã, casa iluminada por dentro mesmo à noite (luz de cômodo). Correção: cenário começa às 08:00 **(#255, dados)**; luz interna de cômodo ligada
   quando escurece (cliente, próxima etapa).
2. **Tempo 1 s por s e nenhum controle de velocidade** (`hudclock`: 00:00 → 00:02 em 2 min de jogo). Barras nunca mexem, dormir/comer não mostra efeito
   em tempo útil (`14`, `16`). Sims: 1x/2x/3x e "pular a noite". Correção: no HUD do jogador (single player ou admin) botões 1x/3x/pausa, com o motor
   já aceitando o Δ do admin; a regra de "só admin/single player" (ADR-0048) segue. **(motor + cliente)**
3. **Usar o móvel não leva o corpo até ele** (`16`: Marina dorme no chão, ao lado da cama, e o balão "Z" flutua). Sem animação de sentar/deitar, sem
   duração nem cancelar visíveis. Sims: o Sim anda até o objeto, encaixa e a ação tem barra e fila. **(#255)**
4. **A oferta principal do E é "Vender · Sua casa"** (`02`, `12`; na roda é a nº 1 em `33`). O primeiro prompt que o novato lê é vender a própria casa.
   Sims: vender fica no modo de compra, nunca no clique do dia a dia. Correção: ordenar por proximidade/relevância, tirar "Vender" da roda
   (menu da casa com confirmação). **(#255, roda)**
5. **Os dois moradores nascem empilhados no mesmo ponto da porta** (`02`, `31`: Marina e Rafael um dentro do outro). Sims: cada um no seu cômodo.
   Correção: posição inicial distinta por cômodo (sala/quarto) ao semear a casa. **(motor, #255)**
6. **Sem cutaway: a câmera alta mostra um poço escuro e a 1ª pessoa fica de frente para a parede** (`12`, `32`). Paredes opacas entre a câmera e o
   corpo; o anel dourado no chão/parede (`02`, `12`) não diz o que é. Sims: corte de parede/telhado por andar, alvo com ícone. **(cliente, #250 a evoluir)**

## P1 — estraga a experiência

7. **Preso na casa e no lote** (`24`–`28`): 14 s de W andaram ~3 m, com "Chão firme −80%" no cartão sem explicação; `walkto` para fora parou na cerca
   (x=18). O mundo é uma placa de 24x24 sem rua nem calçada. Sims: bairro com rua, calçada e vizinhos à vista. **(motor: rua e lotes vizinhos, #257)**
8. **Conversa com o parceiro é a de um desconhecido** (`36`, `37`): "Bom dia! Como vai?" à meia-noite, "Tem alguma novidade por aqui?", "Posso ajudar
   em algo?". Sims: menu social com cônjuge (abraçar, beijar, conversar, brincar) e relação visível. **(S3, motor + cliente)**
9. **Ofertas duplicadas e nomes que não são do Sims**: "Cama: Dormir" e "Cama de casal: Dormir" (as duas existem), "Despensa" em vez de geladeira,
   "Sofá: Conversar" (sem sentar/ver TV), "Mesa: Comer" e "Fogão: Comer". Banho, vaso, TV e pia não têm uso. **(#255)**
10. **Barras sem leitura**: Saúde/Fome/Sede/Sono tudo cheio, sem número, ícone nem tendência; "Parado" no lugar de humor/aspiração (`02`). Sims: barras
    com ícone, cor que muda e humor geral. **(#255, `player_frame.gd`)**
11. **Guia diz "Sua casa fica aqui. Na porta, aperte E para entrar"** com o jogador já dentro (`05`). Correção: passo 1 do guia fala de andar e do E
    nos móveis quando o corpo já está em casa. **(cliente, próxima etapa)**
12. **Habilidades "Morador nível 7/10"** em Metas (`38`): jargão de sistema, sem sentido para quem joga. Sims: aspiração e habilidades com nome (Cozinha,
    Carisma...). **(motor catálogo + cliente)**
13. **Sem feedback de que algo acabou**: nada de toast "Marina terminou de comer", nem ganho de humor. Só o rótulo "Comendo"/"Dormindo" no cartão. **(#255)**
14. **Troca de corpo (G) funciona, mas a dica é minúscula** ("G — passar a ser Rafael", 12 px, `31`). Sims: retratos dos moradores sempre à vista, clique
    para trocar. **(cliente)**

## P2 — polimento

15. Título do parceiro ("Marina / Parado", `31`, `33`) grande e por cima da pessoa e da roda: usar tamanho fixo menor e sumir com a roda aberta.
16. Painel da Pausa (8 botões, 840 px) encosta no relógio do topo a 900 px de altura; "Sair do jogo" e "Voltar ao menu" juntos com o mesmo peso. Agrupar.
17. "Como jogar" (`06`) é uma tabela de 11 linhas de teclas; o Sims ensina por dica na hora (tooltip no móvel). Trocar pela dica contextual.
18. Título do menu mostra "Continuar: Mundo sem nome, ano 0 · 20 pessoas" num banco recém-criado (`01`): um mundo padrão aparece como se fosse do jogador.
19. Ruído de aviso "Mundo pausado: suas ações só andam quando o tempo voltar" aparece na pausa do próprio menu (corrigido, F5) e o relógio do topo
    ("⏸ pausado") continua visível sob o painel.
20. Primeira pessoa (`C`) com nariz na parede ao nascer na porta (`32`): ao alternar, afastar o corpo da parede ou manter 3ª pessoa até ter espaço.

## Corrigido neste passo (só cliente, com teste em `tests/test_playtest_ux.gd`)

- **F1** A placa 3D da cidade ("Jardim das Acácias · 1 na cidade · 0 na rua · parado 1") some no modo jogador e o nome do bairro vira um aviso discreto
  ao entrar na área e ao voltar à cidade (`city_renderer.set_labels_visible`, `app.announce_area`). Captura depois: `43`.
- **F2** "Andar 1 de 1 (PgUp/PgDn)" só aparece com mais de um andar; antes sentava em cima das teclas (`02` antes, `40`/`43` depois).
- **F3** A faixa de 13 atalhos esmaece depois de 20 s de jogo (F1 e o guia continuam), `app.fade_key_hints`; captura `41`.
- **F4** Avisos do modo jogador sobem acima da faixa de teclas (antes cobriam a primeira linha). Captura `43`.
- **F5** A pausa esconde avisos do topo e a faixa "Mundo pausado" que ficavam por baixo do título (`44` antes da correção do painel, `45` depois).

## Ordem sugerida para as próximas etapas
1. #255: cenário 08:00, posições distintas, ofertas por proximidade sem "Vender", andar até o móvel, fila, barras com ícone e feedback de fim.
2. Cliente: controle de velocidade do jogador (item 2), luz de cômodo e cutaway (1, 6), guia certo (11), retratos de troca (14).
3. S3: menu social do casal (8) e habilidades/aspirações com nome (12). Rua e vizinhos: #257.
