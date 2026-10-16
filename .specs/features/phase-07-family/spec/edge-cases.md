## Edge Cases

- WHEN um NPC nunca encontra nenhum candidato elegível na vida THEN ele SHALL permanecer sem
  cônjuge e sem filhos, sem exceção nem log de erro (mesmo espírito do NPC sem-teto, AD-036).
- WHEN o único candidato de cortejo disponível é parente de primeiro grau THEN o cortejo SHALL
  ser rejeitado (`Incesto`) e o NPC SHALL permanecer disponível para cortejo com outro candidato
  em anos seguintes (a rejeição não bloqueia cortejos futuros com terceiros).
- WHEN um cônjuge morre antes de qualquer concepção THEN o household do casal SHALL dissolver
  (Assunção A8) e o sobrevivente SHALL poder entrar em novo cortejo (Assunção A9 — viuvez não é
  bloqueio permanente).
- WHEN a mãe morre no parto (Assunção A10) THEN a criança (se sobreviver) SHALL nascer com
  `FatherId` apontando ao pai se vivo, `MotherId` apontando à mãe mesmo morta (mesmo padrão já
  existente: referência histórica válida, não ponteiro solto — `AD-031`).
- WHEN dois NPCs se encontram mas nunca satisfazem o limiar de convivência declarado (encontro
  único, não repetido) THEN a relação SHALL permanecer ausente ou nos eixos iniciais mínimos,
  nunca pular direto para valores altos.
- WHEN `Vitality`/`Upbringing` são calculados para a população seed inicial (sem pais conhecidos)
  THEN o sistema SHALL sortear valores iniciais (mesmo padrão de `RateGene.RollInitial`), nunca
  lançar exceção por ausência de pai/mãe.

---
