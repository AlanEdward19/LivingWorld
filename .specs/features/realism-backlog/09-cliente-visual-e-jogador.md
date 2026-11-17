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

## 1b. Lacunas que sobraram da etapa 3 do ADR-0047 (AD-171, cliente)

Contrato (formas gravadas pelo AD-170, `tests/contracts/`), urgente:
- [ ] **Formas estritas demais para mundo rodando**: `processes[].resourceId` nulo, `buildingStates[].workplaceId`/`locationTypeId` nulos (moradia) e, no `visual-delta`, `npcUpserts[].observable`/`actionTarget`/`intent`/`intentTarget` obrigatórios embora "omitido = não mudou". Com validação ligada o cliente descarta o snapshot (mundo pausado em tick 0 passa). Medido em 2026-11-17: 103 divergências no Mundo, 34 na Cidade após 150 ticks. Marcar opcionais (`campo?`). O cliente agora mostra a faixa "fora do contrato" em vez de "Conectando…".

Motor/projeção (o cliente já tem o lugar para usar):
- [ ] Alvo da ação só para trabalho, sono e conversa com o cônjuge: conversa com outros, compra, furto e ajuda não viram para o alvo nem ganham a linha.
- [ ] Animal sem idade, porte nem atividade (pastar, fugir, caçar): o desenho é parado, só espécie, energia e doença.
- [ ] Lavoura ocupa uma célula (só hectares): o campo não tem área em células.
- [ ] `intentTarget` sem formato documentado: o cliente não usa.
- [ ] Sem `GET /entities/event/{id}`: a ficha do evento vive só na memória do cliente (some ao reiniciar); `recentEvents` só desde o último esvaziamento do buffer da API.
- [ ] Nome do prédio na inspeção é genérico ("prédio 0"); `typeName`/`locationTypeName` dizem mais.
- [ ] Ficha da cidade: `links` lista todos os prédios (o cliente mostra os 12 primeiros).
- [ ] Interior: `ObjectDescriptor` sem nome e material, mobília sem andar (herdado da etapa 2).

Cliente (feito em parte):
- [ ] Prédio muda enfeite e placa pela função, não o corpo (paredes e telhado iguais).
- [ ] Bench mede mundo pausado (o do `bench.sh` nasce pausado): medir com o mundo rodando quando as formas acima forem corrigidas.
- [ ] Sem chuva nem queda de folhas; animais e bandos não andam entre deltas.
- [ ] Prédio translúcido: o mouse sobre a parte VISÍVEL de um prédio de trás abre o da frente; parte totalmente escondida
  só é alcançável depois que algo em foco abre o da frente (não há "ciclar" entre prédios sob o cursor).
- [ ] Ruas: sem a camada `Roads` do motor (hoje `isModeled: false`), rua = o que não é quintal (1 célula em volta de cada
  prédio) dentro dos limites; quando o motor ganhar ruas e lotes, o cliente já lê a camada.

## 2. Fatia do jogador

Base: `docs/roadmap/phase-25-players.md` (hoje parcial: encarnar, agir, logout e procura) e as fases 11 (conversa com NPC via LLM, sem a LLM alterar o mundo) e 26 (console).

A fazer:
1. **Entrar no mundo** de duas formas: criar o próprio personagem, ou tomar a vida de um NPC que já existe (com família, ofício, dívidas, memórias e vínculos dele).
2. **Todas as visões e mecânicas descritas no roadmap** para o jogador: levantar a lista completa na fase 25 e nas que ela cita, e marcar o que existe, o que é parcial e o que falta.
3. **O mundo trata o jogador como pessoa**: o motor valida e aplica toda ação; nada de atalho que escreva no estado. Offline vira desaparecimento, como a fase 25 descreve.
4. **Quem teve a vida tomada**: o que acontece com a mente do NPC, o que os outros percebem, e a volta dele quando o jogador sai. Regra a definir no desenho.
5. Determinismo: entrada do jogador é evento registrado e reproduzível; mesma seed + mesmas entradas = mesmo mundo.

Se o escopo da fase 25 mudar, perguntar ao dono antes de mexer em `ROADMAP.md`.

## Pedidos do dono em 2026-11-18 (vendo o cliente depois da etapa 1)

"Tá tudo muito grudado, os prédios muito grudados e colados, um na frente do outro. Não tem controle de como os prédios e
casas nascem. [...] ao passar o mouse em um prédio coberto por outro, o que tá na frente fique meio translúcido, mas também
precisamos ajustar como as coisas crescem perto um do outro no motor e no Godot. Não dá para analisar nada com tudo grudado,
e os NPCs ficam se passando um pelo outro."

1. **Cliente (AD-171)**: prédio que cobre o que está sob o mouse ou o selecionado fica translúcido; NPCs na mesma célula ou
   se cruzando não se atravessam na tela (espalhar dentro da célula, desviar ao cruzar).
2. **Motor (AD-173)**: a cidade cresce com espaço: ruas, lotes, quintal e recuo entre prédios, pelo que a própria vila
   decide e pelo terreno; nada de prédio colado em prédio sem passagem. Muda o mundo (goldens regravados na junção).
3. **Delta sem nulos** (aprovado: "pode omitir os nulos no delta"): servidor omite chaves nulas no delta, cliente lê
   ausência como nulo; muda os `shape.*.json` do delta.

Correção do dono no mesmo dia: "quintal depende, nem toda casa vai ter quintal e isso afeta a construção da casa (focado em
realismo lembra)". Quintal e recuo são decisão de cada casa ao construir (terra, meios, ofício, família), com custo na obra e
uso real; o motor expõe o lote e as ruas e o cliente desenha só isso. O quintal de uma célula inventado pelo cliente (AD-171) sai.
E ainda: "nem toda rua é de asfalto também". A rua emerge do uso (trilha → terra → calçada só com tecnologia, material e
trabalho), a superfície tem efeito no passo e na carga, e o contrato diz o tipo de cada trecho para o cliente desenhar.

## Pedido do dono em 2026-11-18: jogar com um comando e menu de videogame (AD-174)
"Eu preciso de uma maneira fácil de executar a simulação gamificada (que rode o projeto e o Godot) e o Godot precisa ter o
menu interativo estilo videogame, onde crio meu mundo etc. Fluxo completo, descomplicado, que um jogador de RPG, Dwarf
Fortress, [...] até uma criança consiga entender e amar."
## 3. Menu de videogame e lançador (AD-174): lacunas da API

O fluxo do menu pede e a API não dá (o cliente contorna como dito; nada mudou em `src/`):
- [ ] `/periods` sem época, descrição nem imagem: os 3 presets são só tamanhos; o passo "Época" usa frases do cliente e os períodos de `scenarios/periods/` (medieval, pré-histórico…) não são oferecidos.
- [ ] `GET /worlds` sem `periodId`, ano (calendário de cada mundo) e marca de "aberto agora": o cliente toma o mais recente como o aberto e calcula o ano pelo calendário do mundo aberto.
- [ ] Sem rota para renomear mundo salvo (Meus mundos não oferece).
- [ ] Sem "salvar agora": a pausa só diz que o mundo se salva sozinho (a cada 24 ticks).
- [ ] O mundo que a API cria sozinha no primeiro uso não se distingue de um mundo do jogador: Continuar some se ele não tem período e o jogador nunca jogou nesta máquina.
- [ ] Criação síncrona sem progresso: o carregamento mostra as etapas do cliente, não as do servidor.
- [ ] Criar e carregar deixam o mundo pausado: o cliente retoma com o token da sessão.
- [ ] Catálogo `assets.json` sem sons de interface: o menu não tem som.
