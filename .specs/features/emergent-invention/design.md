# Invenção emergente — Design (proposta para aprovação, ADR-0045)

> Pedido: `.specs/features/realism-backlog/05-invencao-emergente.md`. Nada implementado. Irmão:
> [physics-client-cost.md](physics-client-cost.md) (avaliador físico, aparência/animação, custo, testes).

## Princípio
Leis físicas são código; **artefatos nunca**. O cenário declara a natureza (materiais brutos e suas
propriedades medidas); o motor declara física simples; o NPC compõe. Nenhum id, nome ou lista de
invenção existe em código, cenário ou teste de produção. "Barco" é o rótulo que uma cultura dá a um
`Design` que o avaliador mostrou flutuar (ADR-0013: a LLM só nomeia).

## Representação (canônico, ADR-0014)
- **`MaterialSpec`** (cenário, por recurso bruto): densidade (kg/m³), resistência à tração e à
  compressão (MPa), rigidez, dureza, temperatura de trabalho, energia de combustão (kJ/kg),
  impermeabilidade, perfil químico (vetor de eixos inteiros, só para matéria orgânica), cor e
  acabamento. Dado do mundo real, com marca "precisa de conferência".
- **Material processado** não é declarado: `Process` = entradas + operação física (aquecer a T,
  bater, misturar, secar, ferver) → `MaterialSpec` derivado por regra (mistura ponderada, têmpera).
  T alcançável depende do combustível e da forma do forno, que é outro `Design`.
- **`Design`** (a concepção): até 16 `DesignPart` = {primitiva (box|cylinder|sphere|capsule, as
  de `ObjectPart`, `src/LivingWorld.Domain/Objects/ObjectCatalog.cs:5`), dimensões em mm (int16×3),
  material ou processo (int16), junção ao pai (fixa|eixo|dobradiça|corrediça|flexível), pai,
  orientação}. ~12 B por parte. Id = hash estável da codificação (estrutural, igual em dois mundos).
- **`Performance`**: o que o avaliador mede contra cada **sonda de função**. Volátil: recomputável
  do `Design`, memoizado por id.
- **Sondas de função = consumidores que o motor já tem**, não categorias de invenção: tempo de
  deslocamento por terreno (`Mobility.TravelTicks`, `src/LivingWorld.Simulation/Emergence/Mobility.cs:28`),
  célula de água transponível, carga por viagem (tamanho da compra), duração de ação
  (`ObjectEffect`, `ObjectCatalog.cs:24`), fecundabilidade (`ReproductionRules`), contágio e
  letalidade (vetor de doença, ADR-0043), produção por hora (oficina). Sonda nova só nasce com
  consumidor novo (`rules/living-world-cohesion.md`).
- **`KnownDesign`** por NPC: (design id int32, fidelidade 0..1000 int16, origem). ~6 B cada.
- **`Item`**: instância produzida no estoque (design id, qualidade, desgaste `LazyNeed`).

## Como o NPC chega à ideia
1. **Necessidade vivida**: frustrações por sonda, alimentadas pelo log de eventos no instante
   (padrão de `LifeEventIntake`, `src/LivingWorld.Simulation/Cognition/LifeEventIntake.cs:15`), sem
   varredura: trajeto longo, rio que barra, carga que não cabe, filho morto, febre, filhos demais
   para a despensa. Cada frustração aponta a sonda que a aliviaria.
2. **Gatilho** (`Monthly`, só quem tem frustração acima do limiar): chance por stream
   `invent-{npcId}` = f(frustração, abertura, tempo livre, habilidade, amplitude do que conhece).
3. **Concepção**: busca local no espaço de composição a partir do que o NPC conhece: seus
   `KnownDesign`, artefatos que viu em uso, e **observações da natureza** que são fatos físicos do
   lugar (quem vive à beira do rio e conhece madeira "viu" que ela boia: densidade < água).
   Operadores: trocar material, mudar dimensão, somar parte, trocar junção, recombinar dois designs.
   K=8 variantes; o NPC as julga com o avaliador **ruidoso pela habilidade** (crença, não verdade).
4. **Protótipo**: gasta material e horas (como `TechDiscoverySystem.cs:56-66`: retirada registrada
   e conclusão agendada). Na conclusão, o avaliador **real** + ruído de fabricação semeado decide:
   funciona, afunda, quebra, pesa demais. A maioria falha.
5. **Falha ensina**: vira memória (vergonha, frustração) e fato aprendido ("este material não
   aguenta este vão"), que poda a próxima busca. A próxima tentativa parte do melhor protótipo.

## Difusão, reprodução e perda
- **Ensino** pela tutoria e `SkillTeachingSystem` (`src/LivingWorld.Simulation/Population/Skills/SkillTeachingSystem.cs`):
  fidelidade alta. **Cópia** ao ver um `Item` em uso na mesma célula ou prédio (evento, não varredura):
  fidelidade menor, partes internas podem sair erradas, então copiar também muta.
- **Segredo ou venda**: personalidade (ambição, altruísmo) e mercado decidem; ensinar dá renda.
- **Reprodução depois**: quem tem `KnownDesign` + habilidade + materiais produz. A receita é
  **gerada do design** (material = volume × densidade; horas = partes × dificuldade da junção) e
  entra na produção existente. Qualidade sai da habilidade.
- **Perda**: morreu o último que sabia, ninguém produz. `Item` que sobrevive pode ser copiado de
  novo (engenharia reversa, com perda). Design sem quem saiba nem instância vira lápide.

## Na economia e na vida (consumidores reais)
Veículo próprio substitui o bônus por cidade em `Mobility.TravelTicks`; barco torna água transponível;
ferramenta reduz duração de trabalho; contraceptivo reduz fecundabilidade com taxa de falha medida
pelo avaliador (barreira: impermeabilidade e ajuste; química: perfil contra o eixo hormonal do
AD-139); remédio é preparo cujo perfil químico casa com o vetor da doença e muda duração ou
letalidade. Preço pelo mercado; vocação de artesão emerge (AD-136). Desigualdade: quem tem, chega antes.

## Eras sem roteiro
Era é estatística lida, nunca portão: maior T de forno, maior resistência de material, maior
densidade de energia acessível. Motor antes de metalurgia é impossível porque o avaliador reprova
a peça que não aguenta calor e pressão, não porque uma tabela diz.

## Aproveitado e em conflito (o que existe hoje)
- **Aproveita**: leitura de pressão por trajeto (`src/LivingWorld.Simulation/Emergence/TechDiscoverySystem.cs:125-144`),
  custo e agendamento do protótipo (`:52-66`), limiar de novidade (`:83-91`, vira distância entre
  designs), difusão por vínculo (`:169-209`, vira por NPC); `ObjectPart`/`ObjectDescriptor`
  (`ObjectCatalog.cs:5`, `:42`) e `object_factory.gd`; instância, desgaste e denominação de
  `.specs/features/emergent-vehicles/design.md`.
- **Conflita**: inventa a **cidade**, não o NPC (`TechDiscoverySystem.cs:39-62`); eixos semânticos
  declarados `pace, load, reach` e categoria fixa `mobility` (`src/LivingWorld.Domain/Emergence/EmergenceRules.cs:22`, `:25`);
  o vetor sai de pressão+habilidade+estoque+sorteio e "funciona" por construção (`TechDiscoverySystem.cs:103-120`);
  saber nunca se perde (adoção por cidade, `Technology.cs:40`); a Fase 24 exige pré-requisitos
  **declarados** (`docs/roadmap/phase-24-emergence.md:16`, `:66`); a spec de veículos enumera tipos
  de propulsão (`.specs/features/emergent-vehicles/spec.md:19`). O `TechDiscoverySystem` fica atrás
  da flag até o domínio água/terra cobrir mobilidade; depois sai, com goldens regravados.

## Decisões do dono (aprovadas em 2026-11-12: a opção em negrito de cada item)
1. Leis físicas escritas no motor (artefatos nunca) é aceitável como "nada escrito"? **Sim** | não.
2. Primeira fatia: transporte sobre água de ponta a ponta? **Sim** | terra | ferramenta de trabalho.
3. `TechDiscoverySystem` é substituído (goldens regravados)? **Substituir após a fatia 1** | manter os dois.
4. Robô como agente que decide fica fora até ADR próprio; automação entra só como capital de oficina? **Sim** | já agente.
5. Design sem quem saiba nem instância: **lápide pequena** (relatos apontam para ela) | apagar.
6. Contracepção e remédio entram depois da fatia de água, ligados ao AD-139 e ao AD-137? **Sim** | antes.
7. Mudar o critério da Fase 24 para "pré-requisito emerge do avaliador" no `ROADMAP`? **Sim** | não.
