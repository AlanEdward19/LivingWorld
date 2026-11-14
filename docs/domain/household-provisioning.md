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
- AD-149, com a lavoura da terra finita ([land-and-harvest.md](land-and-harvest.md)): **ligado** `EconomyRules.PresenceNeedsWork`
  (E1: só lavra quem está trabalhando).
- AD-151: a carga pessoal (`PantryNeedsPresence`, [invention.md](invention.md)) **segue desligada**: com a refeição paga, 20×100
  deu fome 4,5% (teto no salário) e 5,3% (excesso em espécie), base 4,1%; sem refeição nenhuma, 6,9% e uma seed com 3 vivos.
  A diferença de seeds 1–20 para 21–40 (abaixo) mostra que o ruído de um bloco de 20 seeds é dessa ordem.

## A refeição do empregador (AD-151)
Longe de casa o lavrador comia do estoque à venda da fazenda sem ninguém pagar (a "ração da oficina": sonda `provision-trace`,
seed 4, anos 20–35, com a carga ligada: 4.700–6.900 rações por ano, todas longe de casa; ~12–17% do consumo). Agora a comida é
**parte do pagamento**, como o salário "com comida" da vila pré-industrial: cada refeição vai à caderneta do local
(`Workplace.Board`) ao preço da venda e sai do líquido do próximo salário (`WagePaymentSystem`; o valor fica no caixa, onde a
comida já saiu), até o líquido inteiro (`EconomyRules.BoardMaxWageShare` 1,0); o que passa disso — na carestia o trigo chega a
20 e o salário é 90 — a oficina paga em espécie, como o empregador que alimenta o trabalhador na fome. Ofício sem salário não
ganha refeição. Quem saiu da folha paga da carteira o que deve; o resto o empregador perde (morte, saída sem moeda; contado). A água do
poço da propriedade segue livre, como o poço da aldeia. `LW_AG=freeration` volta ao antigo e `LW_AG=nomeal` tira a refeição (só
para medir). 20×100 (carga desligada), base → caderneta: seeds 1–20 fome 517 → 627 mortes; seeds 21–40 619 → 495 (40 seeds:
1.136 → 1.122); ~0,1% do consumo vem da refeição; da caderneta, 52% sai do salário, 48% em espécie e 0,3% se perde.

## Compra a pé do outro lado da água (backlog 05 item 2a, AD-149)
Decidido com a medida: a casa **vai mais vezes**, não manda mais gente. A compra cortada pelo que cabe deixa a despensa abaixo da
meta e a reposição volta a disparar (`NeedsRestocking`). O corte é pequeno: 20 × 100 da base, 29.516 compras cortadas e 210.810
unidades em 90 anos (~0,1% da comida comida); e o recipiente inventado leva o resto. Mandar duas pessoas brigaria com
`OneShopperPerHousehold` sem ganho medido.

## Ver também
- [behavior.md](behavior.md) — a decisão que leva ao mercado
- [economy.md](economy.md) — preço, venda da fazenda e celeiro
