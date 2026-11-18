# Cidade: rua de verdade, lote na obra e bairro denso (pedidos do dono, 2026-11-18)

> Continuação da AD-173 (ruas, lotes, quintal). Só começa depois que a AD-173 juntar. Índice: [09](09-cliente-visual-e-jogador.md).

## Palavras do dono
- "Preciso que tenha sistema real de rua. Nada deve ficar apenas no visual (exceto os NPCs não se esbarrarem, e contornarem,
  isso ok ficar no visual apenas)."
- "Quintal depende, nem toda casa vai ter quintal e isso afeta a construção da casa (focado em realismo lembra)."
- "Nem toda rua é de asfalto também."
- "Até podemos ter prédios colados (estilo favela), mas isso deveria afetar a aparência da cidade e como ela é vista. Em alguns
  casos até vira ponto turístico (igual as pessoas vão visitar o Rio de Janeiro para ver as favelas, mas as pessoas que são do
  Brasil acham feio ou têm medo, além de ter higiene pior), porém tem sempre que ter como chegar na casa, seja via telhado,
  via escada, etc."

## A fazer
1. **Rua nasce do trajeto real**: o NPC anda por caminho de verdade (não em linha reta); o pisoteio abre trilha; trecho sem uso
   se desgasta e some.
2. **Superfície com consequência**: trilha, terra, cascalho e pedra mudam passo, carga e veículo; chuva faz lama onde não é
   calçado; o NPC escolhe o caminho melhor quando compensa.
3. **Calçar é obra**: técnica (invenção/tecnologia), material, mão de obra, quem decide e paga, manutenção.
4. **Lote e quintal na obra**: custo e tempo de construir, onde a casa pode nascer, valor; usos reais do quintal (horta, bichos,
   poço, ampliar a casa); meios diferentes já na fundação.
5. **Bairro denso emerge** (terra cara, renda baixa): casas coladas e empilhadas, com acesso real sempre (viela, escada, laje,
   telhado) por onde o NPC anda; casa sem acesso não nasce.
6. **Consequências do bairro denso**: higiene pior (contágio, água, escoamento, ligado à saúde); como a cidade é vista, por
   pessoa (origem, traços, experiência, crime): quem é de perto pode achar feio ou ter medo, quem é de fora pode visitar;
   efeito em onde morar, valor dos imóveis e migração; visita de fora usa hospedagem e mercado.
7. **Pendências da AD-173** (08d, itens 158–173): lotação das casas na cidade média, custo de escala acima do teto, números
   sem fonte, queda da fome sem causa achada.
8. **Cliente**: desenha só o que o motor disser (lote, rua por superfície, bairro denso, acessos); sai o quintal e a rua
   inventados na AD-171.
   **Feito na AD-177** (item 8, parte do cliente): rua pela camada `Roads` e superfície, lote pelo `lot`, ficha de rua e
   de lote, raio-x, planta, Tab. Bairro denso e acessos esperam o motor (AD-176).

## Lacunas do cliente deixadas pela AD-177
9. **Sem delta de rua**: o motor só manda `Roads` no retrato do mundo; o cliente relê a camada a cada 30 s (e logo depois de
   um lote apontar rua desconhecida), um retrato do mundo inteiro por vez. Pedir `roadUpserts`/`roadRemoved` no delta.
10. **Acesso e obra de rua** (viela, escada, laje, calçamento em andamento): só o ponto único em `street_style.gd`
    (`ACCESS_STYLES`, `WORK_STYLE`); falta o campo no contrato.
11. **Cerca ou sebe**: o motor diz "cercado" sem o tipo; o cliente desenha só cerca de varas. Quintal sem uso modelado
    (horta, bichos, poço; item 4): só o chão do terreiro.
12. **Estação no chão da cidade**: neve e seca recolorem o terreno, não a rua nem o quintal.
13. **Árvores fora da cidade**: a vegetação de enfeite (`scenery.gd`) ainda evita toda a caixa da cidade; o motor não diz
    onde há árvore.
14. **Raio-x sem interior**: o escopo Cidade não traz cômodos; a planta não mostra pessoas; o rótulo só usa o nome do
    prédio depois que a ficha dele foi lida (o escopo não traz nome).
15. **Andar pela rua**: o NPC segue as células que o servidor manda; se o motor andar em linha reta (item 1), o desenho
    também anda.

## Ordens do dono depois da AD-176 (2026-11-18)
- "Resolva a cidade média sem trilha, tem que funcionar no jogo." (08d item 181) → AD-179.
- Sobre a lama sem chuva: "implemente todas as estações, e temperatura." → AD-180 (clima completo).
- Sobre o diarista que recebe sem mudar a agenda: "deve mudar." (08d item 178) → AD-179: obra ocupa o tempo de quem trabalha nela.

## Cliente da AD-181 (rua de verdade na tela) e lacunas do contrato
**Fechados no cliente**: 12 (neve na rua e no caminho, estação no quintal), 9 (rua por `livingState.roads` + `roadUpserts`/`roadRemoved`; a releitura de 30 s só roda com servidor
antigo), 10 (obra com progresso e ficha; `lot.access` desenhado e na ficha), 14 em parte (a planta mostra as pessoas) e 15 (o
boneco anda pela `npc.route` até o `routeArrivalTick`).
16. **Evento de obra sem lugar nem obra**: `StreetWorkStarted`/`StreetWorkDone` vêm sem `location`, sem id da obra e (no
    payload projetado) sem `cityId` garantido; o cliente põe o marcador nas células com `work` (começo) ou nas que perderam
    `work` no mesmo delta (fim). Pedir `location` (célula do meio da obra) e `involved.workId`.
17. **Quem trabalha na obra**: `work` não traz os diaristas; a ficha diz "o motor ainda não diz" e tem o gancho
    (`work.workers`/`workerIds` vira link). Chega com a AD-179 (obra ocupa o tempo de quem trabalha).
18. **Acesso só da porta**: `lot.access` diz o tipo, não a geometria (de que vizinho é a escada, que laje se atravessa,
    onde a viela sai); o cliente desenha a peça na porta, para fora. Pedir as células do acesso.
19. **Rota sem a passagem por cima**: a rota que atravessa prédio colado (`OverPm`) vem como células comuns; o boneco
    anda no chão por dentro da planta. Pedir a marca da célula "por cima" na rota.
20. **Início da viagem**: o cliente usa `actionStartedAtTick` como saída; se o grupo da ação não vier (delta enxuto) e a
    rota mudar, cai na hora em que a rota chegou. Uma `routeDepartedTick` explícita tiraria a ambiguidade.
21. **Reserva sem uso**: `planned` marca o traçado mesmo quando usado; "reserva sem uso" é dedução do cliente
    (`planned` sem pisoteio, sem gente, sem obra, sem calçamento). Com a rua desligada (`LW_STREETS=off`) tudo vem `planned`.
22. **Desempenho a medir com mundo rodando**: o `bench.sh` mede com o mundo pausado (sem rota nem delta de rua).

## Construção por partes, melhoria e prédio que evolui (pedido do dono, 2026-11-17)
> "A construção das casas está realista? NPC precisa construir parte a parte da casa (inclusive melhoria de
> prédios), móveis etc. Isso bate com a spec de invenção que está na fila para ser feito. Prédios poderem evoluir,
> melhorar, nascer novos tipos de estabelecimentos (lojas, hotéis, prédios comerciais, etc) basicamente qualquer
> tipo de prédio e casa. Tão qual evolução de tudo que já foi inventado como veículos voadores, robôs etc. até mesmo
> celulares e internet. Obviamente tudo determinístico e procedural nada hardcoded."

Ordem (dono, 2026-11-17): "Coloca isso após fazer a parte do jogador". Itens 23 a 26 entram nesta trilha, depois da fatia do jogador.

23. **Obra por partes** (depois do jogador): fundação, estrutura, paredes, cobertura, aberturas e acabamento
    como etapas com material, saber e horas próprios; casa meio pronta é habitável com consequência (frio, chuva).
24. **Melhoria e ampliação**: cômodo, andar, troca de material e reparo são obras decididas pela casa ou pelo dono
    conforme meios e necessidade; o prédio guarda de que é feito e em que estado está.
25. **Móvel feito por alguém**: cada peça é fabricada (ou comprada, herdada, furtada), tem material, dono e lugar;
    casa nova nasce vazia. Liga com a issue #182 (interior como estado).
26. **Tipo de prédio que nasce do uso** (issue #186) e **evolução em cadeia do inventado** (issue #187): sem lista
    fixa de tipos nem de tecnologias.

## Motor da AD-185 (rota de verdade)
**Fechados no contrato**: 18 (`lot.accessCells`, `accessViaBuildingId`), 19 (`npc.routeOver`, índices na rota), 20
(`npc.routeDepartedTick`), 21 (`lot.accessState`: `reserved`/`planned`/`trodden`). Buscar água virou viagem (`WaterFetch`), emprego
vai à oficina mais perto de casa, fazendas da metrópole na borda. Em aberto (issues da AD-185): carga e veículo pela superfície,
poço como obra, água pesando onde a casa nasce, vizinho que leva a água.
