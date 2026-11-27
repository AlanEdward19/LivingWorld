# docs/ — contexto de domínio (para HUMANOS)

Regras para a IA moram em `rules/`. Aqui é o **porquê** do domínio: os modelos que os
sistemas implementam. O `AGENTS.md` só aponta para cá.

> **Progressive loading**: abra **só** o doc que a tarefa exige. Não leia a pasta inteira.

## Quick reference — quando ler cada doc
| Você vai mexer em… | Leia |
|---|---|
| Relógio do mundo, ticks, agendamento de eventos | [time-and-ticks.md](domain/time-and-ticks.md) |
| Tempo em milissegundos, Δ variável, contrato v2 (ADR-0050) | [time-in-milliseconds.md](domain/time-in-milliseconds.md) |
| Tempo real com jogador, ritmo por mundo, passo fino dos NPCs, dormir e acelerar (AD-197c) | [real-time-rhythm.md](domain/real-time-rhythm.md) |
| Combate do jogador: alcance em metros, recarga, esquiva, bloqueio e reação do NPC (#234) | [player-combat.md](domain/player-combat.md) |
| Andar do jogador: o motor anda o rumo mantido, comando numerado, reconciliação e máscara de chão (#248) | [player-walk.md](domain/player-walk.md) |
| Casa de verdade: parede em aresta, cômodos por função como catálogo do cenário, portas e móveis no cômodo certo (#226) | [house-rooms.md](domain/house-rooms.md) |
| Metas que nascem do mundo, habilidades que sobem com o uso e diário do jogador (#235) | [player-goals.md](domain/player-goals.md) |
| Extração: a madeira sai aos poucos de uma árvore que é estado finito do mundo; jogador e NPC usam a mesma fonte (#243) | [extraction.md](domain/extraction.md) |
| Quanto detalhe simular por região, agregação, materialização de NPC | [simulation-lod.md](domain/simulation-lod.md) |
| Identidade, atributos, personalidade, necessidades, habilidades | [npc.md](domain/npc.md) |
| Decisão do NPC no dia a dia (utility AI, rotina, objetivos) | [behavior.md](domain/behavior.md) |
| Doença de época, contágio, doente de cama, causas de morte | [disease.md](domain/disease.md) |
| Relações, casamento, reprodução, hereditariedade, genoma | [genetics-and-family.md](domain/genetics-and-family.md) |
| Hormônios, puberdade, ciclo, sexo fora da união, doença genética, contracepção | [hormones-and-reproduction.md](domain/hormones-and-reproduction.md) |
| Freio preventivo: casar e 1º filho por meios, ajuda da família, esmola racionada, emigração | [population-pressure.md](domain/population-pressure.md) |
| Terra finita, braços que a terra paga, ceifa da vila toda | [land-and-harvest.md](domain/land-and-harvest.md) |
| Furto, briga, homicídio, boato, denúncia, julgamento, punição, herança | [crime-and-justice.md](domain/crime-and-justice.md) |
| Memória operacional/episódica/semântica/social, compactação, crenças | [memory.md](domain/memory.md) |
| Recursos, produção, empregos, salários, preços, comércio | [economy.md](domain/economy.md) |
| Cidades, edifícios, crescimento, migração, fundação de assentamentos | [cities.md](domain/cities.md) |
| Ruas, lotes, quintal, caminho e superfície de rua (AD-173) | [city-layout.md](domain/city-layout.md) |
| Toda construção é obra: casa, oficina, jornal que disputa a mão (AD-184) | [build-works.md](domain/build-works.md) |
| Material de verdade no canteiro e o dia perdido pelo tempo (AD-194) | [work-material.md](domain/work-material.md) |
| Bairro denso: sujeira da casa, higiene na doença, como cada um vê o bairro, onde morar (AD-192) | [dense-quarter.md](domain/dense-quarter.md) |
| Depois do turno: casa, largo, conhecido, passeio; moradores em células distintas; "na cidade · na rua" (#242) | [after-work-outings.md](domain/after-work-outings.md) |
| Onde cada trabalhador fica ao trabalhar: posto, campo da lavoura, lotação por célula (#223) | [work-stands.md](domain/work-stands.md) |
| Mapa, regiões, biomas, terreno, camadas de visualização | [world-map.md](domain/world-map.md) |
| Estações, temperatura, tempo do dia (chuva, neve, lama, seca, luz) e o que ele muda | [climate.md](domain/climate.md) |
| Event log, linha do tempo, dinastias, crônicas | [history.md](domain/history.md) |
| Degradação do passado, relatos, livros, mito, cânone limitado | [historical-memory.md](domain/historical-memory.md) |
| Montagem de contexto, prompt, DTO de saída, validação | [llm-contract.md](domain/llm-contract.md) |
| Cultura, conhecimento, tecnologia, política | [society.md](domain/society.md) |
| Invenção por composição física: materiais, design, avaliador, difusão | [invention.md](domain/invention.md) |
| Espólio de quem morre longe de casa: busca pela casa, furto, estrago | [leavings.md](domain/leavings.md) |
| O que o cliente vê de cada escopo e a ficha genérica (`/entities`): projeção do observável | [observable-projection.md](domain/observable-projection.md) |
| Cérebro legível com ganhos herdados e aprendidos (experimento atrás de flag) | [brain.md](domain/brain.md) |
| O que o cliente Godot ainda não recebe do servidor (etapa 2 do ADR-0047) | [cliente-lacunas-etapa-2.md](cliente-lacunas-etapa-2.md) |

### Escopo extra — spec, bloqueado até a Fase 8 fechar
| Você vai mexer em… | Leia |
|---|---|
| Mutantes, magos, poderes, custo e falha de potência | [powers.md](domain/powers.md) |
| Herança dos poderes (genótipo, Mendel, penetrância, custo) | [powers-genetics.md](domain/powers-genetics.md) |
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
