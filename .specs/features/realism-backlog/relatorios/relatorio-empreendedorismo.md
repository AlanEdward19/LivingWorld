# Empreendedorismo no LivingWorld: como é hoje e como deveria ser

Leitura só de código (branch `perf/decision-scan-costs`, com as mudanças não commitadas). Nada foi executado.
**[V]** = verificado no código, com arquivo:linha. **[I]** = inferência minha. **[U]** = dado que veio do pedido e que não consegui medir.

## 1. Como é hoje

**Quem decide abrir uma oficina: a cidade, nunca um NPC [V].**
- `ConstructionDemandSystem.cs:50-78` roda uma vez por dia. Se existe um adulto desempregado cujo ofício aponta para um tipo de local sem vaga, a cidade enfileira uma obra desse tipo (uma por tick, a receita de menor id). Não pesa preço, lucro, demanda nem a personalidade de ninguém.
- `Personality` tem `Ambition` e `RiskAversion` (`Personality.cs:15,19`), mas os dois só pesam na escolha da ação da hora (`PersonalityWeighting.cs:19,30`).

**Quem paga a construção: o tesouro da cidade, só pelo material [V].**
- `ConstructionSystem.cs:61-67` gasta `City.Stock`.
- Esse estoque é comprado com `City.Treasury` das próprias oficinas da cidade (`InputSupplySystem.cs:48-57`, com meta de "uma obra de cada receita" em `:63-71`).
- A mão de obra é de graça: a velocidade da obra é a maior força entre os NPCs da cidade que estão em `Work` naquela hora, seja onde for (`ConstructionSystem.cs:133-146`). Ninguém é contratado nem pago pela obra.
- Não existe terreno com preço.

**Caixa inicial [V].**
- A oficina construída nasce com `treasury: Money.Zero` e preços copiados de outra oficina do mesmo tipo (`ConstructionSystem.cs:110-114`).
- Ela contrata mesmo sem caixa, porque `PayrollFailed` nasce falso (`Workplace.cs:69-71`, AD-128).
- No primeiro mês sem receita, a folha falha e a oficina para de contratar.
- As oficinas iniciais recebem caixa declarado no cenário, que não passa por `Mint`:
  - `ScenarioRunner.Seeding.cs:51-66`: fazenda 500 000 e ferraria 500 000. O comentário chama isso de "capital de giro do dono", mas não existe dono.
  - `scenarios/default.json:101-128`: 500 000, 200 000 e 200 000, contra 20 000 da cidade (`:163`).
- Pela API, a oficina criada por autoria cunha moeda nova (`WorldAuthoringCommands.cs:125`).
- A conservação é "saldo = inicial + cunhado − destruído" (`WorldState.cs:369-373`). Por isso esse caixa não fere o invariante, mas vem do nada do ponto de vista narrativo.

**Dono (`Owner`): na prática, sempre `null` [V+I].**
- O comentário diz "só entra por autoria do cenário ou por herança" (`Workplace.cs:30-33`).
- Mas `EconomyScenarioLoader.LoadWorkplace` não lê `Owner` (`EconomyScenarioLoader.cs:142-166`), e a obra cria a oficina sem dono.
- O único que chama `AssignOwner` é `EstateSystem.cs:108-118`, que só repassa um dono que já existia.
- Sobra apenas um snapshot editado à mão (`WorldSnapshot.cs:121`). **[I]** Num mundo rodando, a herança de oficina é código morto.
- Mesmo com dono, ele não ganha nada: não há retirada, e a carteira dele nunca toca o `Treasury`.

**Para onde vai o lucro [V].**
- Fica no `Workplace.Treasury`.
- Entradas: venda ao NPC (`BehaviorDecisionSystem.Effects.cs:241-250`), venda de insumo a outra oficina ou à cidade (`InputSupplySystem.cs:119-120`), aluguel quando a oficina é senhoria (`LodgingSystem.cs:81`) e taxa de treino (`SkillTeachingSystem.cs:123`).
- Saídas: salário (`WagePaymentSystem.cs:60`) e compra de insumo (`InputSupplySystem.cs:91`).
- O salário é tabela fixa por ofício (`WagePaymentSystem.cs:37-40`), sem relação com a receita. Não há investimento, ampliação nem dividendo.
- **[I]** Uma oficina que vende mais do que paga entesoura para sempre.

**Quando o caixa acaba [V].**
- A folha é paga pro rata, um `WageUnpaid` por pessoa (`WagePaymentSystem.cs:50-57`), e marca `PayrollFailed` (`:46-47`).
- Com a marca: ninguém entra (`EmploymentSystem.cs:52`), e os empregados avaliam trocar de emprego (`BehaviorDecisionSystem.Profession.cs:48,60`).
- A oficina nunca fecha. Não existe `RemoveWorkplace` no código, nem venda de ativo ou dívida pendente.
- A marca cai quando o caixa volta a cobrir um salário.

**Quem compra o que cada oficina produz [V].**
- O NPC compra só comida e água, e só do mercado de essenciais mais próximo (`Effects.cs:210-255`).
- Quem não tem moeda tira água de graça do poço (`:227-232`).
- Outras oficinas compram insumo só se a receita delas declara `Inputs`.
- A cidade compra só material de obra.
- No `ScenarioRunner` padrão, a ferraria produz o recurso 4 (`ScenarioRunner.cs:243-245`). Nenhuma receita pede 4 como insumo, e `DemandBaselinePerNpc` não tem a chave 4 (`:224`). Assim, `MarketPricingSystem` nem reajusta esse preço (`MarketPricingSystem.cs:37`). **[I]** Ninguém compra ferro, então a ferraria só gasta e vai a zero. Isso bate com o relato.
- O preço reage ao estoque dividido por (base × população da região) (`MarketPricingSystem.cs:39-48`), não a vendas reais nem a custo.

**Crédito ou dívida em moeda: não existe [V].**
- `Relationship.Debt` é um eixo social `double` (`Relationship.cs:32`). Sobe +3 quando alguém recebe ajuda (`ScenarioRunner.cs:149-153`) e alimenta só atenção e contexto de decisão (`AttentionRouter.cs:137`, `DecisionContextBuilder.cs:112`). Não há principal, juros, prazo nem cobrança.
- `MutualAidSystem` é doação, nunca devolvida (`MutualAidSystem.cs:148-161`), e a esmola sai da cidade (`:172-184`).
- O doc `genetics-and-family.md:17` já prevê "empréstimo" como fonte desse eixo.
- Herança: a carteira vai a cônjuge, filhos, pais ou irmãos, e sem herdeiro vai à cidade (`EstateSystem.cs:3-8,61-87`).

**Concentração de moeda [U].** O número de 520 mil para 980 mil em 50 anos não foi medido por mim. O mecanismo (só a fazenda vende o que todos precisam, e o salário é fixo) é consistente com o código.

## 2. O que não é realista (do mais grave ao menos grave)

1. **Ninguém empreende.** A cidade abre a oficina porque "falta vaga", não porque alguém vê lucro. Não há dono, risco nem ambição.
2. **Moeda inicial sem origem e sem dono.** Centenas de milhares de moedas no caixa de oficinas sem dono, contra 20 mil da cidade, distorcem toda a economia por décadas.
3. **O lucro não tem destino.** O caixa só cresce ou só cai. Ninguém retira, investe ou paga dividendo, e a moeda fica presa na fazenda. **[I]** É a causa estrutural da concentração.
4. **Produção sem mercado.** A ferraria produz o que ninguém compra e continua contratando. Nada liga o que se produz à demanda real.
5. **Oficina imortal.** Sem falência, liquidação ou fechamento, um negócio inviável fica para sempre como "PayrollFailed".
6. **Salário desligado da receita.** Tabela fixa por ofício: a oficina rica não paga mais, e a pobre não negocia nem demite para sobreviver (só perde gente por evasão).
7. **Obra de graça.** O pedreiro não recebe, o terreno não custa, e não há ferramenta nem estoque inicial como custo de abertura.
8. **Não existe crédito.** O eixo `Debt` só tem nome: não há empréstimo, juros, garantia nem calote.
9. **Herança de negócio inerte.** O código existe, mas nunca dispara, porque não existe dono.

## 3. Desenho realista e implementável

Princípios: toda rolagem vem de `ctx.StreamFor("<sistema>", chave)` (mesmo molde de `MutualAidSystem.cs:153`), toda moeda só troca de dono (`TryDebit`/`Credit`), todo número fica em `EconomyRules.Entrepreneurship` no cenário (`null` = modelo antigo, hash idêntico), e a LLM no máximo narra o evento.

**3.1 Dono e retirada (base de tudo).**
- `Owner` passa a ser lido do cenário (`EconomyScenarioLoader`).
- Novo `OwnerDrawSystem` mensal, depois de `WagePaymentSystem`: se `Treasury` > reserva (N meses de folha mais insumo), o dono retira uma fração do excedente para a carteira.
- Oficina sem dono, ou da cidade: o excedente vai para `City.Treasury`, como um feudo ou comuna.
- Estado novo: nenhum; usa `Owner` e regras. Toca `Workplace`, `WagePaymentSystem` (ordem) e `EstateSystem` (passa a disparar).

**3.2 Motivação para empreender.**
- Novo `VentureSystem` mensal. O candidato é adulto, tem habilidade no ofício acima de um limiar (`Npc.Skills`) e tem capital acessível.
- O desejo combina `Ambition`, `1 − RiskAversion` e a oportunidade percebida na cidade: preço perto do teto, estoque abaixo da demanda base, ou fila de desempregados do ofício (o mesmo sinal que hoje vai a `ConstructionDemandSystem:66-78`).
- Uma rolagem semeada decide.
- Estado novo: `Npc.VentureCooldownUntil` (long), para não tentar todo mês.

**3.3 Capital.**
- Fontes, em ordem:
  1. Carteira própria, ou seja, poupança de salário.
  2. Herança, que já cai na carteira.
  3. Sócio: parente ou amigo com `Trust` alto e sobra. Vira cotista, com `Workplace.Shares: List<(NpcId, int pct)>`, e a retirada é dividida pelas cotas.
  4. Empréstimo.
- Empréstimo é um novo `Loan(LoanId, Lender: NpcId|CityId|WorkplaceId, Borrower: NpcId, Principal, RateMonthly, InstallmentsLeft, Collateral: WorkplaceId?)` em `WorldState.Loans`, no snapshot e com hash.
- O credor é um NPC rico (carteira > K vezes o custo de vida), a cidade (fração limitada do tesouro) ou uma oficina com excesso de caixa, o que também drena o entesouramento.
- A decisão de emprestar pesa `Trust` com o tomador, `RiskAversion` do credor e o histórico de calote.
- Novo `LoanServicingSystem` mensal: cobra a parcela da carteira do tomador ou do caixa da oficina garantida, com juros que só transferem moeda.
- Calote: atraso acumula e, depois de X parcelas, executa a garantia (a oficina passa ao credor) e aplica um evento `Default` que derruba `Trust` e sobe o eixo `Debt`, o que dá enfim uso real ao eixo.

**3.4 Custo de abrir.**
- Abrir vira `ConstructionProject` com `Sponsor: NpcId?`.
- O dono paga, adiantado ou por tick: material comprado do mercado (o `Buy` de `InputSupplySystem`, com a carteira dele), o salário de obra dos NPCs da cidade (`ConstructionSystem.ConstructionWorkSteps` passa a pagar quem trabalha), terreno (lote como taxa à cidade, regra `PlotPriceByBuildingType`), ferramentas e estoque inicial (receita opcional `StartupInputs`).
- O que sobra do capital vira `Treasury` inicial, como capital de giro.
- `ConstructionDemandSystem` continua só para moradia e obra pública, ou como fallback da cidade quando ninguém empreende.

**3.5 Folha e gestão.**
- Contratar só quando o caixa cobre M meses da nova folha.
- Se a folha falha dois meses seguidos, o dono demite o mais recente (último a entrar) em vez de esperar evasão (`Workplace.Fire` com um evento `Laid off`).
- Salário: base na tabela, com prêmio opcional por margem (fatia futura; arrisca a calibragem).

**3.6 Falência.**
- Dispara com N meses de `PayrollFailed` e caixa abaixo de um limiar, ou dívida maior que os ativos.
- Novo `BankruptcySystem`:
  - vende o estoque às outras oficinas e à cidade ao preço de mercado (reaproveita `Buy`);
  - paga, em ordem, salário atrasado (exige registrar o atraso: `Workplace.WageArrears` por NPC), credores e dono;
  - remove a oficina (novo `WorldState.RemoveWorkplace`, com `EmploymentSystem:33-42` já tratando órfãos); o prédio vira da cidade, vazio e reaproveitável.
- A dívida restante fica no `Loan` do dono, como pessoa física, até ser paga ou até a morte.

**3.7 Sucessão.**
- `EstateSystem` passa a herdar cotas (`Shares`) e empréstimos.
- O crédito vira do herdeiro. A dívida é cobrada do espólio antes da partilha (`PayHeirs`), com perdão do resto se o espólio não cobrir, como no direito moderno.
- O herdeiro sem o ofício pode vender a oficina (fatia futura).

**3.8 Fechar o ciclo de demanda.**
- Bem sem consumidor (o ferro) precisa de comprador real: ferramentas como insumo da fazenda (aumenta a produtividade, `Inputs` na receita) ou desgaste de ferramenta do NPC.
- Sem isso, nenhuma motivação por oportunidade escolhe a ferraria, o que é realista, mas a ferraria inicial continua morrendo.

## 4. Ordem de implementação (fatias pequenas, cada uma com consumidor causal)

| # | Fatia | Consumidor causal | Critério mensurável |
|---|---|---|---|
| 1 | `Owner` no cenário + `OwnerDrawSystem` | carteira do dono → compras e mutual aid; herança dispara | 50 anos/100 NPCs: participação da fazenda na moeda cai abaixo de X% (medir antes); `EstateSettled` com `transferred>0` aparece |
| 2 | Oficina sem dono devolve excedente à cidade | esmola e obra pública | tesouro da cidade cresce; Gini das carteiras não piora |
| 3 | Demanda real de ferramenta (insumo da fazenda) | ferraria vende | ferraria com caixa > 0 em 20/20 seeds aos 30 anos |
| 4 | `VentureSystem` com capital próprio + obra paga pelo dono | nova oficina com `Owner` e caixa vindo da carteira | conservação exata (sem `Mint`); ≥1 oficina aberta por NPC em ≥N seeds; nenhuma por NPC sem habilidade |
| 5 | Falência + `RemoveWorkplace` + liquidação | moeda e estoque voltam a circular | nenhuma oficina com `PayrollFailed` por mais de K meses; soma de moeda idêntica antes e depois |
| 6 | `Loan` + `LoanServicingSystem` (credor NPC e cidade) | crédito financia a fatia 4 | juros pagos = Σ transferências; taxa de calote em faixa calibrada; `Debt` muda por evento real |
| 7 | Sócio/cotas + herança de cotas e dívida | sucessão empresarial | herdeiro recebe cota; dívida abatida do espólio |
| 8 | Demissão por caixa e salário ligado à margem | mercado de trabalho reage | desemprego e fome dentro da faixa do golden atual |

Em toda fatia: `rules/living-world-cohesion.md` (nada sem consumidor), um teste de conservação (Σ carteiras + tesouros = inicial + cunhado − destruído) e uma medida de perf (`stress:500`), porque `VentureSystem` e `Loan` varrem NPCs e precisam de índice.

**Riscos de calibragem.**
- **Fome em cascata:** a retirada do dono drena o caixa da fazenda e a folha falha, como no comentário de `Seeding.cs:51-55`. Por isso a reserva de N meses precisa vir antes de qualquer retirada.
- **Demografia:** falência ou demissão aumentam o desemprego; sem salário não há compra, só poço e esmola, e a fome mata em 21 dias (AD-128). A fatia 5 precisa da 2 (esmola com caixa).
- **Espiral de crédito:** juros compostos podem concentrar moeda no credor mais rápido que hoje. Teto de juros e de alavancagem nas regras.
- **Ninguém empreende ou todos empreendem:** a limiar de ambição e habilidade e o cooldown são sensíveis. Medir oficinas por 100 habitantes ao longo de 100 anos.
- **Caixa inicial do cenário:** ao trocar os 500 mil por carteiras de donos, mudam todos os goldens e pins. Fazer numa fatia isolada, com ADR, porque muda o contrato do cenário.
- **Determinismo:** a ordem de credores e candidatos por id, uma rolagem por (NPC, mês), e `Loans` no hash canônico.
