# Fase 23 — Intriga

**Objetivo**: intriga não é subsistema novo — é a camada de crença da Fase 10 e a memória
social da Fase 7 usadas **contra as pessoas**. Segredo é fato com propagação restrita;
chantagem é usar o segredo de outro; traição é agir contra um vínculo de confiança por
ganho; e a briga sai do primitivo do ADR-0011, não de um roteiro. Ninguém escreve um enredo:
o mundo passa a produzir rixa, escândalo e conspiração como subproduto do que já existe.

> **Spec, não gate.** Os critérios abaixo são a intenção; os critérios finais são escritos
> sob `rules/eval-criteria.md` quando a fase for ativada. Não comece esta fase antes da
> Fase 10 fechar.

## Tasks
1. **Segredo = fato do esqueleto (Fase 10) com propagação de crença restrita**: dono,
   cúmplices, conjunto de quem sabe e risco de vazamento por salto de transmissão. Nenhuma
   tabela paralela de segredos — é atributo de propagação, não entidade nova.
2. **Motivo e oportunidade** como pré-condição de toda ação hostil: motivo (necessidade,
   rancor, ganho, ordem de facção) **e** oportunidade (proximidade, acesso, ausência de
   testemunha). Faltando um, a ação nem entra no conjunto candidato da utility.
3. **Chantagem** como modificador de negociação a partir de segredo alheio. Exige que o
   chantagista **acredite** no segredo — consulta de crença, nunca de verdade.
4. **Traição**: agir contra vínculo com confiança acima do limiar do cenário, por ganho
   mensurável. O efeito é o colapso do eixo de confiança da Fase 7 mais memória episódica de
   alta importância em quem viu — não um flag `traidor`.
5. **Pilha de humor**: modificadores transitórios com fonte, magnitude e decaimento
   declarados, somados numa pilha inspecionável que entra como peso na utility da Fase 4.
   Referência de mecânica: a pilha de pensamentos do RimWorld. Humor é derivado, nunca campo
   escrito à mão.
6. **Rancor de longo prazo**: memória episódica negativa que sobrevive à compactação virando
   crença sobre uma pessoa ou linhagem. Decai, **prescreve** no prazo do cenário e reacende
   com evento novo do mesmo alvo — a rixa cuja origem ninguém lembra direito, à moda do
   Dwarf Fortress, já sai de graça da distorção da Fase 10.
7. **Briga e violência pelo primitivo do ADR-0011**, perfil `Dramático`, com **sucesso
   parcial** de primeira classe: "você venceu, mas alguém viu" é o que alimenta a próxima
   intriga.
8. **Fofoca como transmissão enviesada**: os operadores de distorção da Fase 10, com
   probabilidade modulada pelos eixos de relação de quem conta com o alvo e com o ouvinte.
   Inimigo infla, aliado omite.
9. **Reputação por comunidade**: agregado das crenças daquela comunidade sobre um NPC,
   distinto da verdade e do que cada indivíduo acredita. Duas comunidades, dois retratos.
10. **Facção e conspiração**: organização com objetivo oculto (segredo de múltiplos donos),
    recrutamento por afinidade e rancor comum, e exposição pública como evento com
    consequência.
11. **Persona do portador de potência** (Fase 16): `PersonaDescriptor` (dono = `NpcId` único,
    nomes por cultura) é verdade; associação persona↔dono é **crença por observador**
    (`IdentityAttributionBelief`: observador, candidato, confiança, evidências) — nunca
    `bool SecretIdentityKnown` global. Falsa atribuição funciona igual à verdadeira. Ver o
    efeito não é ver o dono: associação nasce de evidência (rosto, voz, assinatura
    observável, testemunho) e a exposição é gradual (testemunha → grupo → rumor →
    comunidade), nunca um booleano que liga de uma vez.
12. **Publicar é ação de agente, não renderização**: jornalista forma crença, avalia risco/
    interesse/ganho, decide ignorar/investigar/publicar/suprimir — produz `PublicationEvent`
    que a Fase 12 só transforma em texto depois. Apelido de imprensa é rótulo negociado (o
    portador aceita ou rejeita). Reação ao extraordinário (medo, culto, fascínio) é a mesma
    reputação-por-comunidade da task 9, aplicada a portadores — nunca `if target.HasPower`.

## Critérios de verificação
- **Ninguém sabe sem caminho**: reflexão sobre **todo** acesso a crença reprova se algum
  caminho resolver para o fato direto **ou** ficar sem cobertura; assert a cada tick em 10
  anos de que todo NPC que conhece um segredo tem cadeia de transmissão até o dono. Par de
  mutação igual à Fase 10: desligar a checagem por flag tem de **fazer isto falhar**.
- **Segredo causa traição**: par base/tratamento, densidade de segredos maior no tratamento.
  Taxa de traição maior em **18/20 seeds**. Direção, não magnitude.
- **O chantagista acredita no que usa**: a cada tick em 10 anos, toda chantagem tem o
  segredo nas crenças do chantagista naquele tick. Segredo desconhecido reprova.
- **Rancor decai, prescreve, só reacende por evento**: sem evento novo do alvo,
  `rancor(t+1) ≤ rancor(t)` a cada tick, chegando a zero no prazo do cenário.
- **Humor entrou na conta e diversificou a ação**: desligar a pilha por flag muda o hash
  canônico em 10 anos **e** reduz ações distintas escolhidas, par na seed, em 18/20 seeds.
- **Reputação não é verdade**: existe ≥1 NPC cuja reputação diverge entre duas comunidades
  **e** ambas divergem do fato, cada uma agindo coerente com sua versão.

## Fora do escopo
Prosa de escândalo e crônica: Fase 12. Guerra entre estados: Fase 10/`society.md`. Segredo de
culto: Fase 17. Orientação/divulgação: Fase 22 (só consumidos). Combate tático: fora do
projeto, a briga resolve numa rolagem do ADR-0011. Ferimento localizado e recuperação **é**
parte do projeto, saída da rolagem, fase própria de saúde/corpo.

## Questões em aberto
- Segredo colide com o **cânone limitado** da Fase 10: despejado, deixa de existir ou vira
  fato irrevelável, matando a chantagem junto?
- Reputação por comunidade é recalculada por tick (caro) ou estado mantido (quarta camada de
  crença)? Não dá pros dois.
- "Ausência de testemunha" exige saber quem vê quem por tick — cabe no LOD agregado da Fase
  8, ou intriga só existe em região materializada?
- Rancor que prescreve contradiz rixa de linhagem multi-geracional: prescrição por indivíduo
  com prazo de linhagem à parte, ou linhagem tem prazo próprio?
- Onde a pilha de humor entra sem virar fator dominante na utility da Fase 4, e que sensor
  barato avisa quando virou?

## Ver também
[memory.md](../domain/memory.md) · [historical-memory.md](../domain/historical-memory.md) ·
[genetics-and-family.md](../domain/genetics-and-family.md) ·
[behavior.md](../domain/behavior.md) · [society.md](../domain/society.md) ·
[powers.md](../domain/powers.md) · [phase-16-powers.md](phase-16-powers.md) ·
[phase-12-narrative.md](phase-12-narrative.md) ·
[ADR-0011](../adr/ADR-0011-primitivo-unico-de-resolucao.md) ·
[ADR-0007](../adr/ADR-0007-memoria-historica-degradavel.md) ·
[ADR-0010](../adr/ADR-0010-potencia-como-modificador-unificado.md) ·
[rules/eval-criteria.md](../../rules/eval-criteria.md)
