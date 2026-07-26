# Fase 8 — Cidades

**Objetivo**: fecha os **objetivos #2 e #4** — cidades viram entidades que crescem,
encolhem e fundam assentamentos, e qualquer NPC vivo é inspecionável por **API e CLI**
(o cliente web é a Fase 14; o objetivo #2 não depende dele).

## Tasks
1. **Cidade como entidade simulada**: população, governo, economia, recursos, segurança,
   saúde, educação, infraestrutura, habitação e desigualdade. Todo campo é derivado dos
   NPCs e dos edifícios — nada de número de cidade editado à mão.
2. **Crescimento e encolhimento**: saldo de nascimentos, mortes, imigração e emigração.
   Falta de comida, moradia ou segurança reduz a população; excedente atrai gente.
3. **Construção de edifícios** conforme demanda e recursos disponíveis: obra consome
   material e mão de obra ao longo de vários ticks, com fila e receita declarada no cenário.
4. **Migração entre assentamentos**: NPC ou household decide sair por emprego, comida,
   segurança e laços familiares. Sair de A entra em B — nunca some no caminho.
5. **Fundação de assentamento**: dispara quando concentração populacional, recursos,
   acesso a rota, defensabilidade e presença de liderança batem os limiares do cenário.
   O cenário declara também o **tempo de organização** — quantos ticks o grupo leva entre
   bater os limiares e fundar. Um grupo migra junto; a cidade-mãe perde exatamente ele.
6. **Simulation LOD**: população agregada (estatística) vs NPCs materializados (indivíduos
   completos). Materializar 10 NPCs tira 10 do pool agregado; desmaterializar devolve os
   atributos ao agregado. Conservação é a regra dura desta fase.
7. **Política de materialização**: materializa quem tem papel (líder, mestre, chefe de
   household), quem está no foco do observador e quem é alvo de inspeção. O resto agrega.
8. **API + CLI de inspeção de NPC (objetivo #2)**: endpoint e comando que devolvem
   identidade, família, profissão, atributos, rotina e memórias de qualquer NPC vivo.
   Somente leitura — nada nesse caminho escreve no mundo.
9. **Contagem independente para auditoria**: `COUNT(*)` no store de NPCs e o contador
   agregado persistido, ambos legíveis **sem passar** pela propriedade derivada de
   população. Existe para que o critério de conservação do LOD não seja `a + b == a + b`.

## Critérios de verificação
- **Conservação do LOD contra fonte independente**: para toda cidade e todo tick,
  `COUNT(*) de NPCs materializados no store + contador agregado persistido` bate com a
  população total — os dois lados lidos sem tocar a propriedade derivada. Assert a cada
  tick em 10 anos no gate; 100 anos em nightly (`Category=Scenario`).
- **Round-trip de materialização**: materializar e desmaterializar o mesmo NPC deixa
  `Hash(world)` byte-idêntico — população e somas agregadas inclusas.
- **Agregados de cidade recomputados do zero**: riqueza, saúde e desigualdade da cidade
  conferidas, a cada `N` ticks (`N` do cenário), contra a soma recalculada do zero sobre os
  NPCs materializados. Divergência de uma unidade falha.
- **Fundação com gatilho já satisfeito**: cenário montado com **todos** os limiares de
  fundação satisfeitos no tick 0. Assert de fundação em `≤ K` ticks, `K` = tempo de
  organização declarado no cenário. E a soma das populações antes e depois do split bate.
- **Fome derruba população, com braço de controle**: par base/tratamento na **mesma seed**,
  tratamento = produção de comida zerada. Assert `popTrat < popBase`, com a diferença
  **maior que o spread** entre duas seeds do baseline — senão é só demografia normal.
- Um NPC que migra de A para B aparece em B no mesmo tick em que sai de A. Zero NPCs vivos
  sem cidade, assert a cada tick em 10 anos.
- **Obra sem material não avança**: iniciar obra sem o insumo declarado retorna `Failure` e
  deixa `Hash(world)` inalterado; nenhuma obra conclui sem consumo registrado igual à
  receita do cenário. (Estoque não-negativo é invariante de tipo — não vira critério.)
- **Inspeção exaustiva, sem sorteio**: em um mundo de 100 NPCs, **iterar todos os vivos** e
  comparar a resposta da API campo a campo com o estado do motor no mesmo tick. Os campos
  do DTO são enumerados por reflexão e o teste **falha se algum ficar sem comparação** —
  campo novo no DTO entra no gate sozinho.
- **O LOD entrou na conta**: desligar LOD e migração por flag de teste muda `Hash(world)`
  após 10 anos.

## Fora do escopo
Guerra entre cidades, tratados e política externa: fora do roadmap atual. Cliente web de
inspeção: Fase 14. Memória histórica e crença sobre fundações: Fase 9. Diálogo: Fase 10.

## Ver também
[cities.md](../domain/cities.md) · [simulation-lod.md](../domain/simulation-lod.md) ·
[world-map.md](../domain/world-map.md) · [society.md](../domain/society.md) ·
[rules/eval-criteria.md](../../rules/eval-criteria.md) ·
[rules/database-entities.md](../../rules/database-entities.md)
