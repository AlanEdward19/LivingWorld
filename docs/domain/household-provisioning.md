# Como a casa se abastece (AD-148)

A casa compra comida e água no mercado da vila (a venda da fazenda). Como numa aldeia de verdade, quem tem como pagar repõe
a despensa para alguns dias, uma pessoa vai por todos, a compra espera o fim do turno quando a despensa dura até lá, e quem não
tem moeda não fica indo à banca. Todos os números em `ProvisioningRules` (`NeedsRules.Provisioning`);
`ProvisioningRules.Legacy` volta ao jeito antigo (só para medir: `LW_PROVISION=legacy` nos relatórios).

## Quando alguém sai para comprar
- **Despensa baixa** (`NeedsRestocking`): abaixo de metade da meta de dias (2 dias + ida e volta ao mercado), quem tem moeda
  vai repor. Só vai pelo que pode pagar (a compra completa a meta; com a comida cara e a água cheia, não vai).
- **No turno** (`RestockDuringWorkBelowDays`, 0,5): no horário de trabalho a compra fica para depois, a menos que a despensa
  tenha menos de meio dia por morador.
- **Uma pessoa por casa** (`OneShopperPerHousehold`): com alguém da casa no mercado ou a caminho, os outros não vão; só a
  própria fome ou sede crítica (déficit acima de 80) não espera.
- **Sem moeda para a comida** (`OnlyIfCanPay`, backlog 08 item 46): a fome não puxa ao mercado quem não paga uma unidade; a
  água sem moeda continua saindo do poço da aldeia. Quem tem fome e nada em casa trabalha, recebe da família ou da esmola.
- **Banca vazia** (`FoodStallRetryHours` 6, até 48): a casa que achou a banca sem comida espera 6 h, e o dobro a cada ida vazia
  seguida (a venda é reposta uma vez por dia, do celeiro); zera na primeira compra de comida (`Household.FoodStallRetryTick`).

## O que se compra
- A compra **completa a meta** (`TopUp`): o que falta para os dias da meta, não a meta inteira por cima do que já há. Antes,
  cada ida trazia de novo a água da meta: na seed 7 as casas chegavam a 7.900 de água por morador no ano 52, pagas com o
  dinheiro da comida.
- A banca com menos que a compra **vende o que tem** (`BuyWhatIsThere`); antes a transação falhava inteira.
- Limitada à carteira de quem foi, ao estoque da banca e ao que se carrega (AD-143/147).

## Trabalhar pede usar ferramenta
`OntogenyDefaults.HumanRequirements[Work]` passou de mão hábil completa (motor fino 1,0) a uso de ferramenta (0,7). Com 1,0,
o adulto que perdeu a janela da mão hábil na infância (teto abaixo de 1 pela doença ou falta de cuidado) nunca trabalhava:
55% dos adultos da seed 7 aos 30 anos, contratados e parados no turno. Era a raiz da fila: a fazenda só produzia porque o
mercado fica nela e a fila contava como presença (backlog 04, E1).

## Medido (sonda `provision-trace <seed> <ano-inicial> <ano-final>`, seed 7, anos 30–50)
| | antes | depois |
|---|---|---|
| lavradores às 10 h trabalhando / comprando | 17% → 4% / 1% → 35% | 51% / 2% |
| horas por adulto por dia: trabalho / compra | 1,2 / 3,9 | 7,0 / 0,3 |
| banca vazia (horas), preço do trigo | até 66%, 15–18 | 0–4%, 1 |
| casas sem comida | até 77% | 1–3% |

A sonda imprime por ano a despensa em dias de consumo, a banca, as horas por ação, a decisão na hora do trabalho (urgente,
reposição, humor, desenvolvimento, doença) e o motivo de cada compra cortada (`PurchaseTally`, só leitura).

## Ligado junto (AD-148)
- `FamilyRules.Pressure = Measured` no cenário de código (freios preventivos do AD-145, [population-pressure.md](population-pressure.md)).
- `PsycheRules.CriticalBodyOverridesLeisure` (backlog 08 item 47).
- Desligados, com o número que impediu: `EconomyRules.PresenceNeedsWork` (E1: fome 3,5% → 9,2%, pede recalibrar a lavoura)
  e `InventionRules.PantryNeedsPresence` (carga pessoal: fome 3,5% → 5,7%).

## Ver também
- [behavior.md](behavior.md) — a decisão que leva ao mercado
- [economy.md](economy.md) — preço, venda da fazenda e celeiro
