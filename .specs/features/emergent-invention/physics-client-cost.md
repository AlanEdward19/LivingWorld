# Invenção emergente — avaliador físico, cliente, custo e testes

> Irmão de [design.md](design.md). Proposta; nada implementado.

## Avaliador (código = leis, nunca artefatos)
Aritmética inteira (int64; mm, g, mN, J), só `+ - * /`; raiz e equilíbrio por iteração de Newton
inteira com número fixo de passos. Nada de `Math` transcendental (`rules/simulation-determinism.md`,
AD-108). Ordem fixa: partes na ordem do design, candidatos por id de NPC, variantes na ordem do stream.
`DesignPart` ganha espessura de parede (0 = maciça) para existir casco oco.
- **Massa e volume**: Σ volume da primitiva × densidade do material.
- **Flutuação**: boia se massa + carga < ρ_água × volume externo; carga útil é a sobra.
- **Estabilidade**: centro de massa contra largura²/altura (metacentro grosseiro); vira se instável.
- **Resistência**: cada parte como viga simples; tensão = carga × braço / seção ≤ resistência.
- **Rolar e deslizar**: cilindro com junção de eixo tocando o chão rola (atrito por dureza e
  terreno); sem eixo, desliza (atrito do material).
- **Propulsão**: músculo do usuário (corpo, AD-134), vento do clima × área de parte plana, correnteza
  do rio, combustão = energia do combustível × eficiência de uma câmara que aguente T e pressão.
- **Arrasto e sustentação**: área frontal × coeficiente da primitiva; placa plana × ângulo × v².
- **Velocidade** = equilíbrio potência / força resistiva → fator de deslocamento por terreno.
- **Química** (remédio, contraceptivo): perfil químico do preparo · vetor da doença ou eixo hormonal;
  dose = massa; eixo de toxicidade fere a saúde. O perfil de cada espécie de planta é sorteado na
  criação do mundo (stream `flora-chem-{espécie}`): seeds diferentes têm plantas úteis diferentes.
- **Automação**: fonte não muscular + mecanismo que move uma parte-ferramenta → sonda "produção por
  hora sem operador", consumida como capital da oficina.

## Aparência e animação (contrato com o cliente)
- `GET /designs/{id}`: partes {primitiva, tamanho, offset, orientação, junção, pai, material} e
  materiais {cor, acabamento, dureza, condutividade, grão, translucidez}. Nenhum nome, nenhum modelo.
- O cliente já monta objeto por partes (`client/godot/Scenes/object_factory.gd:25`, malha em `:60`,
  material em `:84`). Estender `_material_for`: albedo = cor do material; rugosidade = 1 − acabamento;
  metálico = condutividade alta; textura procedural por grão com semente = id do design; desgaste
  (`LazyNeed` do item) escurece e racha.
- Animação sai da junção e do estado da instância, calculada no cliente (volátil): eixo gira com
  ω = v/r; dobradiça oscila com a cadência da fonte (remada = ritmo do corpo); flexível verga com
  carga e vento; corrediça vai e vem; casco balança com amplitude ∝ 1/estabilidade.
- Pose do NPC: ponto de uso derivado da geometria (parte plana horizontal a 0,4–0,6 m = assento;
  parte em dobradiça ao alcance = alavanca). O vocabulário de poses
  (`src/LivingWorld.Domain/Objects/ObjectCatalog.cs:15`) ganha poses de corpo genéricas (sentar,
  empurrar, puxar ritmado), nunca pose por artefato.
- Teste do cliente (`client/godot/tests`): 50 designs sorteados montam sem erro, uma malha por parte,
  cor vinda da tabela de material.

## Custo (conta explícita; medir no experimento)
Base: 0,844 µs/NPC-tick (`docs/decisions-log.md:70`, AD-119) e 12.396 B/vivo no snapshot (`:67`).
- **Concepção**: `Monthly` (720 ticks), só quem passou do limiar; suposição ≤ 10% dos NPCs.
  Tentativa = 8 variantes × 16 partes × ~40 operações ≈ 5 mil operações ≈ 10 µs com a busca.
  1.000 NPCs: 100 × 10 µs = 1 ms/mês ÷ (720 × 1.000) ≈ **0,0014 µs/NPC-tick (+0,2%)**. 5.000 NPCs: o
  mesmo por NPC (linear): 5 ms/mês. Protótipo e ensino são eventos agendados, não varredura.
- **Uso**: hoje `Mobility.BonusPct` percorre todas as tecnologias a cada deslocamento
  (`src/LivingWorld.Simulation/Emergence/Mobility.cs:15-23`); com `Performance` memoizado por id e
  veículo do dono, vira consulta O(1): custo igual ou menor.
- **Snapshot**: design ≈ 16 × 12 + 28 = 220 B; suposição de 500 designs vivos por 1.000 NPCs num
  século = 110 B/NPC; `KnownDesign` 6 B × 5 = 30 B/NPC; itens ~12 B/NPC. Total ≈ **150 B/NPC (+1,2%)**,
  igual a 5.000. Teto proposto: +3%. Eventos de produção agregados por oficina e dia, como o `TradeLedger` (AD-092).

## Testes de que nada é escrito à mão
1. Registro de designs vazio no tick 0 em todo cenário; não existe bloco de cenário para designs.
   Todo design tem proveniência até um `DesignPrototyped` com inventor, tick e pais (AD-013).
2. Duas seeds, mesmo cenário, 100 anos: conjuntos de ids com sobreposição < 50%, e as duas com design
   funcional para a sonda pressionada. Mesma seed em dois processos: mesma sequência de ids.
3. Perturbação física: madeira com densidade > água → nenhum design de madeira passa na travessia em
   nenhuma seed. A física decide; não há tabela a consultar.
4. Varredura de fonte (como `DeterministicMathTests`): nenhum literal de design ou parte em `Domain`,
   `Simulation` ou cenário de produção.
5. Pareado: vila sem rio não inventa travessia (≥ 18/20 seeds); com rio, inventa (taxa a calibrar).
6. Perda: matar todos os que sabem zera a produção no ano seguinte; item sobrevivente permite
   reaprender com fidelidade < 1.
7. Flag desligada = mundo de hoje byte-idêntico; ligada muda o hash.

## Experimento da fatia 1 (água, de ponta a ponta)
Materiais brutos do cenário (madeira, junco, couro, resina) com propriedades conferidas em fonte;
sonda = travessia de célula de água e tempo ao longo do rio; consumidor = `Mobility.TravelTicks`.
Aprova se os testes 1–7 passam, `scripts/compare-simulation-cost.sh` fica no teto (+3% CPU, +2%
alocação, `scripts/README.md:14`), snapshot ≤ +3% e a demografia de referência não sai da faixa.
