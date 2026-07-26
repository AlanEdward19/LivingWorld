# Fase 2 — Geografia mínima

**Objetivo**: o mundo tem geografia consultável como **dado** — regiões, células, terreno,
bioma, altitude, água, recursos — e uma função de custo de deslocamento. É exatamente o que
as Fases 5 e 8 consomem, e nada além disso. Nenhum pixel: visualização é a Fase 14.

## Tasks
1. **Grid de células e regiões**: célula com terreno, bioma, altitude, presença de água e
   recursos disponíveis; região agrupa células e é a unidade de consulta. Value objects
   imutáveis no Domain, sem referência a UI.
2. **Catálogo de terreno e bioma vindo do cenário**, com `Terrain.Unset = 0` como valor
   default explícito — célula não preenchida é detectável, não vira "planície" por acidente.
   Nenhum nome de terreno ou recurso escrito em C#.
3. **Custo de deslocamento**: `custo(origem, destino)` derivado de terreno + distância +
   altitude, com a tabela de pesos no cenário. Base de rota comercial (Fase 5) e migração
   (Fase 8). Entrega aqui a função e o pathfinding mínimo entre locais.
4. **Consulta por região**: célula → região, região → células, região → vizinhas. Índice de
   leitura, classificado como estado **volátil** no hash da Fase 1 (é cache reconstruível).
5. **Carregar mapa de cenário**: gerado por seed ou autoral em arquivo. Mesma seed → mesmo
   mapa. Validação no carregamento: campo inválido falha rápido, apontando o campo.
6. **Âncora de assentamento**: cidade/aldeia declarada no cenário aponta para uma célula do
   grid. Só a referência — crescer, migrar e fundar é Fase 8.
7. **Mapa entra no hash canônico** do mundo (Fase 1) e no `world-hashes.json`.

## Critérios de verificação
- Carregar o mesmo cenário com a mesma seed em **dois processos** → hash de mapa idêntico;
  seed diferente → hash diferente.
- **`Unset` é detectável**: em 20 seeds do gerador, zero células com `Terrain.Unset` e pelo
  menos 2 valores de terreno distintos presentes no grid. "Toda célula tem um terreno" é
  garantido pelo enum e não prova nada sozinho.
- **Custo, property-based sobre 1000 pares de células**: se `altitude(A) == altitude(B)`,
  então `custo(A,B) == custo(B,A)` exato; caso contrário `custo(subida) > custo(descida)`.
  Custo é sempre > 0 entre células distintas.
- **Efeito do terreno com controle**: 20 pares montanha/planície de mesma distância e mesma
  seed — custo da montanha maior em **20/20**. Direção, não magnitude.
- **Cobertura por enumeração**: toda célula do grid pertence a exatamente uma região e a
  região a devolve na consulta (round-trip sobre todas as células, não amostra); célula
  órfã reprova.
- Cenário cuja cidade aponta para célula fora do grid é **rejeitado no carregamento** com o
  campo na mensagem, e o mundo não é criado — não explode 40 anos depois.
- **A geografia entra na conta**: carregar o mesmo cenário sem a camada de geografia muda o
  hash canônico do mundo.
- Teste de arquitetura falha se nome de terreno, bioma ou recurso aparecer como literal em
  `src/LivingWorld.Domain` ou `src/LivingWorld.Simulation`.

## Fora do escopo
Tudo que é visual ou de cliente foi para a **Fase 14**: React, canvas, camadas de
renderização, drill-down de UI, OpenAPI, geração de tipos TS, endpoint de mapa e extensão
dos scripts para o ramo web. Cidades que crescem, migram ou se fundam (Fase 8), rotas
comerciais reais (Fase 5) e clima com efeito na produção (Fase 5+) também ficam fora —
aqui clima, se existir, é dado de célula, não simulação.
Esta fase **está** no caminho crítico do objetivo #1: é o mínimo de geografia que a Fase 8
exige, e é por isso que ela cabe em pouca coisa.

## Ver também
[world-map.md](../domain/world-map.md) ·
[cities.md](../domain/cities.md) ·
[phase-14-map-visual.md](phase-14-map-visual.md) ·
[rules/eval-criteria.md](../../rules/eval-criteria.md) ·
[rules/implementation.md](../../rules/implementation.md)
