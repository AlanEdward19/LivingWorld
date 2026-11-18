# docs/decisions-log-archive-2.md — decisões de processo antigas (AD-094 em diante)

Arquivo do `decisions-log.md` (teto de 100 linhas), continuação de [decisions-log-archive.md](decisions-log-archive.md). IDs e textos preservados.

| ID | Data | Decisão | Motivo |
|---|---|---|---|
| AD-094 | 2026-10-12 | `estate-inheritance` (spec `.specs/features/estate-inheritance/`, light): na morte a carteira passa ao cônjuge vivo, senão filhos (partes iguais, resto ao menor id), pais, irmãos, senão ao tesouro da cidade; a despensa de domicílio que fica vazio vai ao domicílio do primeiro herdeiro; `Workplace.Owner` (novo, só por autoria ou herança) passa ao herdeiro de menor id presente. Evento `EstateSettled` (capacidade DEATH), relação `Inheritance`. Dinheiro de morto não fica mais congelado: pins, ouro, colapso e testes de conservação (agora contam o tesouro das cidades) regravados. Alocação +0,01%, µs dentro do ruído. | O mundo real herda; antes a moeda do morto saía de circulação e a conservação só valia porque ela ficava parada. |
