# Fase 22 — Imperfeição e diversidade

**Objetivo**: o mundo não é justo nem limpo. Defeito congênito, doença, deficiência e gente
ruim de coração existem — e, contra as probabilidades, também existe quem foi criado num
lar terrível e saiu bom. Moralidade **não é um eixo bom/mau**: emerge de temperamento,
criação e circunstância. O termo de **sorte** do modelo `w_gene + w_env + w_sorte` deixa de
ser desculpa e vira um canal declarado, forte o bastante para produzir o resultado
improvável e fraco o bastante para não apagar a causalidade que a Fase 7 mediu.

> **Spec, não gate.** Os critérios abaixo são a intenção; os critérios finais são escritos
> sob `rules/eval-criteria.md` quando a fase for ativada. Não comece esta fase antes da
> Fase 7 fechar.

## Tasks
1. **Condição como dado de cenário**, com **origem declarada** (genética, ambiental,
   acaso) e curso (congênita, adquirida, crônica, progressiva, remissiva). Nenhuma condição
   em `enum` de código — mesma regra que a Fase 3 impôs a profissão e recurso.
2. **Doença com eixos**: transmissão por **vetor nomeado** (contato, água, ar, ferida,
   vertical), letalidade, incubação, imunidade (nenhuma, temporária, permanente). O curso
   **individual** sai da rolagem do ADR-0011, com a resistência do NPC como modificador:
   mesma doença, desfechos diferentes.
3. **Deficiência com consequência funcional**, reusando o pré-requisito de marco da Fase
   20: a condição rebaixa o teto do eixo afetado, e as ações que o exigem saem do alcance.
4. **A cultura decide a reação, não a condição** — mesma regra da Fase 16. Igualitarismo,
   tradição, religiosidade e valorização da ciência escolhem entre acolher, esconder,
   excluir ou descartar. A condição não carrega a reação.
5. **Moralidade emergente, nunca campo**: comportamento moral é resultado de empatia,
   altruísmo, impulsividade (Fase 4), criação (Fase 21) e circunstância. "Gente ruim" é um
   padrão lido do event log, do mesmo jeito que a Fase 7 recusa um score global de aptidão.
6. **Canal de sorte explícito**: peso declarado no cenário, **stream próprio** no RNG
   (ADR-0005), cauda pelo perfil `Raro` do ADR-0011. Ser um termo nomeado — e não ruído
   espalhado por dez sistemas — é o que torna o improvável auditável e desligável.
7. **Orientação sexual como atributo**, independente de cultura, mais um **estado de
   divulgação** (assumido, oculto, negado) que evolui com a tolerância local, com o vínculo
   de quem sabe e com eventos de exposição.
8. **Divulgação alimenta estresse e risco**, e deixa o gancho de chantagem pronto para a
   Fase 23 consumir. Aqui existe o estado; a mecânica de segredo é de lá.
9. **Compatibilidade de cortejo revista** (Fase 7): valores, temperamento, orientação e
   estado de divulgação entram junto da atração, com **motivo nomeado** na rejeição — o
   mesmo mecanismo do `Incesto`.
10. **Cenários pareados**: duas culturas com tolerância oposta; o mesmo genoma em lar hostil
    e em lar acolhedor; e o mundo inteiro com `w_sorte` zerado, que é o braço de controle de
    todos os critérios abaixo.

## Critérios de verificação
- **O improvável acontece, e não é comum** — falseável nos dois lados: em 20 seeds existe
  **pelo menos um** NPC criado em ambiente hostil cujo resultado moral contradiz o previsto
  pelo ambiente. **Zero ocorrências reprova** (o canal de sorte está morto); taxa **acima**
  da faixa de `tests/baselines/` também reprova (a sorte virou ruído branco e apagou a
  causalidade). No braço com `w_sorte` zerado, a contagem tem de ser zero.
- **A cultura muda a divulgação, não a orientação** — os dois asserts juntos, um sem o
  outro não prova nada: par base/tratamento na mesma seed com tolerância oposta, a taxa de
  "assumido" diverge na direção prevista em **18/20 seeds**, e a distribuição de orientação
  é **byte-idêntica** nos dois braços.
- **Cortejo respeita orientação e divulgação, os dois lados**: zero pares formados violam a
  orientação declarada em 10 anos, assert a cada tick (negativo), **e** um cenário em que
  dois NPCs compatíveis em tudo o mais têm o cortejo rejeitado com motivo nomeado
  (positivo). Só o negativo passa também se ninguém nunca se encontrar — a armadilha da
  Fase 7.
- **Contágio conserva a cadeia**: todo caso novo tem caso-fonte com contato pelo vetor
  declarado dentro da janela de incubação. Caso sem cadeia reprova. E o conjunto de doenças
  instanciadas é subconjunto do catálogo do cenário carregado — ninguém adoece do que não
  existe. Assert a cada tick em 10 anos; 100 anos em nightly.
- **Nenhum campo de moralidade**: teste de arquitetura por reflexão sobre o esquema do NPC
  reprova qualquer escalar de alinhamento, karma ou bondade, e falha se algum campo novo do
  esquema ficar sem classificação.
- **A sorte entrou na conta**: zerar `w_sorte` muda o hash canônico em 10 anos — o mesmo
  braço que serve de controle ao primeiro critério.

## Fora do escopo
Segredo, chantagem, fofoca e traição como mecânica: Fase 23 — aqui só o estado de
divulgação. Medicina como profissão, hospital e cura: Fases 6, 5 e 8. Epidemia como relato
histórico e prosa: Fases 10 e 12. Poder que cura ou amaldiçoa: Fase 16. Prevalência de
condições é balanceamento e não tem gate (ADR-0010).

## Questões em aberto
- Que peso de sorte produz o improvável **sem** derrubar as correlações que a Fase 7 gravou
  em baseline? Se o baseline cair quando a sorte sobe, quem arbitra o trade-off?
- O par base/tratamento **fixa** o stream de sorte ou o deixa livre? Fixado, o improvável
  aparece nos dois braços e some do teste; livre, os braços deixam de ser comparáveis.
- Condição congênita de origem genética e predisposição da Fase 6 são o mesmo mecanismo com
  dois nomes? Se são, um dos dois deve sumir antes de virar código.
- Imunidade permanente extingue a doença do mundo em poucas gerações. Reintrodução é evento
  de cenário (como a redescoberta do livro na Fase 10) ou mutação do patógeno com stream
  próprio?
- Divulgação "negado" é crença do próprio NPC ou ação de fingimento? Só a segunda pode ser
  desmentida por prova, e a primeira mora na camada de crença da Fase 10.

## Ver também
[npc.md](../domain/npc.md) ·
[genetics-and-family.md](../domain/genetics-and-family.md) ·
[society.md](../domain/society.md) · [behavior.md](../domain/behavior.md) ·
[ADR-0011](../adr/ADR-0011-primitivo-unico-de-resolucao.md) ·
[ADR-0005](../adr/ADR-0005-simulacao-deterministica-semeada.md) ·
[ADR-0010](../adr/ADR-0010-potencia-como-modificador-unificado.md) ·
[rules/eval-criteria.md](../../rules/eval-criteria.md)
