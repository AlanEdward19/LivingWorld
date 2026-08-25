# Fase 21 — Ontogenia — Specification

## Problem Statement

Hoje um NPC nasce com capacidade de agir plena. Esta fase corrige isso: um recém-nascido não
anda, não fala, não se alimenta sozinho — ele chora, e o resto é conquistado por marco de
desenvolvimento ao longo do convívio. **Não é machine learning** (ADR-0005 proíbe modelo treinado
em runtime) — é um modelo de desenvolvimento determinístico e barato: curvas por marco, janelas
de idade e entradas de exposição, a mesma curva compartilhada por todos os NPCs.

## Goals

- [ ] Marco é dado de cenário (nunca `enum` de código) em 6 eixos — motor grosso, motor fino,
      linguagem, autocuidado, cognição social, abstração — cada um um escalar contínuo `[0,1)`
      progredindo por rolagem, com janela de idade (início/mediana/limite) e pré-requisitos de
      outros marcos.
- [ ] Toda ação do catálogo da Fase 4 declara o marco/limiar que exige, consultável por
      reflexão — recém-nascido só tem chorar/mamar/dormir disponíveis porque nenhuma outra ação
      passa no filtro de pré-requisito, nunca por script de bebê.
- [ ] Exposição é entrada acumulada por tick, derivada 100% de dados já existentes (coabitação
      do household da Fase 7, fala dirigida, contato físico, brincadeira, ensino deliberado como
      ações de rotina) — nenhum medidor novo alimentado à mão.
- [ ] Aquisição é progresso por rolagem (ADR-0011, perfil `Agregado`, sem críticos), dificuldade
      função de idade dentro da janela + exposição acumulada + predisposição genética.
- [ ] Janela crítica fechada sem exposição mínima reduz o teto permanentemente — exposição
      tardia recupera parte, nunca o total.
- [ ] Regressão por trauma/doença reduz marco recente sem piso, marco consolidado com piso —
      toda regressão é evento no log com causa nomeada.
- [ ] Predisposição genética é multiplicador de taxa (mesmo mecanismo já usado na Fase 6) —
      nenhum marco nasce pronto, nenhuma habilidade atravessa o parto.
- [ ] Fluência de idioma é marco de linguagem cujo alvo é o idioma de maior exposição entre os
      cuidadores — não o idioma da etnia/cultura declarada.
- [ ] Crenças/traços copiados de quem cria usam o canal ambiental já separado do genético pela
      Fase 7 — nenhum canal novo.
- [ ] Criança em região agregada (sem cuidador nomeado, LOD baixo) recebe exposição média do
      agregado; o marco real é reconciliado só na materialização — nunca força materialização de
      toda criança do mundo.
- [ ] Ação sem marco atingido sai do conjunto candidato da utility AI antes da pontuação —
      nunca permanece pontuando zero.
- [ ] Chorar é ação comum na utility AI, pontuada pela fome como qualquer necessidade (Fase 4) —
      nenhum script/fallback especial de "saída forçada".
- [ ] Ontogenia é sistema finito: para no fim da última janela declarada — NPC "adulto
      desenvolvido" fica inerte pro sistema, sem custo de tick adicional; regressão por
      trauma/doença (Fase 22) é sistema separado que ainda pode agir depois.

## Out of Scope

| Item | Razão |
| --- | --- |
| Escola como edifício e oferta de vagas | Fase 8 — aqui só o pré-requisito de marco que uma escola poderia acelerar. |
| Habilidade de ofício, tutoria, curva de retornos decrescentes | Fase 6 — esta fase só consome o mecanismo de predisposição/multiplicador, não redefine skill. |
| Defeito congênito, doença, deficiência | Fase 22 (Imperfeição) — aqui só o modelo saudável de desenvolvimento. |
| Prosa sobre a infância | Fase 12 (Narrativa). |
| Aprendizado por modelo estatístico treinado em runtime | Fora do projeto, por ADR-0005 — decisão irrevogável, não uma escolha desta fase. |
| Regressão/readquisição de marco pra adultos fora do gatilho de trauma/doença (Fase 22) | Decisão explícita do usuário — ontogenia para no fim da última janela. |

## Assumptions & Decisions (confirmadas com o usuário — 2026-08-25)

| Decisão | Escolha confirmada | Racional |
| --- | --- | --- |
| Criança em região agregada | **Exposição média do agregado + reconciliação na materialização** — mesma disciplina de LOD já usada pra idade/necessidades | Usuário confirmou explicitamente (Recommended) — preserva custo por NPC-tick da Fase 9 |
| Forma do marco | **Escalar contínuo por eixo `[0,1)`**, ações declaram limiar mínimo | Usuário confirmou explicitamente (Recommended) — 1 número por eixo por NPC, barato e testável |
| Ação indisponível | **Sai do conjunto candidato da utility AI**, nunca pontua zero | Usuário confirmou explicitamente (Recommended) — evita custo de calcular utilidade de ação impossível, multiplicado por toda criança do mundo |
| Choro | **Ação comum na utility, pontuada pela fome como qualquer necessidade** — sem script especial | Usuário confirmou explicitamente (Recommended) — mesmo espírito "The Sims" já citado no roadmap, emergente não scriptado |
| Fim da ontogenia | **Para no fim da última janela** — sistema fica inerte pro NPC desenvolvido, sem custo adicional; regressão por trauma/doença é sistema separado (Fase 22) | Usuário confirmou explicitamente (Recommended) — evita pagar custo pra 80% da população adulta |

**Todas as 5 decisões estão confirmadas** — nenhuma pendência bloqueando Design.

## User Stories

### P1: Marco como dado de cenário, ação declara pré-requisito consultável

**User Story**: Como quem desenha o catálogo de ações do mundo, quero declarar marcos (6 eixos,
janela de idade, pré-requisitos) inteiramente como dado de cenário, e que toda ação declare o
marco/limiar que exige — sem nenhum `enum` de marco hardcoded no motor.

**Why P1**: É a fundação que sustenta o critério mais forte da fase ("ninguém age acima do
próprio desenvolvimento").

**Acceptance Criteria**:

1. WHEN um marco é declarado no cenário THEN ele SHALL especificar eixo (motor grosso/fino/
   linguagem/autocuidado/cognição social/abstração), janela de idade (início, mediana, limite) e
   lista de marcos pré-requisito — nunca um `enum` de código fixo.
2. WHEN uma ação do catálogo da Fase 4 é registrada THEN ela SHALL declarar o eixo e o limiar
   mínimo `[0,1)` que exige para ficar disponível — ação sem essa declaração é erro de
   configuração de cenário, nunca comportamento implícito.
3. WHEN o catálogo de ações completo é enumerado por reflexão THEN o teste SHALL reprovar se
   qualquer ação não declarar seu pré-requisito de marco.
4. WHEN um recém-nascido é avaliado THEN as únicas ações candidatas SHALL ser as que não exigem
   nenhum marco acima do valor inicial (ex.: chorar, mamar, dormir) — nunca por script dedicado
   de "bebê", só pelo filtro de limiar.

**Independent Test**: catálogo de 50 ações com pré-requisitos declarados — enumeração por
reflexão passa; remover o pré-requisito de 1 ação a reprova; recém-nascido só tem 3 ações
elegíveis, todas com limiar 0.

---

### P1: Exposição acumulada por tick, sem medidor novo

**User Story**: Como quem quer convívio como causa real de desenvolvimento, quero que a
exposição de uma criança seja derivada 100% de dados já existentes — coabitação, fala dirigida,
contato físico, brincadeira, ensino deliberado — sem inventar um medidor alimentado à mão.

**Why P1**: É a garantia de reuso — sem ela, "convívio importa" vira um número mágico.

**Acceptance Criteria**:

1. WHEN uma criança coabita um household (Fase 7) com um ou mais cuidadores THEN a exposição
   acumulada por tick SHALL ser função de ações de rotina já existentes desses cuidadores (fala
   dirigida, contato físico, brincadeira, ensino) — nenhum campo novo alimentado fora dessas
   ações.
2. WHEN nenhum cuidador interage com a criança num tick THEN a exposição daquele tick SHALL ser
   zero (ou o piso mínimo declarado no cenário) — nunca um valor positivo sem causa rastreável.
3. WHEN um par base/tratamento roda na mesma seed, tratamento = cuidador ausente/negligente THEN
   a criança negligenciada SHALL atingir menos marcos até a idade de corte declarada no cenário,
   na direção prevista, em 18/20 seeds.

**Independent Test**: par base/tratamento — criança com cuidador presente vs. cuidador
negligente — menos marcos atingidos no braço tratado, 18/20 seeds.

---

### P1: Aquisição por rolagem `Agregado`, janela crítica com perda permanente

**User Story**: Como quem desenha a curva de desenvolvimento, quero que a aquisição de marco seja
uma rolagem (ADR-0011, perfil `Agregado`, sem críticos) função de idade+exposição+predisposição,
e que fechar a janela sem exposição mínima reduza o teto permanentemente — exposição tardia
recupera parte, nunca tudo.

**Why P1**: É o mecanismo central que torna "quando" importa tanto quanto "quanto".

**Acceptance Criteria**:

1. WHEN o progresso de um marco é avaliado a cada tick dentro da janela THEN o motor SHALL
   rolar via `Resolver.Resolve` com perfil `Agregado`, dificuldade função de
   `(idade dentro da janela, exposição acumulada, predisposição genética)` — nenhuma fórmula
   paralela fora do primitivo único.
2. WHEN a janela de um marco fecha (idade ultrapassa o limite declarado) sem a exposição mínima
   do cenário THEN o teto máximo daquele eixo SHALL cair permanentemente — nunca retornar ao
   teto original por nenhuma exposição futura.
3. WHEN três braços rodam na mesma seed — exposição no prazo, restaurada depois da janela, e
   nenhuma — THEN a ordem estrita `prazo > tardia > nenhuma` SHALL valer em 18/20 seeds, e
   `tardia` NUNCA SHALL alcançar `prazo` (recuperação é sempre parcial, nunca total).

**Independent Test**: 3 braços pareados (prazo/tardia/nenhuma) — ordem estrita em 18/20 seeds;
tardia sempre estritamente menor que prazo.

---

### P1: Regressão datável com causa nomeada

**User Story**: Como quem quer trauma/doença com consequência mecânica real, quero que toda
regressão de marco tenha, no mesmo tick, um evento de causa nomeada — marco recente perde
progresso, marco consolidado tem piso.

**Why P1**: É a garantia de auditabilidade — regressão sem causa é bug, não feature.

**Acceptance Criteria**:

1. WHEN um evento de trauma/doença aciona regressão de marco THEN o motor SHALL registrar, no
   mesmo tick, um evento no log com a causa nomeada (`WorldEventKind` + referência ao trauma/
   doença) — nunca uma queda silenciosa.
2. WHEN o marco afetado é "recente" (dentro de uma janela de consolidação declarada no cenário)
   THEN a perda SHALL poder chegar até um piso mais baixo (sem piso protetor forte).
3. WHEN o marco afetado já está "consolidado" (fora da janela de consolidação) THEN a perda
   SHALL respeitar um piso mínimo declarado — nunca reverter abaixo dele.
4. WHEN o log de eventos é auditado ao longo de 10 anos simulados (gate) / 100 anos (nightly)
   THEN toda queda de valor de marco SHALL ter um evento de causa no mesmo tick — uma única
   queda sem causa reprova.

**Independent Test**: trauma forçado num marco recente e num marco consolidado — recente cai
mais, consolidado respeita piso; auditoria de 10 anos não encontra queda sem evento.

---

### P1: Predisposição genética como multiplicador de taxa

**User Story**: Como quem quer variação individual real, quero que a predisposição genética seja
um multiplicador de taxa de aquisição — mesmo mecanismo já usado na Fase 6 — nunca um marco que
nasce pronto.

**Why P1**: Preserva "nenhuma habilidade atravessa o parto" como garantia dura.

**Acceptance Criteria**:

1. WHEN dois NPCs têm genes idênticos e exposição idêntica THEN eles SHALL terminar
   byte-idênticos na trajetória de aquisição de marco — 20/20 seeds.
2. WHEN dois NPCs têm genes diferentes e exposição idêntica THEN eles SHALL divergir na idade de
   aquisição — 20/20 seeds.
3. WHEN um NPC nasce THEN nenhum eixo de marco SHALL iniciar acima do valor mínimo declarado
   pro nascimento (tipicamente 0) — nenhum marco/habilidade "atravessa o parto" via genética.

**Independent Test**: par genes-idênticos/exposição-idêntica byte-idêntico (20/20); par
genes-diferentes/exposição-idêntica diverge (20/20).

---

### P1: Idioma dos cuidadores, não da etnia

**User Story**: Como quem quer órfãos e adoção com consequência real, quero que a fluência de
linguagem tenha como alvo o idioma de maior exposição entre os cuidadores reais — nunca o idioma
"esperado" pela etnia/cultura de nascimento.

**Why P1**: Decisão explícita do domínio — "idioma é o dos cuidadores, não da etnia".

**Acceptance Criteria**:

1. WHEN uma criança é criada por um ou mais cuidadores THEN o alvo de fluência de linguagem
   SHALL ser o idioma de maior exposição acumulada entre eles — nunca o idioma declarado da
   etnia/cultura de nascimento se ele diverge do(s) cuidador(es).
2. WHEN vários cuidadores falam idiomas diferentes THEN a criança SHALL desenvolver fluências
   distintas por idioma, proporcionais à exposição de cada um.
3. WHEN um órfão é criado por um cuidador de idioma diferente do dos pais biológicos THEN ele
   SHALL terminar com fluência maior no idioma do cuidador do que no dos pais biológicos, em
   20/20 seeds, e nenhuma fluência SHALL ser maior que zero ao nascer.

**Independent Test**: órfão criado por cuidador de idioma B (pais biológicos falavam idioma A) —
fluência em B maior que em A, 20/20 seeds; fluência em ambos é 0 no nascimento.

---

### P1: Crenças/traços copiados pelo canal ambiental já existente

**User Story**: Como quem quer nutrição cultural sem duplicar sistema, quero que crenças e
traços copiados de quem cria usem exatamente o canal ambiental que a Fase 7 já separa do
genético.

**Why P1**: Garantia de reuso — canal novo aqui seria duplicação direta.

**Acceptance Criteria**:

1. WHEN uma criança copia uma crença/traço de um cuidador THEN a transmissão SHALL passar pelo
   mesmo canal ambiental já usado pela Fase 7 (nunca um canal genético, nunca um canal novo
   criado por esta fase).
2. WHEN o canal ambiental já é testado como separado do genético (Fase 7) THEN essa mesma
   suíte/garantia SHALL continuar valendo sem modificação — esta fase só adiciona conteúdo
   transmitido pelo canal, nunca sua mecânica.

**Independent Test**: crença copiada de cuidador aparece via canal ambiental existente,
verificável pela mesma suíte de separação genético/ambiental da Fase 7, sem novo campo de canal.

---

### P1: Criança agregada — exposição média + reconciliação tardia

**User Story**: Como quem quer escala sem forçar materialização de toda criança, quero que uma
criança em região agregada (sem cuidador nomeado, LOD baixo) receba exposição média do agregado,
com o marco real calculado só na materialização.

**Why P1**: Preserva o teto de custo por NPC-tick da Fase 9 mesmo com ontogenia ativa.

**Acceptance Criteria**:

1. WHEN uma criança existe numa região agregada THEN sua exposição por tick SHALL ser
   aproximada pela taxa média de exposição do agregado (mesma disciplina de LOD já usada pra
   idade/necessidades) — nunca custar o cálculo individual de rotina completa.
2. WHEN essa criança é materializada THEN o motor SHALL reconciliar seu estado de marco a partir
   da exposição média acumulada durante o período agregado — determinístico, reproduzível pela
   mesma seed.
3. WHEN o custo por tick do mundo é medido com uma fração grande de crianças em região agregada
   THEN o custo NÃO SHALL escalar como se toda criança estivesse materializada.

**Independent Test**: criança em região agregada por 5 anos, depois materializada — estado de
marco reconciliado determinístico; custo medido não escala com toda a população infantil
materializada.

---

### P1: Filtro de disponibilidade antes da pontuação, choro sem script especial

**User Story**: Como quem quer utility AI barata e sem casos especiais, quero que ação sem marco
atingido saia do conjunto candidato ANTES da pontuação (nunca pontue zero), e que chorar seja
uma ação comum pontuada pela fome como qualquer necessidade — sem fallback forçado.

**Why P1**: Decisão explícita do usuário em ambos os pontos — mantém a utility AI consistente e
barata.

**Acceptance Criteria**:

1. WHEN o conjunto de ações candidatas é montado pra um NPC num tick THEN ações cujo limiar de
   marco não foi atingido SHALL ser excluídas do conjunto ANTES de qualquer cálculo de
   utilidade — nunca calculadas e descartadas depois.
2. WHEN um recém-nascido com fome alta é avaliado THEN "chorar" SHALL competir no mesmo
   pipeline de utility que qualquer outra necessidade (Fase 4), pontuado pela fome — nenhum
   caminho de código força "chorar" como saída garantida fora da utility.
3. WHEN o custo de montar o conjunto candidato é medido THEN ele SHALL ser proporcional ao
   número de ações cujo pré-requisito passa no filtro — nunca ao catálogo completo de ações do
   mundo.

**Independent Test**: NPC recém-nascido com fome alta — "chorar" vence por utilidade na maioria
dos ticks, mas não é hardcoded (outro NPC com necessidade diferente e mesma idade pode ter outra
ação vencedora se disponível); ações fora do desenvolvimento nunca aparecem no conjunto avaliado.

---

### P1: Ontogenia é sistema finito

**User Story**: Como quem quer custo previsível, quero que o sistema de ontogenia pare de
avaliar um NPC assim que a última janela declarada fecha — adulto desenvolvido não paga custo de
tick adicional; regressão por trauma/doença (Fase 22) é sistema separado.

**Why P1**: Decisão explícita do usuário — evita pagar custo pra 80% da população adulta.

**Acceptance Criteria**:

1. WHEN a última janela de marco declarada no cenário fecha para um NPC THEN o sistema de
   ontogenia SHALL parar de avaliá-lo em ticks subsequentes — nenhum recálculo/rolagem
   adicional ocorre por este sistema.
2. WHEN o custo por tick do mundo é medido com uma população majoritariamente adulta THEN o
   custo atribuível a este sistema SHALL ser proporcional apenas aos NPCs ainda dentro de
   alguma janela — nunca à população total.
3. WHEN um NPC "adulto desenvolvido" sofre trauma/doença (Fase 22, fora de escopo aqui) THEN
   essa regressão SHALL ser tratada por um sistema diferente — esta fase não reabre avaliação
   de janela pra ele.

**Independent Test**: mundo com 80% de adultos desenvolvidos e 20% de crianças em janela — custo
por tick medido escala só com os 20%.

## Edge Cases

- WHEN um marco declara pré-requisito de outro marco que nunca é atingido (configuração de
  cenário inconsistente) THEN o motor SHALL sinalizar erro de configuração explícito — nunca
  travar silenciosamente numa dependência circular ou inatingível.
- WHEN uma criança não tem NENHUM cuidador (órfã sem adoção, região sem household) THEN a
  exposição SHALL ser o piso mínimo declarado no cenário (possivelmente zero) — nunca erro por
  ausência de cuidador.
- WHEN dois cuidadores oferecem exposição EXATAMENTE igual em idiomas diferentes THEN o motor
  SHALL resolver o alvo de fluência dominante deterministicamente (mesma seed/regra de
  desempate declarada) — nunca resultado ambíguo/não-determinístico.
- WHEN a janela crítica de um marco fecha no MESMO tick em que a exposição mínima é atingida
  THEN o motor SHALL declarar explicitamente (na Design) se conta como "no prazo" ou "tardia" —
  resolvido na fase de Design, não deixado implícito.

## Requirement Traceability

| ID | Story | Status |
| --- | --- | --- |
| ONT-01 | P1: Marco — declarado como dado de cenário, nunca enum de código | Pending |
| ONT-02 | P1: Marco — toda ação declara eixo+limiar | Pending |
| ONT-03 | P1: Marco — enumeração por reflexão reprova ação sem pré-requisito | Pending |
| ONT-04 | P1: Marco — recém-nascido só tem ações de limiar 0 | Pending |
| ONT-10 | P1: Exposição — derivada de ações de rotina existentes, sem medidor novo | Pending |
| ONT-11 | P1: Exposição — sem interação, exposição zero/piso | Pending |
| ONT-12 | P1: Exposição — par negligência, menos marcos, 18/20 seeds | Pending |
| ONT-20 | P1: Aquisição — rolagem Agregado, dificuldade multi-fator | Pending |
| ONT-21 | P1: Aquisição — janela fechada sem exposição mínima reduz teto permanentemente | Pending |
| ONT-22 | P1: Aquisição — ordem estrita prazo>tardia>nenhuma, 18/20 seeds | Pending |
| ONT-30 | P1: Regressão — evento com causa nomeada no mesmo tick | Pending |
| ONT-31 | P1: Regressão — marco recente sem piso forte | Pending |
| ONT-32 | P1: Regressão — marco consolidado respeita piso | Pending |
| ONT-33 | P1: Regressão — auditoria de 10/100 anos sem queda sem causa | Pending |
| ONT-40 | P1: Predisposição — genes+exposição idênticos, byte-idêntico, 20/20 | Pending |
| ONT-41 | P1: Predisposição — genes diferentes, exposição idêntica, diverge, 20/20 | Pending |
| ONT-42 | P1: Predisposição — nenhum marco nasce acima do mínimo | Pending |
| ONT-50 | P1: Idioma — alvo é o de maior exposição do cuidador, não da etnia | Pending |
| ONT-51 | P1: Idioma — múltiplos cuidadores geram fluências distintas | Pending |
| ONT-52 | P1: Idioma — órfão de cuidador diferente, fluência maior no idioma do cuidador, 20/20 | Pending |
| ONT-60 | P1: Canal ambiental — crença/traço usa o mesmo canal já separado da Fase 7 | Pending |
| ONT-61 | P1: Canal ambiental — suíte de separação genético/ambiental da Fase 7 continua válida | Pending |
| ONT-70 | P1: Agregado — exposição média aproximada, sem custo de rotina completa | Pending |
| ONT-71 | P1: Agregado — reconciliação determinística na materialização | Pending |
| ONT-72 | P1: Agregado — custo não escala com toda criança materializada | Pending |
| ONT-80 | P1: Utility — ação sem marco excluída ANTES da pontuação | Pending |
| ONT-81 | P1: Utility — chorar compete normalmente, sem fallback forçado | Pending |
| ONT-82 | P1: Utility — custo de montar candidatos proporcional ao filtro, não ao catálogo | Pending |
| ONT-90 | P1: Fim — sistema para de avaliar após última janela fechar | Pending |
| ONT-91 | P1: Fim — custo por tick proporcional só aos NPCs em janela | Pending |
| ONT-92 | P1: Fim — trauma/doença pós-janela é sistema separado (Fase 22) | Pending |

**Coverage**: 30 total. Todas as decisões de Assumptions confirmadas — sem bloqueio pra Design.

## Success Criteria

- [ ] Ninguém age acima do próprio desenvolvimento: enumeração por reflexão + assert em 10 anos
      + par de mutação provando que a checagem é testada de verdade.
- [ ] Convívio é causal: negligência reduz marcos atingidos, 18/20 seeds.
- [ ] Janela crítica falseável nos dois lados: ordem estrita prazo>tardia>nenhuma, 18/20 seeds,
      tardia nunca alcança prazo.
- [ ] Idioma vem de quem cria: órfão de cuidador diferente, fluência maior no idioma do
      cuidador, 20/20 seeds, nenhuma fluência > 0 ao nascer.
- [ ] Regressão é datável e tem causa: toda queda com evento no mesmo tick, 10/100 anos sem
      exceção.
- [ ] Gene muda a taxa, exposição idêntica: byte-idêntico com genes iguais (20/20) e diverge com
      genes diferentes (20/20).
- [ ] Ontogenia entrou na conta: desligar o sistema muda o hash canônico em 10 anos.
- [ ] `dotnet test` completo sem regressão nas suítes `Population*`/`Family*`/`Behavior*`.
