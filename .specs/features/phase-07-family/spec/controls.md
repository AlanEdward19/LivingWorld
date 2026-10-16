### P2: Cenários de controle (deriva neutra e contrafactual de household)

**User Story**: Como quem verifica a fase, quero um cenário de deriva neutra (mesma demografia,
acasalamento aleatório, seleção desligada, mesma seed) e um cenário contrafactual de household
(mesmo genoma em household rico vs pobre), para que diversidade genética e "berço importa" sejam
provados por comparação, não por limiar mágico.

**Why P2**: Sem eles os testes estatísticos do roadmap (CV vs controle, contrafactual) não têm
comparador — mas o mundo funciona (casais formam, filhos nascem, herdam) sem esses cenários
existirem primeiro; eles são o instrumento de prova, não o mecanismo em si.

**Acceptance Criteria**:

1. WHEN o cenário de deriva neutra está ativo (flag `FamilyRules.NeutralDriftEnabled`, Assunção
   A11) THEN o sistema SHALL ignorar atração/cortejo (acasalamento aleatório entre elegíveis na
   janela de fertilidade) e a mortalidade SHALL ignorar `Vitality`/`Health` como fator de
   seleção (só idade/`LifeTable` base) — mesma demografia inicial, mesma seed do braço real.
2. WHEN o cenário contrafactual de household está ativo THEN o sistema SHALL permitir fixar
   `Vitality`/`RateGene` de um NPC semente e instanciá-lo em dois households com riqueza inicial
   diferente (rico/pobre, declarado no cenário de teste), demais condições fixadas.
3. WHEN qualquer um dos dois cenários acima está desativado (default) THEN o comportamento do
   mundo SHALL ser idêntico ao mundo sem a Fase 7 rodando esses cenários — ou seja, os cenários
   são aditivos ao harness de teste, nunca ao caminho default de produção.

**Independent Test**: Rodar o cenário default e o cenário de deriva neutra com a mesma seed,
comparar CV de `Vitality` nas duas populações finais (critério FAM-24); rodar o contrafactual
rico/pobre por 40 anos, 20 seeds, comparar medianas de patrimônio adulto (critério FAM-27).

---
