# Cliente visual refeito e fatia do jogador (não começado; ÚLTIMOS da fila)

> Pedido do dono em 2026-11-12. **Só começar depois de tudo o que está pendente nos arquivos 01 a 08.**
> Ordem entre os dois: primeiro o cliente visual, depois o jogador.

## O que o dono pediu (palavras dele)

- "Preciso que registre uma refatoração completa do motor visual, que hoje é muito abstrato; não consigo interagir direito com nada, muito menos usá-lo para assistir os mundos."
- "Após isso, preciso que implemente a fatia do jogador: poder entrar no mundo, criar seu personagem ou roubar a vida de um NPC já existente, com todas as visões e mecânicas descritas no roadmap."
- "Esses ajustes devem vir após implementar tudo da lista que temos de pendências (coloque no final da lista esses itens)."

## 1. Refatoração completa do cliente visual (Godot)

Problema dito pelo dono: abstrato demais, não dá para interagir com as coisas, não serve para assistir um mundo.

A fazer:
1. **Levantamento primeiro**: o que o cliente Godot mostra hoje, o que é só símbolo, onde a interação falha. Ler `client/godot/README.md`, ADR-0022 e ADR-0025, e as fases 15 (mapa visual), 27 (cinemáticas) e 28 (painel "ver o cérebro").
2. **Assistir o mundo**: ver pessoas, casas, lavoura, estações, animais e o que cada um está fazendo, sem precisar ler tabela; seguir uma pessoa, uma família ou uma cidade; controlar tempo (pausa e velocidade, fase 26).
3. **Interagir com tudo**: clicar em pessoa, prédio, lavoura, animal, objeto e ver estado e história; abrir o cérebro (memórias, emoções e motivos do AD-138); navegar por parentesco, emprego e vínculos.
4. **Aparência derivada do estado**: corpo que cresce e envelhece (AD-134), estação e safra (AD-135), doença, luto; coisa inventada com aparência e animação vindas de material e forma (arquivo 05).
5. **Desempenho medido**: FPS antes e depois com `--steps "stress:500"`, registrado em `client/godot/README.md`. Sem regressão.
6. É decisão de arquitetura e muda contrato com o servidor: escrever desenho e ADR e **levar ao dono antes de implementar**.

Perguntas abertas para o dono (fazer quando a vez chegar): 2D ou 3D; nível de detalhe esperado ao aproximar; o que do crime e da violência (arquivo 06) o cliente mostra.

## 2. Fatia do jogador

Base: `docs/roadmap/phase-25-players.md` (hoje parcial: encarnar, agir, logout e procura) e as fases 11 (conversa com NPC via LLM, sem a LLM alterar o mundo) e 26 (console).

A fazer:
1. **Entrar no mundo** de duas formas: criar o próprio personagem, ou tomar a vida de um NPC que já existe (com família, ofício, dívidas, memórias e vínculos dele).
2. **Todas as visões e mecânicas descritas no roadmap** para o jogador: levantar a lista completa na fase 25 e nas que ela cita, e marcar o que existe, o que é parcial e o que falta.
3. **O mundo trata o jogador como pessoa**: o motor valida e aplica toda ação; nada de atalho que escreva no estado. Offline vira desaparecimento, como a fase 25 descreve.
4. **Quem teve a vida tomada**: o que acontece com a mente do NPC, o que os outros percebem, e a volta dele quando o jogador sai. Regra a definir no desenho.
5. Determinismo: entrada do jogador é evento registrado e reproduzível; mesma seed + mesmas entradas = mesmo mundo.

Se o escopo da fase 25 mudar, perguntar ao dono antes de mexer em `ROADMAP.md`.
