### P1: Atração, cortejo e rejeição nomeada

**User Story**: Como designer do mundo, quero que NPCs elegíveis calculem um score de atração e
entrem em cortejo por um tempo declarado, podendo ser rejeitados com um motivo nomeado
(`Incesto`, `ForaDaFaixaEtária`, `SemAfinidade`), para que a formação de casal seja auditável e
não uma sorte silenciosa.

**Why P1**: É o portão entre "duas pessoas se conhecem" (história anterior) e "casam e têm
filhos" (histórias seguintes) — sem ele, casamento seria arranjo aleatório, o mesmo problema que
a Fase 7 existe para resolver.

**Acceptance Criteria**:

1. WHEN um score de atração é calculado entre dois NPCs elegíveis (vivos, sexo/idade compatíveis
   com reprodução, sem cônjuge — Assunção A9) THEN o sistema SHALL combinar idade, saúde, status
   (profissão/riqueza), habilidade, afinidade cultural e a relação já existente (A3), com pesos
   declarados em `FamilyRules` (A4).
2. WHEN o score de atração entre dois candidatos elegíveis excede o limiar declarado em
   `FamilyRules` THEN o sistema SHALL iniciar cortejo, com duração declarada (não instantâneo).
3. WHEN o cortejo é avaliado entre dois candidatos que são parentes de primeiro grau (Assunção
   A6) THEN o sistema SHALL rejeitar com motivo `Incesto`, **mesmo que score de atração e todo o
   resto seja compatível**.
4. WHEN o cortejo é avaliado entre dois candidatos fora da janela de fertilidade compatível
   (idade abaixo/acima do declarado em `PopulationRules`) THEN o sistema SHALL rejeitar com
   motivo `ForaDaFaixaEtária`.
5. WHEN o cortejo é avaliado entre dois candidatos sem afinidade suficiente (score de atração
   abaixo do limiar após excluir os dois casos acima) THEN o sistema SHALL rejeitar com motivo
   `SemAfinidade`.
6. WHEN o cortejo é bem-sucedido (passa por todos os checks e completa a duração declarada)
   THEN o sistema SHALL registrar o resultado (evento nomeado, mesmo padrão de
   `WorldEventKind.Birth`) antes de acionar o casamento (próxima história).

**Independent Test**: (a) Negativo — cenário de 10 anos sem nenhum casamento entre parentes de
primeiro grau (household comum não gera irmãos coabitando compatíveis o suficiente para tentar).
(b) Positivo — cenário dedicado com dois irmãos adultos coabitando, compatíveis em tudo o mais
(mesma idade, saúde, cultura, sem outro candidato competindo) — o cortejo entre eles SHALL ser
rejeitado com motivo `Incesto` (critério do roadmap: "só o negativo passa também se irmãos nunca
se encontrarem" — este teste prova que o mecanismo existe, não que ele nunca é exercitado).

---
