# Fase 24 — Emergência aberta

**Objetivo**: raça, tecnologia, ideologia, potência e doença **novas** aparecem sem catálogo,
porque o motor as deriva por composição de eixos já declarados. A LLM só produz o rótulo: a
entidade emergente existe, funciona e entra na conta **antes de ter nome** (ADR-0013).
Nomear é etiquetar o que o motor já instanciou — nunca criar fato.

> **Spec, não gate.** Os critérios abaixo são a intenção; os critérios finais são escritos
> sob `rules/eval-criteria.md` quando a fase for ativada. Não comece esta fase antes da
> Fase 13 fechar.

## Tasks
1. **Especiação como aritmética**: distância genética acumulada (Fase 7) + isolamento
   reprodutivo + pressão ambiental (Fase 2) cruzam um limiar declarado no cenário e o motor
   instancia uma subespécie. Nenhuma raça em `enum`, mesma regra da Fase 3 para profissão.
2. **Tecnologia como composição**: nó novo = nós existentes + recursos disponíveis +
   especialistas vivos + estabilidade da comunidade. Os pré-requisitos são **declarados** e
   consultáveis por reflexão — é o registro que sustenta o critério de pré-requisito.
3. **Ideologia e religião**: mutação no vetor de valores culturais (`society.md`) + evento
   fundador + transmissão. Se pegar, vira doutrina consumível pela Fase 17.
4. **Potência nova por combinação dos eixos do ADR-0010** (fonte, efeito, custo,
   probabilidade, modo de falha, consequência social). Nenhum eixo é reescrito aqui.
5. **Doença nova por mutação sobre eixos de patógeno** (transmissão, letalidade, incubação,
   vetor), sorteada pelo perfil `Raro` do primitivo do ADR-0011.
6. **Limiar de novidade**: regra única que decide entre *entidade nova* e *variação de uma
   existente*, medida como distância no espaço de eixos da categoria. Vale para as cinco
   categorias — uma regra, não cinco.
7. **Nomeação determinística por composição** como fallback **obrigatório**: o gate roda sem
   LLM e tudo funciona. O rótulo da LLM é opcional por design (`rules/llm-boundary.md`).
8. **Nome se parte em dois** (ADR-0013 + ADR-0014): **denominação** — `cultura × entidade →
   token de nome` — é crença, é **canônica** e é sempre referência, nunca string; **rótulo**
   — o texto exibido do token — é **volátil**, recomputável por composição e lido por decisão
   nenhuma. Gravado uma vez, nunca regerado. O id estável é estrutural, derivado dos eixos.
9. **Nome é de quem descobriu**: a denominação pertence à cultura descobridora e viaja pelos
   relatos da Fase 10. Culturas diferentes nomeiam a mesma coisa de formas diferentes e
   discordam — divergência de denominação é dado, não bug.
10. **Cenário `test-emergence` pareado**: o mesmo mundo com e sem emergência ligada, e o
    mesmo mundo com e sem provider de LLM, para servir de controle aos critérios.

## Critérios de verificação
- **Funcional antes de ter nome** — o critério que sustenta o ADR inteiro: o gate roda com
  `NullLlmProvider` e todas as entidades emergentes nascem, agem e são consultáveis. Ligar o
  provider de LLM na **mesma seed** deixa o hash canônico byte-idêntico em 10 anos; só os
  campos de rótulo mudam. Qualquer divergência de hash prova que a LLM criou fato.
- **Denominação dentro, rótulo fora**: mutar por reflexão **qualquer** denominação
  (`cultura × entidade → token`) **muda** o hash canônico; mutar **todos** os rótulos de
  todas as entidades emergentes **não** muda, e mexe só na projeção volátil. Falha se algum
  campo de qualquer dos dois lados ficar sem cobertura. Par de mutação: trocar a classe de um
  dos dois tem de **fazer este critério falhar**.
- **A mesma seed produz as mesmas entidades emergentes**: dois processos separados, mesma
  seed, 10 anos — o conjunto de identificadores estruturais e a ordem de nascimento são
  idênticos. Comparação sobre estrutura, nunca sobre nome.
- **Isolamento prolongado aumenta a especiação**: par base/tratamento na mesma seed,
  tratamento = rota de migração cortada entre duas populações. A taxa de especiação do braço
  tratado é maior em ≥ 18/20 seeds. Direção, não magnitude; sem o braço base, deriva
  demográfica explicaria sozinha.
- **Nenhuma tecnologia emerge sem pré-requisito**: enumerar por reflexão todo nó emergido no
  run e exigir que **todos** os pré-requisitos declarados existissem no tick da emergência —
  falha se algum nó ficar sem cobertura, e falha se algum pré-requisito declarado nunca for
  exercido pelo cenário. Só a primeira metade passa com a emergência desligada.
- **Emergência entrou na conta**: desligar o sistema por flag muda o hash canônico em 10 anos.

## Fora do escopo
Potência genérica: Fase 16. Deus e economia de crença consomem a ideologia daqui, mas são
Fase 17. Módulos de conteúdo pré-escritos: Fase 13 — emergência não os substitui, compõe
sobre eles. Prosa que apresenta o emergente ao leitor: Fase 12. Distorção do nome ao longo
da transmissão: Fase 10. Plausibilidade do emergente é balanceamento contínuo e **não tem
gate** (ADR-0013).

## Questões em aberto
- O limiar de novidade é distância no espaço de eixos ou contagem de eixos divergentes? A
  resposta decide quantas entidades nascem por século e o tamanho do índice da Fase 26.
- Isolamento reprodutivo é regra dura (não cruzam mais) ou penalidade contínua de
  fertilidade? A regra dura é barata e cria espécies por acidente de mapa.
- Tecnologia cujos últimos especialistas morreram: a entidade permanece como nó morto ou é
  coletada? Se coletada, para onde apontam os relatos que ainda falam dela?
- Uma praga que também é potência de um deus (Fase 17) atravessa duas categorias de eixos.
  É uma entidade emergente com dois descritores, ou duas entidades acopladas?

## Ver também
[society.md](../domain/society.md) ·
[genetics-and-family.md](../domain/genetics-and-family.md) ·
[powers.md](../domain/powers.md) ·
[historical-memory.md](../domain/historical-memory.md) ·
[ADR-0013](../adr/ADR-0013-emergencia-aberta-motor-estrutura-llm-nome.md) ·
[ADR-0010](../adr/ADR-0010-potencia-como-modificador-unificado.md) ·
[ADR-0011](../adr/ADR-0011-primitivo-unico-de-resolucao.md) ·
[ADR-0014](../adr/ADR-0014-canonico-vs-volatil.md) ·
[rules/llm-boundary.md](../../rules/llm-boundary.md) ·
[rules/eval-criteria.md](../../rules/eval-criteria.md)
