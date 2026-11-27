# Fatia jogável — simulador de vida (plano para aprovação)

Épico: #246.

**Pedido do dono (2026-11-27):** "não tá jogável", "quero um simulador completo de vida", vibe The Sims.
Prioridades: casa e objetos, rotina e necessidades, visual e movimento; pessoas e conversa por
último (NPC↔NPC segue pelo sistema; jogador↔NPC em linguagem natural pela IA da conta do jogador).
Diagnóstico com arquivo e linha: [diagnostico.md](diagnostico.md).

## Como deve ser jogar (os primeiros 10 minutos)
1. Entro no meu personagem em casa. A casa tem quarto, cozinha e sala de tamanho real; ando
   sem travar, a câmera não pula, a rua continua visível ao sair. Nada recarrega.
2. Vejo as barras (fome, sede, sono, higiene, diversão, social) e o que está baixo.
3. Clico na tina e tomo banho; clico no fogão e cozinho; enfileiro "comer na mesa" depois.
   Cada ação tem duração visível, animação e posso cancelar.
4. Saio para a rua sem tela de carregamento, compro um banco no carpinteiro e o ponho na sala.
5. Clico num vizinho: menu claro (conversar, elogiar, contar piada, pedir ajuda, discutir)
   e vejo a relação subir ou descer.

## Marcos (cada um termina jogável e medido)
**M0 — consertos imediatos (1 a 2 dias).** O visual do corpo não muda ao entrar em casa (o
interior usa a mesma fábrica de figura do mundo). Casa nova nunca menor que 2×2 de piso, e a
escada nunca come o único piso. Aceite: entrar e sair de 5 casas sem troca de visual.

**M1 — andar sem voltar.** O motor move o corpo a cada tick pela direção mantida (não só quando
chega comando); comandos com número de sequência e o cliente reaplica o que o motor ainda não viu;
o cliente prevê com as mesmas regras de colisão do motor (porta, quina, multidão).
Aceite: 3 min andando pela cidade e campo sem puxão > 0,3 m; FPS e custo por NPC-tick iguais.

**M2 — casa de verdade (#226).** Parede vira aresta entre células, não célula inteira; cômodos
por função (quarto, cozinha, sala, oficina) com portas internas; casa inicial ~6×5 células
(9 × 7,5 m). Sem compatibilidade com mundos salvos (decisão do dono).
Aceite: andar por todos os cômodos sem bloqueio invisível; motor e cliente concordam na parede.

**M3 — um mundo só, sem troca de tela.** A cidade fica carregada; ao entrar, o telhado some e o
interior aparece na mesma cena; mundo aberto → cidade sem recarregar tudo (só o que muda).
Aceite: atravessar mundo → cidade → casa → cidade sem pausa > 100 ms nem câmera pulando.

**M4 — necessidades e objetos.** Higiene e diversão como necessidades do motor (para NPC e
jogador, com efeito real: saúde, humor, como os outros reagem). Objetos do período: fogão a
lenha (cozinhar), tina (banho), latrina, lareira, banco/instrumento (diversão). Toda ação usa a
tarefa com duração (hoje só o corte de lenha), com animação e cancelamento. Fila de ações do
jogador (até 4). Os NPCs usam os mesmos objetos (coesão).
Aceite: um dia de jogo cuidando das 6 barras só com objetos da casa; NPCs medidos usando-os.

**M5 — comprar e arrumar móveis.** Móvel é item feito por quem sabe o ofício e vendido (moeda
conservada); modo arrumar simples em grade dentro da própria casa, com caminho até a porta.
Aceite: comprar, pôr, mover e vender um móvel; o NPC passa a usá-lo.

**M6 — pessoas.** Menu social claro ligado às intenções que o motor já valida; barra de relação
visível; reação do NPC pelo estado dele. NPC↔NPC continua pelo sistema.

**M7 — conversa em linguagem natural pela conta do jogador (por último).** Provedor por jogador;
o texto livre vira intenção do sistema e a resposta do motor vira fala natural. A ligar: conta
Claude e Codex do jogador sem chave de API. **A pesquisar antes:** se há login de conta oficial
para app de terceiro; se não houver, alternativa é usar o Claude Code/Codex instalado na máquina
do jogador como provedor local. Precisa de ADR (dependência e fronteira da LLM).

## Desempenho e realismo
Cada marco mede FPS (`stress:500`) e custo por NPC-tick antes e depois. Nada de visual inventado:
o cliente desenha o que o motor diz. Aleatoriedade só por RNG semeado.

## Decisões do dono (2026-11-27)
1. Ordem M0 → M7 aprovada.
2. Sem compatibilidade nem migração de mundos antigos.
3. Nada é escrito no código: cama, fogão, tina, cômodos e necessidades são conceitos de catálogo
   configurável (cenário/dados), criados na hora; o código só conhece usos e efeitos genéricos.
   Hoje `DefaultObjects.cs` é catálogo em código — o M4 o move para dados.
4. Referência: The Sims em 3D (casa, objetos, rotina), mas o jogo é **RPG com movimentação
   fluida** em 3ª/1ª pessoa (não clique-para-andar).
5. Velocidade: durante a fatia roda só lint, unit, cliente e testes filtrados; longrunning,
   demografia e baselines rodam depois, e o que quebrar se ajusta então.
