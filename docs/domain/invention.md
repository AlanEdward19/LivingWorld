# Invenção por composição física (ADR-0045, AD-141)

Quem inventa é a pessoa. Leis físicas são código; artefatos nunca. O cenário declara só a natureza
(materiais brutos e a água); o motor mede; o NPC compõe. "Barco" é o nome que alguém dá a um design
que o avaliador mostrou flutuar, carregar e andar. Primeira fatia: atravessar água.

## Peças
- **`MaterialSpec`** (`InventionRules.Materials`): densidade, tração, impermeabilidade, peça natural
  (tronco, feixe, lâmina, bloco) e seus limites, parede mínima, minutos de coleta e de trabalho por kg,
  vida útil, se só nasce à beira d'água, se só serve de revestimento, se se molda sobre armação, e a
  aparência (cor, acabamento, grão, condutividade). Id próprio, sem nome no motor.
- **`Design`**: até 16 `DesignPart` (box, cylinder, sphere, capsule; medidas em mm; parede 0 = maciça;
  material, revestimento, junção à mãe, offset). Id = FNV-1a da estrutura. Proveniência: quem, quando,
  pais e origem (inventou, aprendeu, copiou, engenharia reversa).
- **`InventionLedger`** (canônico, `WorldState.Invention`): designs vivos, lápides, `KnownDesign` por
  pessoa (fidelidade ‰), itens (`CraftedItem`, de um domicílio, até gastar) e mentes (frustração em
  minutos, ocupado até, tentativas, falhas, último protótipo que falhou, material em que desconfia).
  Vazio no tick 0; nenhum cenário tem bloco de designs.

## Avaliador (`PhysicsEvaluator`, só inteiros)
Massa e volume por primitiva (π = 355/113); empuxo: a cota d'água sai por busca binária até o volume
deslocado igualar massa + pessoa (casco oco estanque desloca o volume externo; oco que encharca, só o
material); borda livre mínima; estabilidade: KB + BM − KG ≥ mínimo (BM = I da linha d'água / volume);
resistência: viga com a pessoa no meio (M = P·L/8) contra módulo de seção × tração / fator de segurança
das partes que vencem o vão, pesadas pela junção; velocidade: v³ = 2·P·η / (ρ·(Cd·A + Cf·S)), com η
maior se houver parte chata articulada que empurra a água. Falhas: inválido (a natureza não dá a peça),
afunda, vira, quebra, lento. A receita de construção (material por id e horas) sai do mesmo design.

## Como o NPC chega à ideia (`InventionSystem`, mensal)
1. **Vivência**: ao chegar de um trajeto cuja reta cruza células com água, a pé, o tempo perdido no
   vau vira frustração (`InventionIntake.OnTravelArrived`, no instante, sem varredura). Decai 30%/mês.
2. **Gatilho**: acima do limiar, chance por mês = base × frustração × abertura × tempo livre × habilidade.
3. **Ideia**: copia um item visto na cidade (com perda) ou concebe 8 variantes a partir do protótipo
   que falhou, dos designs que sabe e da peça natural dos materiais que viu boiar (só quem mora perto
   da água vê o junco); operadores genéricos (medida, duplicar ao lado, escavar, revestir, trocar
   material, parte chata, juntar designs, tirar parte, junção, primitiva), puxados pela última falha.
4. **Julgamento**: o avaliador com a crença (densidade e tração com erro que cai com a habilidade);
   só constrói o que acredita funcionar.
5. **Protótipo**: as horas do design espalhadas no tempo livre (3 h/dia com emprego, 8 sem); na
   conclusão, a peça como saiu das mãos (erro de fabricação) passa pelo avaliador real.
6. **Falha ensina**: lembrança (`InventionFailed`: raiva e vergonha), ponto de partida da próxima
   tentativa e, se quebrou ou afundou pelo material, desconfiança dele. Sucesso: `InventionWorked`.

## Difusão, produção e perda
Quem sabe ensina por mês alguém da casa ou amigo próximo (segredo se ambicioso e pouco altruísta), com
perda de fidelidade e chance de uma medida sair errada (vira outro design, de pai conhecido). Quem sabe
e não tem embarcação em casa constrói a sua. Itens gastam (vida do material mais curto × qualidade). Na
morte, o saber vai junto; design sem quem saiba e sem item vira lápide (`DesignTombstone`). Item que
sobrou pode ser copiado de novo (engenharia reversa).

## Consumidor causal
`Mobility.TravelTicks` e `Mobility.HouseholdTicks`: cada célula d'água na reta custa o vau a pé; com
embarcação na casa, custa remar na velocidade medida mais pôr e tirar (o menor dos dois). Isso muda o
trajeto de todo deslocamento, a meta de despensa e o tamanho da compra no mercado.

## Contrato com o cliente
`GET /designs` e `GET /designs/{id}`: partes (forma, tamanho em m, offset, eixo, parede, junção, mãe,
material) e materiais (cor, acabamento, grão, condutividade); `object_factory.gd::build_design` monta
uma malha por parte, albedo = cor, rugosidade = 1 − acabamento, metálico = condutividade alta.

## Ainda não
Animação por junção, pose de uso, carga por viagem, venda no mercado, terra e ar, química (remédio e
contracepção, decisão 6), robôs (decisão 4). Substituir o `TechDiscoverySystem`: ver AD-141.

## Números
Padrão em `InventionRules` (Domain); `InventionRules.Disabled` = mundo de antes. Valores e marcas
"precisa de conferência" no registro AD-141 de `docs/decisions-log.md`.
