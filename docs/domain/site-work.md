# Obra com turma e carga que pesa (AD-179)

Toda obra é um lugar de trabalho de verdade: anda só pelas horas de gente que está nela. O motor é a fonte da verdade.

## Quem trabalha na obra (`Cities/Streets/SiteWork`)
- **Obras com turma**: o trecho de calçamento (`PavingWork`, AD-176) e, com `StreetRules.BuildNeedsCrew`, a casa ou o prédio da
  fila da cidade (`ConstructionProject`, a obra da vez). A regra da fila vem **desligada**: em `default.json` (pleno emprego)
  ninguém sem emprego aceita e a cidade parava de construir (6 testes de construção); falta a família e os vizinhos construírem.
- **Obra sem mão**: sem nenhuma hora de turma por `SiteStaleMonths` (6), o trecho é desfeito (o material comprado fica gasto);
  na `vila-pequena` (todo adulto lavra) a 1ª obra ficou aberta 15 anos sem hora até isto existir.
- **Aceitar é decisão do NPC pela utilidade**: o termo `SiteWork` da utilidade de trabalhar (grupo Pressão, pressão
  `EarnIncome`; aparece no rastro e na leitura do cérebro) = `SiteOfferMax` × necessidade × parte do dia que sobra depois da ida e
  volta pela rota. Necessidade = o maior entre o que falta de moeda na casa para `SiteReserveDays` dias de jornal e o que falta
  na despensa. Zero para quem tem emprego (não troca o que tem), não é adulto ou mora noutra cidade. Entra quem tem oferta
  ≥ `SiteJoinMin`, quem mais precisa primeiro, até `CrewMax`; sai abaixo da metade (arrumou emprego, encheu a casa, morreu).
- **Agenda**: o destino de `Work` do diarista é a obra; vai e volta pela rota nas horas da rotina (como o empregado vai à
  oficina), sai mais cedo pela distância, cansa e sente fome; enquanto trabalha lá não faz outra coisa.
- **Andamento**: ao fim da jornada (ou quando larga no meio), as horas em que ele esteve NA obra contam (teto `CrewHoursPerDay`)
  e são pagas na hora (jornal do dia ÷ horas da jornada) por quem paga (casas de frente ou tesouro, no trecho; tesouro, no
  prédio). Sem dinheiro, ele sai sem receber e a hora não conta. Sem gente, a obra para; com mais braço anda mais rápido, até o
  teto. A força (poder) multiplica as horas de obra de quem a tem.
- **Prédio da fila**: a obra da vez escolhe o lote (onde a turma trabalha); as primeiras horas vão a carregar o material do
  estoque da cidade (o largo) até o lote (`BuildUnitsPerLoad` unidades por viagem, ida carregada); cada hora seguinte é um tick da
  receita (com o insumo de sempre). Pronto, o prédio sobe pelo lote da regra de sempre.
- Eventos: `SiteWorkJoined` (npc|obra|oferta×100), `SiteWorkLeft` (npc|obra|motivo). Snapshot: turma, horas e pago na obra.

## Carga a pé (item 175, `Mobility.LoadFactor`)
O passo alonga 1 + kg × `CarryPmPerKg`/1000 (5‰/kg) até `CarryMaxKg` (40 kg; +20%). Carga = o que a pessoa leva
(`InventionLedger.Carried`: comida e água da viagem, espólio) + o recurso nas mãos. A chegada agendada já conta a carga. O material
de obra pesa `SiteLoadKg` (25 kg) na ida da fonte ao trecho ou ao lote. Veículo por superfície: só o que a AD-176 já faz (o veículo
de terra cabe ou não na rota); a carga dele não muda o passo — não entregue.

## Toda obra do motor exige gente presente?
| Obra | Antes | Agora |
|---|---|---|
| Calçamento e conserto (`PavingWorksSystem`) | pagava diaristas sem irem | turma no trecho |
| Casa e prédio da fila (`ConstructionSystem`) | 1 tick/dia sozinho | turma no lote, opcional (desligada por padrão) |
| Casa de quem casa ou sai de casa (`MarriageSystem`, `UnionDissolution`) | nenhuma obra: mora na célula | não entregue |
| Oficina de quem empreende (`VentureFounding`, `BuildPersonDays`) | dias-homem pagos, sem ida | não entregue |
| Materialização (`MaterializationSystem`), pool agregado | aparece | não é obra (nível de detalhe) |
| Protótipo de invenção e de técnica (`DesignWorkshop`, `TechDiscovery`) | prazo sozinho | não entregue |
| Produção da oficina (`ProductionSystem`) | já exige presença (AD-149) | igual |

## Fontes e confiança
- Diarista e obra pública no tempo morto da lavoura (Dyer, *Standards of Living in the Later Middle Ages*, 1989) — média.
- Carga × passo: com carga o passo preferido cai e o custo sobe (Pandolf, Givoni e Goldman, *J. Appl. Physiol.* 43, 1977;
  Knapik, Reynolds e Harman, *Mil. Med.* 169, 2004); o 0,5%/kg e o teto de 40 kg são escolha dentro disso — baixa.
- Escolhidos sem fonte: `SiteOfferMax` 30 (a escala de `IncomeNeedMax`), `SiteJoinMin` 3, `SiteReserveDays` 30, 25 kg por viagem,
  2 unidades por viagem de material de prédio.

## Medir
`MenuWorldsReport` (`LW_MENU_WORLDS_OUT`) imprime, por linha, a turma e as obras (`obra[...]`).
