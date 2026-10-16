### P1: Casamento, household e reprodução agendada

**User Story**: Como designer do mundo, quero que um cortejo bem-sucedido case o casal em um
household novo, e que a reprodução dependa de janela de fertilidade, saúde, qualidade da relação
e recursos do household — agendando o nascimento no scheduler em vez de varrer por tick — para
que família seja uma consequência determinística e rastreável, não um evento instantâneo.

**Why P1**: É o mecanismo central do objetivo #1 — sem casamento real e reprodução ligada à
relação, não existem linhagens não-clonadas para rastrear.

**Acceptance Criteria**:

1. WHEN um cortejo é bem-sucedido THEN o sistema SHALL criar um `Household` novo (Assunção A7),
   remover ambos os cônjuges de seus households anteriores e adicioná-los ao novo, com estoque
   inicial próprio (declarado em `FamilyRules`, mesmo espírito de AD-046).
2. WHEN um casal casado avalia concepção (frequência `Yearly`, mesmo padrão de
   `NatalitySystem` atual) THEN o sistema SHALL exigir: ambos vivos, mulher dentro da janela de
   fertilidade (`PopulationRules.IsFertileAge`), saúde de ambos acima de um piso declarado,
   qualidade da relação (eixos A3) acima de um limiar declarado, e recursos do household
   (`Household.Stock`) acima de um piso declarado — faltando qualquer um, concepção não ocorre
   neste ano.
3. WHEN a concepção é bem-sucedida (rolagem determinística sobre `AnnualConceptionChance`
   modulada pelos fatores acima) THEN o sistema SHALL **agendar** o nascimento via
   `ctx.ScheduleEvent` na data de parto (gestação, mesmo mecanismo já existente em
   `NatalitySystem`) — nunca produzir o filho na hora nem varrer households por tick à procura
   de gestantes.
4. WHEN a gestação está em curso e a mãe morre antes do parto THEN o evento de nascimento
   SHALL ser tratado como falha silenciosa (sem exceção) — nenhum filho é criado (mesmo
   comportamento já existente: `mother is not { IsAlive: true }` no handler).
5. WHEN o evento de nascimento agendado dispara THEN o sistema SHALL rolar o risco de parto
   (Assunção A10): com probabilidade `MaternalDeathRisk`, a mãe morre no parto; com
   probabilidade `InfantDeathRisk`, a criança nasce morta (ambas as rolagens independentes,
   streams de RNG próprios por evento).
6. WHEN ambos os pais de um `Household` morrem THEN o sistema SHALL dissolver o household e
   redistribuir os membros remanescentes conforme Assunção A8.

**Independent Test**: Cenário com um casal casado, household com estoque suficiente e relação
alta — nascimento ocorre dentro da janela esperada, agendado (visível como `ScheduledEvent`
pendente antes do parto, não como filho já existente no tick da concepção); braço espelho com
household sem estoque — nenhuma concepção ocorre apesar de mesma relação/idade/saúde.

---
