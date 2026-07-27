# Fase 21 — Ontogenia

**Objetivo**: um recém-nascido não anda, não fala e não se alimenta sozinho — ele chora, e
o resto é adquirido pelo convívio. Capacidade de agir deixa de ser dada no nascimento e
passa a ser **conquistada por marco**. Correção honesta de escopo: isto **não é machine
learning**. Um modelo treinado por NPC seria caro, não-determinístico e quebraria o motor
(ADR-0005). O que produz o mesmo comportamento observável, de forma determinística e
barata, é um **modelo de desenvolvimento**: curvas por marco, janelas de idade e entradas
de exposição.

> **Spec, não gate.** Os critérios abaixo são a intenção; os critérios finais são escritos
> sob `rules/eval-criteria.md` quando a fase for ativada. Não comece esta fase antes da
> Fase 7 fechar.

## Tasks
1. **Marco como dado de cenário**, nunca `enum` de código: eixos motor grosso, motor fino,
   linguagem, autocuidado, cognição social e abstração; cada marco declara janela de idade
   típica (início, mediana, limite) e os marcos que exige antes de si.
2. **Toda ação declara o marco que exige**. O catálogo de ações da Fase 4 ganha o
   pré-requisito, consultável por reflexão — é ele que sustenta o primeiro critério. O
   recém-nascido só tem chorar, mamar e dormir, e **chora porque a fome pontua na utility**
   (Fase 4), como em The Sims: não existe script de bebê.
3. **Exposição como entrada acumulada por tick**, derivada do que já existe: quem coabita o
   household (Fase 7), fala dirigida à criança, contato físico, brincadeira e ensino
   deliberado são ações da rotina. Nenhum medidor novo alimentado à mão.
4. **Aquisição = progresso por rolagem do ADR-0011**, perfil `Agregado`, dificuldade em
   função da idade dentro da janela, da exposição acumulada e da predisposição. Uma curva
   por eixo, compartilhada por todos os NPCs — é isto que torna o modelo barato.
5. **Janela crítica com perda parcial permanente**: fechada a janela sem a exposição mínima
   do cenário, o teto do marco cai e não volta. Exposição tardia recupera parte, nunca
   tudo.
6. **Regressão sob trauma ou doença**: marco recente perde progresso, marco consolidado tem
   piso. Toda regressão é evento no log com causa nomeada.
7. **Variação individual por genética como predisposição** — multiplicador de taxa, o mesmo
   mecanismo da Fase 6. Nenhum marco nasce pronto e nenhuma habilidade atravessa o parto.
8. **Idioma é o dos cuidadores, não o da etnia**: fluência é marco de linguagem cujo alvo é
   o idioma de maior exposição. Vários cuidadores, vários idiomas, fluências distintas.
9. **Crenças e traços copiados de quem cria** pelo canal **ambiental** que a Fase 7 já mede
   e já separa do genético. Canal novo aqui seria duplicação.
10. **Cenários pareados**: cuidador presente vs. negligência; exposição no prazo vs.
    restaurada depois da janela vs. nenhuma; órfão criado por cuidador de outro idioma.
    Idade, genes e seed fixados por parâmetro.

## Critérios de verificação
- **Ninguém age acima do próprio desenvolvimento**: enumeração por reflexão de todo o
  catálogo de ações contra os marcos exigidos, reprovando se **alguma ação não declarar
  pré-requisito**; mais o assert, a cada tick em 10 anos, de que nenhum NPC executou ação
  cujo marco ele não atingiu. Par de mutação: desligar a checagem por flag de teste tem de
  **fazer este critério falhar**.
- **Convívio é causal**: par base/tratamento na mesma seed, tratamento = cuidador ausente.
  A criança negligenciada atinge **menos** marcos até a idade de corte declarada no
  cenário, na direção prevista, em **18/20 seeds**. Direção, não magnitude.
- **A janela crítica é falseável nos dois lados**: três braços na mesma seed — exposição no
  prazo, restaurada **depois** da janela, e nenhuma. Exige ordem estrita
  `prazo > tardia > nenhuma` em 18/20 e que a tardia **nunca** alcance a do prazo. Só
  `tardia < prazo` passaria com recuperação zero; o braço `nenhuma` prova que ela recupera
  algo.
- **O idioma vem de quem cria**: órfão criado por cuidador de outro idioma termina com
  fluência maior no idioma do cuidador do que no dos pais biológicos, 20/20 seeds, e
  nenhuma fluência é maior que zero ao nascer.
- **Regressão é datável e tem causa**: toda queda de marco tem, no mesmo tick, evento de
  trauma ou doença registrado. Uma única queda sem causa reprova; assert a cada tick em 10
  anos no gate, 100 anos em nightly.
- **Gene muda a taxa, exposição idêntica**: dois NPCs com genes idênticos e exposição
  idêntica terminam **byte-idênticos**; com genes diferentes e exposição idêntica, divergem
  na idade de aquisição. 20/20 nos dois sentidos — um sem o outro não prova nada.
- **Ontogenia entrou na conta**: desligar o sistema por flag de teste muda o hash canônico
  em 10 anos.

## Fora do escopo
Escola como edifício e oferta de vagas: Fase 8. Habilidade de ofício, tutoria e curva de
retornos decrescentes: Fase 6 — aqui só entra o pré-requisito de marco. Defeito congênito,
doença e deficiência: Fase 22. Prosa sobre a infância: Fase 12. Qualquer aprendizado por
modelo estatístico treinado em runtime: fora do projeto, por ADR-0005.

## Questões em aberto
- Criança em região **agregada** (Fase 8) não tem cuidador nomeado. A região aplica
  exposição média e o marco é reconciliado na materialização, ou toda criança materializa?
- Marco é escalar por eixo ou conjunto discreto de itens adquiridos? A resposta decide se
  "não sabe falar" é um limiar ou uma lista de ações liberadas — e muda o custo do teste.
- Ação indisponível **sai** do conjunto candidato da utility (barato) ou pontua zero
  (inspecionável)? Multiplicado por toda criança do mundo, isso é custo por tick.
- Choro é ação com utilidade normal ou saída forçada quando nada é elegível? Se for
  utilidade, existe um bebê que "escolhe" não chorar com fome máxima.
- Ontogenia para no fim da última janela, ou o adulto também regride e readquire? Se para,
  o sistema fica inerte para 80% da população e vale desligá-lo por idade.

## Ver também
[npc.md](../domain/npc.md) · [behavior.md](../domain/behavior.md) ·
[genetics-and-family.md](../domain/genetics-and-family.md) ·
[memory.md](../domain/memory.md) · [society.md](../domain/society.md) ·
[ADR-0011](../adr/ADR-0011-primitivo-unico-de-resolucao.md) ·
[ADR-0005](../adr/ADR-0005-simulacao-deterministica-semeada.md) ·
[rules/eval-criteria.md](../../rules/eval-criteria.md)
