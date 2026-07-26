# Fase 18 — Cosmos e contato

**Objetivo**: o mundo cresce até a escala de sistema estelar sem máquina nova — são
**degraus de LOD acima do global**. Corpos e órbitas vivem em estatística, o céu entra
porque tem consequência agrícola e cultural, e alien ou conquistador não é tipo novo: é
cultura em outro degrau tecnológico chegando por contato.

> **Spec, não gate.** Os critérios abaixo são a intenção; os critérios finais são escritos
> sob `rules/eval-criteria.md` quando a fase for ativada. Não comece esta fase antes da
> Fase 8 fechar.

## Tasks
1. **Degraus `sistema` e `planeta` no topo da pilha de LOD** da Fase 8. Resolução agregada,
   detalhada e máxima valem igual; a conservação na promoção continua sendo a regra dura.
   Nada de segunda máquina de simulação.
2. **Corpos e órbitas em estatística pura**: estrela, luas, planetas, elementos orbitais,
   recursos orbitais. Um tick de sistema produz totais e datas, nunca pessoas.
3. **Calendário astronômico derivado das órbitas**, não tabelado à mão: estações, eclipses,
   cometas e conjunções saem dos mesmos elementos e são previsíveis pelo motor.
4. **Consequência aterrissa em sistema que já existe**: estação e eclipse entram como
   modificador de produção agrícola (Fase 5) e como presságio na cultura e na legitimidade
   (Fases 9 e 16). A mesma efeméride, dois usos, conforme o nível tecnológico de quem olha.
5. **Contato como evento que promove ao detalhe**: uma civilização distante existe só em
   estatística até tocar o planeta principal; aí a região vira detalhe, com cultura,
   liderança e economia coerentes com o agregado de onde veio — igual ao ferreiro da Fase 8.
6. **Alien e conquistador reusam cultura, tecnologia, economia, guerra e diplomacia**. O
   degrau tecnológico é módulo de conteúdo da Fase 12 (o pacote "futurista" encostando no
   "medieval"). Nenhuma entidade nova, nenhum handler exclusivo de alien.
7. **Assimetria tecnológica como pressão, não desfecho**: colapso cultural, culto de carga,
   conquista, tutela, extermínio e adaptação seletiva saem dos valores culturais, da coesão
   política e do que os recém-chegados querem. Nenhum roteirizado, nenhum sorteado de tabela.
8. **Colônia com custo de viagem e atraso de comunicação** derivados da distância orbital.
   Autonomia é consequência do atraso; independência colonial é divergência cultural mais
   atraso, sem sistema próprio.
9. **Cenários pareados**: mundo com e sem degrau cósmico; mundo com e sem contato; ano com
   e sem eclipse na janela de colheita.

## Critérios de verificação
- **O cosmos não vaza sem evento**: adicionar o degrau `sistema` a um mundo **sem contato**
  deixa o hash canônico byte-idêntico ao do mesmo mundo sem o degrau, a cada tick em 10 anos
  no gate; 100 anos em nightly. É a prova de que astronomia sem consequência não entra na
  conta — e o par oposto vale: com contato no cenário, o hash **tem** de mudar.
- **Conservação orbital contra fonte independente**: para todo corpo e todo tick, a soma dos
  agregados do degrau `sistema` mais o `COUNT(*)` do que foi promovido a detalhe bate com o
  total, ambos lidos sem tocar a propriedade derivada — a mesma invariante da Fase 8, um
  nível acima. Promover uma lua **move** população e recurso; nunca cria.
- **Eclipse e estação mexem na colheita, com controle**: par base/tratamento na mesma seed,
  tratamento = eclipse (e, no segundo par, estação adversa) posicionado na janela de
  colheita declarada no cenário. Produção agrícola do braço tratado menor que a do base,
  10/10 seeds, com a diferença maior que o spread entre duas seeds do baseline.
- **Contato promove e devolve sem perda**: o round-trip da Fase 8 aplicado à região de
  contato — promover a detalhe e desmaterializar depois deixa `Hash(world)` byte-idêntico,
  totais de população, recurso e produção inclusos.
- **A colônia decide com o que já chegou**: mesma família do teste de conhecimento limitado
  da Fase 10 — o cenário planta uma ordem da metrópole cujo tick de entrega ainda não
  passou, e a decisão da colônia é byte-idêntica à de um braço onde a ordem nunca foi
  enviada. Qualquer divergência é informação viajando mais rápido que o atraso.
- **Alien não é tipo novo**: enumeração por reflexão dos sistemas que a civilização
  contatante alcança; o teste reprova se ela tocar qualquer handler, tabela ou campo que uma
  cultura nativa não toque. Cobertura nos dois sentidos — sistema alcançado sem par nativo
  também reprova.

## Fora do escopo
Galáxia e multiverso: não há degrau acima de `sistema` e inventá-lo agora custaria sem pagar
nada. Trânsito entre linhas temporais: Fase 19; ramificação: Fase 17. Culto de carga como
economia de crença: Fase 16 (aqui só é um desfecho possível). Tecnologia alienígena como
fonte de potência: Fase 15. Prosa sobre o primeiro contato: Fase 11.

## Questões em aberto
- Uma civilização distante que nunca toca o planeta precisa existir como agregado desde o
  tick 0, ou nasce no evento de contato? Se nasce, ela tem passado — e de onde ele vem.
- Previsibilidade do eclipse é propriedade do fenômeno ou conhecimento da cultura (Fase 12)?
  A resposta decide se presságio e ferramenta política são um sistema ou dois.
- Atraso de comunicação é fila de eventos com tick de entrega ou snapshot defasado do
  conhecimento da colônia? O critério de decisão sem informação depende de qual for.
- Colapso demográfico por doença no contato não tem sistema: vira mortalidade parametrizada
  pelo cenário, ou exige epidemiologia que o roadmap não tem em fase nenhuma?
- Colônia que se separa vira entidade política nova, cultura nova, ou só divergência
  acumulada com um limiar? Muda a contagem de cidades e o teste de conservação.

## Ver também
[cosmos.md](../domain/cosmos.md) · [simulation-lod.md](../domain/simulation-lod.md) ·
[society.md](../domain/society.md) · [world-map.md](../domain/world-map.md) ·
[economy.md](../domain/economy.md) ·
[divinity-and-belief.md](../domain/divinity-and-belief.md) ·
[ADR-0011](../adr/ADR-0011-primitivo-unico-de-resolucao.md) ·
[rules/eval-criteria.md](../../rules/eval-criteria.md)
