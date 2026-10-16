## User Stories

### P1: Relação assimétrica com eixos numéricos ⭐ MVP

**User Story**: Como designer do mundo, quero que dois NPCs acumulem confiança, afeto, respeito
e dívida de forma assimétrica (A→B ≠ B→A), formada por proximidade/convivência e alterada por
eventos nomeados, para que exista substrato numérico real sobre o qual cortejo e casamento
decidem — nunca uma flag "são amigos".

**Why P1**: Toda a fase depende disso: cortejo, casamento e reprodução (P1 seguintes) leem a
relação para decidir. Sem substrato, não há o que ler.

**Acceptance Criteria**:

1. WHEN dois NPCs nunca se encontraram THEN o sistema SHALL não ter nenhuma entrada de relação
   entre eles (par ausente, não par com valor 0 — "quem nunca se encontra nunca se conhece").
2. WHEN dois NPCs convivem (mesmo `Household` ou mesmo `Workplace`/`CellCoord` prolongado, regra
   declarada em `FamilyRules`) THEN o sistema SHALL criar (se ausente) e evoluir os 4 eixos
   (Confiança, Afeto, Respeito, Dívida) do par ordenado A→B, independentemente do par B→A.
3. WHEN um evento nomeado ocorre (ajuda, traição, convívio, comércio) THEN o sistema SHALL
   aplicar o delta declarado em `FamilyRules` ao eixo correspondente, na direção correta
   (ex.: traição de A contra B altera `A→B`, não necessariamente `B→A`).
4. WHEN um par de NPCs deixa de conviver por um período declarado em `FamilyRules` THEN os 4
   eixos da relação SHALL decair em direção a um valor neutro, nunca ultrapassando-o.
5. WHEN a relação de A→B é lida em qualquer sistema (cortejo, casamento) THEN o valor lido SHALL
   ser exclusivamente o do par ordenado correto — `A→B` e `B→A` SHALL poder divergir na mesma
   run (assimetria comprovável).

**Independent Test**: Cenário com 2 NPCs que convivem N dias, comparado a um par que nunca se
encontra (mesma seed) — o primeiro par tem relação não-nula, o segundo não tem entrada; um dos
dois NPCs sofre um evento de traição unilateral e só o eixo Confiança na direção da vítima cai.

---
