# Carreiras e contas da casa como dado (#263, épico #246)

Emprego, turno, desempenho, promoção, salário e as contas da casa: **nada disto está no código**. O motor só conhece o mecanismo; cargo, nível, horário, salário,
o que pesa no desempenho e o valor de cada conta são o catálogo `scenarios/catalog/careers.json` (embutido; o período o estende com
`CatalogExtends: ["careers"]`) e o período `sandbox-casal.json`. Decisão: AD-229. Sandbox: [sandbox.md](sandbox.md).

## Catálogo (`Careers`)
- `rules`: `weekDays`, `dayNames` (só rótulo), `noticeLeadHours` (aviso antes de sair), `graceHours` (tolerância depois do começo), `absencesToFire`, `billHour`,
  `searchValidHours` (validade da busca), `idleSearchPerDay` (chance de quem ninguém conduz procurar vaga), `noticeKeep`.
- `careers[]`: `employerType` (o `LocationType` do empregador), `commuteMin` (ida e volta, fora do mapa), `skillId` (habilidade que pesa e cresce com o trabalho),
  `performance {needs:{id:peso}, skill, practice}`, `search {baseChance, perSkill}` e `levels[]` com cargo, `wagePerHour`, `startHour`, `hours`, `days`, `minSkill`,
  `promoteAt`/`promoteAfter`, `demoteBelow`/`demoteAfter` e `awayEffects` (o que o turno dá ao corpo por hora: refeição, café, cansaço).
- `bills[]`: `fixed`, `everyDays`, `dueDays`, uso medido (`meterUses`: ids de uso ou toggle do catálogo de objetos, `perHour`), `lateFeeRate`, `cutAfterDays` e `blocks`
  (os usos que o corte desliga). A luz mede as lâmpadas ligadas (`light:on`, amostra horária); a água, as horas de chuveiro, pia e vaso.
- Objeto `newspaper` (jornal de vagas) com o uso `search-job` (`toggle: "job:search"`): ao terminar, lista as vagas reais. O período o põe na mobília.

## O mundo (`world.Careers`, `CareerLedger`)
Livro esparso (nulo sem carreira: fora do snapshot e do hash; mundos sem o catálogo não mudam). `Jobs` (carreira, nível, empregador = um `Workplace` com caixa, sequência
boa e má, faltas, turno em curso), `Bills` (por casa e conta: uso medido, valor aberto, multa, vencimento, corte), `Notices` (avisos numerados para a tela) e `Searches`.
`CareerStart` no período põe quem já começa empregado (`{Member, Career, Level, Employer}`; `Employer` é o índice em `Workplaces[]`).

## O turno (`CareerSystem`, de hora em hora, + evento agendado da volta)
1. **Aviso**: `noticeLeadHours` antes de sair, quem o jogador conduz recebe "Hora do trabalho em 1 h" (verbo `go`).
2. **Saída**: NPC sai sozinho na hora (`start − ceil(commute)`); o jogador escolhe "Ir ao trabalho" (oferta `job`/`go`, na janela do turno). O corpo para o que fazia,
   sai da casa e **some do mapa** (`IsAway`: fora do interior, da projeção da cidade, das ofertas e da rotina); o motor simula o turno. Chegar depois do começo é atraso (desconta horas e desempenho).
3. **Volta** (evento no instante exato): o corpo reaparece na soleira, o turno dá o `awayEffects`, a habilidade pratica, o desempenho conta e o empregador paga.
4. **Falta**: sem sair até `start + graceHours`, sem pagamento, desempenho 0 (turno ruim) e `absencesToFire` seguidas demitem.
5. **Desempenho** (0–100) = média ponderada das necessidades e da habilidade, medida na saída; **promoção** com `promoteAfter` turnos ≥ `promoteAt` (e `minSkill` do nível seguinte),
   **rebaixamento** com `demoteAfter` abaixo de `demoteBelow`; no primeiro nível, demissão.
6. Corpo no trabalho não responde (409 "você está no trabalho até HH:MM"); o cliente passa a conduzir a outra pessoa da casa.

## Dinheiro (moeda conservada)
Salário = `wagePerHour × horas trabalhadas`, debitado do `Treasury` do empregador, com a mesma retenção de `EconomyRules.WageTaxRate` da folha (vai ao tesouro da cidade) e o líquido
na carteira; caixa curto paga o que há, loga `WageUnpaid` e avisa. Conta paga sai das carteiras da casa (a de quem paga primeiro) para o tesouro da cidade. Nunca nasce moeda
(`Careers263Tests`). O `WagePaymentSystem` mensal e `Npc.Employer` não são tocados: quem tem carreira no livro não entra na folha de ofício.

## Procurar emprego
Usar o jornal termina com a lista de vagas reais (empregador do tipo da carreira, vaga livre no livro, caixa para o turno, habilidade mínima). Candidatar-se (oferta `job`,
verbo `apply:<carreira>@<empregador>`) sorteia pelo RNG semeado com `baseChance + perSkill × habilidade`; contratado entra no nível 0 e abre as contas da casa. A busca vale
`searchValidHours` e é gasta ao candidatar-se. NPC desempregado de casa de carreira olha as vagas sozinho (`idleSearchPerDay`).

## Contas
À hora `billHour`: emite a do período (fixo + uso medido), avisa um dia antes, cobra multa uma vez no vencimento e **corta** o serviço depois de `cutAfterDays` (o uso
recusa: "Água cortada: conta em atraso"). Casa sem jogador paga sozinha antes do vencimento, se pode; a do jogador paga em "Contas" (E em casa), que religa o serviço.

## Contrato e cliente (só aditivo)
Ficha do NPC: `career {job, bills, notices, listings, dayNames}` (omitido sem carreira). Ofertas `job` e `bills` com `uses` (texto do dado) e verbos `go`, `apply:…`, `pay:…`.
Cliente: linha do emprego e da conta no cartão, painel **O** (`career_panel.gd`), avisos novos uma vez, passo de captura `career` e `careerpanel`.

## Custo
Sem carreira no mundo o sistema sai na primeira linha (`world.Careers` nulo) e o corte da rotina é uma checagem de nulo: medido em 400 pessoas, 20 dias, melhor de 5 rodadas, 0,81 µs/NPC-tick sem o `CareerSystem` na lista e 0,74 com ele (dentro do ruído). Com carreiras só quem está no livro paga o turno (por hora, O(empregos)).

## Fora desta fatia
Emprego com o corpo andando até um prédio do bairro (hoje o corpo sai do mapa e o empregador é um prédio do bairro sem o corpo dentro); carpool e ir a pé; o humor e os
momentos (#262) no desempenho (hoje só necessidades e habilidade); aluguel a um locador; entrega de contas pelo correio.

## #267: turno sem armadilha
`CareerWork.NoticeOnTakeover` (ao assumir o corpo na janela do turno sem o aviso dela no livro), `Careers.rules.stopKinds` (ficha `career.stopKinds`) e `career.mates` (quem mais da casa tem emprego: `away`, `returnMs`, `returnText`).
A volta (`Return`) põe o corpo conduzido logo depois da porta (`EnterHome`). `POST /players/{id}/accelerate` com `{untilReturn:true}` salta até a volta do primeiro da casa (409 se ninguém está fora). Ver [sandbox.md](sandbox.md).

## #268: emprego guiado
Ler o jornal lista as vagas (cargo, empregador, salário) e o cliente as abre na roda com "Candidatar-se" (`career.listings`, com `commuteMin`). `CareerWork.BeginApply`: com `Careers.rules.applyAtEmployer` e o corpo longe do empregador
(`applyReachM`), o motor põe o corpo a caminho (o mesmo `PlayerGo` do "ir até", candidatura guardada em `JobSearched.PendingCareer`) e `OnArrived` a corre na chegada. Fora do horário de atendimento (`applyOpenHour` a
`applyCloseHour`) vira entrevista marcada para a próxima abertura (`later`, aviso `interview`); dentro, o sorteio decide: contratado ou recusado com a chance dita. Desfechos da interação: `done`, `refused`, `going`, `later`.
- **Entrevista que acontece (#246, AD-237)**: a candidatura fora do atendimento guarda a vaga (`JobSearched.PendingCareer/Employer`) e a hora (`InterviewAtMs`); `CareerSystem` a roda na abertura (`CareerWork.RunInterview`): com o corpo no empregador decide (contratado ou recusado com a chance), longe perde a vez e o aviso diz.
## #269: rótulos honestos (AD-238)
A ida do jornal e do turno vem da rota real da soleira de casa à do empregador, no passo do corpo (`CareerWork.CommuteMsOf`); `commuteMin` do cargo só vale sem rota. O aviso `interview` sai do livro quando a entrevista tem desfecho (`hired` ou `rejected`).
"Pagar" as contas já é verbo da roda (`pay:<conta>`, oferta `bills`). A medida de ambiente do humor (`Cômodo feio`) só existe em moradia (`RoomBeauty.Ambient`).
