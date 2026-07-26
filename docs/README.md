# docs/ — contexto de domínio (para HUMANOS)

Regras para a IA moram em `rules/`. Aqui é o **porquê** do domínio: os modelos que os
sistemas implementam. O `AGENTS.md` só aponta para cá.

> **Progressive loading**: abra **só** o doc que a tarefa exige. Não leia a pasta inteira.

## Quick reference — quando ler cada doc
| Você vai mexer em… | Leia |
|---|---|
| Relógio do mundo, ticks, agendamento de eventos | [time-and-ticks.md](domain/time-and-ticks.md) |
| Quanto detalhe simular por região, agregação, materialização de NPC | [simulation-lod.md](domain/simulation-lod.md) |
| Identidade, atributos, personalidade, necessidades, habilidades | [npc.md](domain/npc.md) |
| Decisão do NPC no dia a dia (utility AI, rotina, objetivos) | [behavior.md](domain/behavior.md) |
| Relações, casamento, reprodução, hereditariedade, genoma | [genetics-and-family.md](domain/genetics-and-family.md) |
| Memória operacional/episódica/semântica/social, compactação, crenças | [memory.md](domain/memory.md) |
| Recursos, produção, empregos, salários, preços, comércio | [economy.md](domain/economy.md) |
| Cidades, edifícios, crescimento, migração, fundação de assentamentos | [cities.md](domain/cities.md) |
| Mapa, regiões, biomas, terreno, camadas de visualização | [world-map.md](domain/world-map.md) |
| Event log, linha do tempo, dinastias, crônicas | [history.md](domain/history.md) |
| Degradação do passado, relatos, livros, mito, cânone limitado | [historical-memory.md](domain/historical-memory.md) |
| Montagem de contexto, prompt, DTO de saída, validação | [llm-contract.md](domain/llm-contract.md) |
| Cultura, conhecimento, tecnologia, política | [society.md](domain/society.md) |

### Escopo extra — spec, bloqueado até a Fase 8 fechar
| Você vai mexer em… | Leia |
|---|---|
| Mutantes, magos, poderes, custo e falha de potência | [powers.md](domain/powers.md) |
| Deuses, culto, economia de crença, cisma | [divinity-and-belief.md](domain/divinity-and-belief.md) |
| Viagem no tempo, ramificação, âncora e colapso de branch | [timelines.md](domain/timelines.md) |
| Sistema estelar, órbitas, contato alienígena, colônias | [cosmos.md](domain/cosmos.md) |

## Decisões de arquitetura
Uma decisão por arquivo em [adr/](adr/). Nova dependência ou escolha estrutural → novo ADR.

## Roadmap
Fases e critérios de aceite: [`../ROADMAP.md`](../ROADMAP.md) → `roadmap/phase-NN-*.md`.

## Os 12 princípios do projeto
1. O mundo continua sem o jogador. 2. A LLM não simula o cotidiano. 3. A simulação é a
fonte da verdade. 4. NPCs têm conhecimento limitado. 5. Eventos geram consequências.
6. A história é emergente. 7. A escala determina o nível de detalhe. 8. O sistema suporta
milhares de habitantes. 9. Jogadores influenciam, não controlam. 10. A arquitetura permite
web, Unreal e outros clientes. 11. Sistemas simples produzem comportamento complexo.
12. A LLM enriquece a experiência, não sustenta a simulação.
