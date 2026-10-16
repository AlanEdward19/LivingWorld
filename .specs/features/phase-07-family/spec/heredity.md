### P1: Hereditariedade genético vs ambiental e seleção emergente

**User Story**: Como designer do mundo, quero que o filho herde atributos genéticos dos pais por
uma fórmula declarada com mutação semeada por `NpcId`, com um campo ambiental de origem
totalmente distinta, e que nenhuma função de aptidão artificial exista, para que "quem sobrevive
e se reproduz" defina o pool — não um score de bug.

**Why P1**: É o mecanismo que faz a Fase 7 valer o "objetivo técnico #1" (linhagens não-clonadas)
e cumpre o aviso de design de `genetics-and-family.md` ("genética não é destino").

**Acceptance Criteria**:

1. WHEN um `Npc` nasce de pais conhecidos THEN o sistema SHALL calcular `Vitality` (Assunção A1)
   por `vitalidadeFilho = vitalidadeMãe*pesoMãe + vitalidadePai*pesoPai + mutação`, com o stream
   de RNG da mutação semeado por chave que inclui o `NpcId` do filho (mesmo padrão de
   `RateGene.Inherit`/`Personality.RollFrom`).
2. WHEN o mesmo `Npc` nasce THEN o sistema SHALL calcular `Upbringing` (Assunção A1) a partir da
   riqueza do `Household` na concepção — origem **inteiramente ambiental**, nunca misturada nem
   derivada de `Vitality`/genes dos pais.
3. WHEN o snapshot do mundo é inspecionado THEN `Vitality` (genético) e `Upbringing` (ambiental)
   SHALL existir como campos distintos do `Npc`, nunca o mesmo campo com origem ambígua (mesmo
   critério de separação de `genetics-and-family.md`).
4. WHEN a mortalidade anual de um `Npc` é calculada THEN `Vitality` SHALL influenciar o
   multiplicador de saúde (junto com `Health`, sem substituí-lo) — mas o peso do canal ambiental
   (`Upbringing`/recursos do household) SHALL entrar na mesma ordem de grandeza em algum
   resultado de vida mensurável (patrimônio adulto, Assunção A2), nunca só o gene sozinho
   decidindo o resultado.
5. WHEN a população evolui por 100 anos THEN nenhuma função de "score de aptidão global" ou
   ranking de "melhor NPC" SHALL existir no código — sobrevivência e reprodução emergem só de
   mortalidade (`LifeTable`+`Vitality`+`Health`), fertilidade (`PopulationRules`) e sucesso de
   cortejo (relação+atração), nunca de uma função de fitness explícita somando os três.

**Independent Test**: Harness de N nascimentos com `Vitality`/`Upbringing` dos pais e do filho
registrados — verificar que `Vitality(filho)` correlaciona com `Vitality(pais)` (herdado) e que
`Upbringing(filho)` não correlaciona com `Vitality` dos pais (canais independentes); grep no
código por qualquer campo/método chamado "fitness"/"aptidão"/"score global" SHALL retornar vazio
fora de comentário/doc.

---
