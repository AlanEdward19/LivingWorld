# ADR-0013: Emergência aberta — o motor cria a estrutura, a LLM só nomeia

- **Status**: aceito
- **Data**: 2026-07-26
- **Decisores**: Alan

## Contexto
O usuário quer que raças, tecnologias e ideologias **novas** surjam sem intervenção — coisas
que ninguém escreveu num catálogo. Isso bate de frente com a regra mais dura do projeto: a
LLM não cria fato (ADR-0004, `rules/llm-boundary.md`). Se uma LLM inventar uma raça, o
mundo deixa de ser determinístico e reprodutível.

Mas "sem intervenção" não exige criatividade linguística. Exige **composição**.

## Decisão
Vamos separar as duas coisas com uma linha firme: **o motor cria a estrutura, a LLM produz
o rótulo.** Nomear não é criar fato — é etiquetar uma entidade que o motor já instanciou.

| O que emerge | Como o motor deriva, sem catálogo |
|---|---|
| Raça / subespécie | distância genética acumulada + isolamento reprodutivo + pressão ambiental. Especiação é aritmética |
| Tecnologia | composição de nós existentes + recursos + especialistas + estabilidade. O nó novo é a combinação |
| Ideologia / religião | mutação no vetor de valores culturais + evento fundador + transmissão |
| Poder / potência | combinação de eixos declarados (fonte, efeito, custo, falha) do ADR-0010 |
| Doença | mutação sobre eixos de patógeno (transmissão, letalidade, incubação, vetor) |

Regras que tornam isso seguro:
- A entidade emergente existe e é **funcional antes de ter nome**. O nome é cosmético.
- Sem LLM, o motor gera um nome determinístico por composição. O gate roda assim.
- O nome vindo da LLM é gravado **uma vez**, vira dado, e nunca mais é regerado — dois
  mundos com a mesma seed são idênticos exceto por rótulos.
- Nome se parte em dois, e só um é volátil (ADR-0014):
  - **Denominação** — `cultura × entidade → token de nome`. É **crença**, é **canônica** e é
    uma referência, nunca uma string. Que duas culturas discordem do nome é fato do mundo.
  - **Rótulo** — o texto exibido de um token. **Volátil**: nenhuma decisão o lê, e ele é
    recomputável por composição.
- Nome é dado por quem descobriu, dentro do mundo, e portanto está sujeito à distorção do
  ADR-0007: culturas diferentes chamam a mesma coisa por nomes diferentes, e discordam.

## Alternativas consideradas
- **Catálogo fechado de tudo** — determinístico, auditável e mata o pedido: nada realmente
  novo jamais aparece, só recombinação de itens pré-escritos e visíveis.
- **LLM cria a entidade inteira** — máxima novidade e destrói determinismo, reprodutibilidade
  e o gate de hash. Viola o ADR-0004 diretamente.
- **Nome gerado só por gramática procedural** — determinístico e grátis, e produz nomes que
  soam a gerador. Fica como fallback obrigatório, não como única opção.

## Consequências
- **Positivas**: novidade real sem quebrar determinismo; o gate nunca depende de LLM; nomes
  divergentes entre culturas saem de graça do sistema de crença; adicionar um eixo de
  composição multiplica o espaço de emergentes sem escrever conteúdo.
- **Negativas / trade-offs**: o espaço do que pode emergir é limitado pelos eixos que
  declararmos — é combinatória, não imaginação, e alguém vai notar; e balancear composição
  para que o emergente seja *plausível* (e não um monstro estatístico) é trabalho contínuo
  sem gate possível.
- **Follow-ups**: a fase de emergência aberta declara os eixos de cada categoria e o
  critério de "suficientemente novo para virar entidade" em vez de variação de uma existente.
