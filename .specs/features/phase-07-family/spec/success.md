## Success Criteria

- [ ] Casais formam por atração/cortejo real (não fila de geração), com rejeição nomeada
      auditável nos 3 motivos declarados.
- [ ] Casamento produz household novo; reprodução agenda nascimento no scheduler; toda criança
      tem `PaiId`/`MãeId` válidos e vivos na concepção, zero órfãos de referência.
- [ ] `Vitality` (genético) e `Upbringing` (ambiental) existem como campos distintos, com
      hereditariedade e mutação semeada por `NpcId` do filho.
- [ ] FAM-32: contraste de CV de Vitality dentro da faixa medida em 20 seeds de
      `tests/baselines/family-diversity.json`, controle negativo de Openness compatível com zero,
      pisos positivos de CV/população nos checkpoints de 100 anos do cenário completo;
      seleção amplificada deve reprovar (ADR-0036, substitui AD-066/088 para este critério).
- [ ] Correlação genética×sucesso tem IC95 do `\|r\|` inteiramente abaixo do mesmo mundo com o
      canal ambiental desligado — teto derivado, não inventado.
- [ ] Contrafactual de household (rico vs pobre, mesmo genoma) mostra medianas diferentes e
      overlap ≥ o overlap entre genomas extremos no mesmo household.
- [ ] Desligar hereditariedade + formação de casais por flag muda `Hash(world)` em 10 anos —
      Fase 7 "entrou na conta".
- [ ] `bash scripts/verify.sh` limpo (build + lint + test em 0) com a Fase 7 integrada em
      `ScenarioRunner.DefaultSystems()`.
