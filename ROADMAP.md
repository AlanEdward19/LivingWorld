# ROADMAP — Living World

Índice das fases. **Carregue só o arquivo da fase em que está trabalhando.**
Cada `docs/roadmap/phase-NN-*.md` tem: objetivo, tasks acionáveis, critérios de
verificação (o que o gate precisa provar) e o que fica **explicitamente fora**.

## Objetivos técnicos (a régua)
| # | Objetivo | Fecha na fase |
|---|---|---|
| 1 | 100 NPCs numa vila medieval por **100 anos sem LLM**, com famílias, profissões, economia, nascimentos, mortes e habilidades preservados | 7 |
| 2 | Selecionar qualquer NPC vivo e ver identidade, família, profissão, atributos, rotina e memórias | 8 |
| 3 | Jogador conversa com NPC via LLM, **sem** que a LLM altere o mundo | 10 |
| 4 | Cidades crescem, encolhem e dão origem a novos assentamentos | 8 |

## Trilhas
O número é **identidade**, não ordem. Só a trilha núcleo é sequencial.

| Trilha | Fases | Regra |
|---|---|---|
| **Núcleo** | 0–8 | Caminho crítico. Sequencial. Fecha os 4 objetivos. Nada entra na frente |
| Clientes | 13, 14 | Depois da 8. Independentes entre si |
| Mundo vivo | 9–12 | Depois da 8. História, LLM, narrativa, períodos |
| Extraordinário | 15–19 | Depois da 8. Potência → divindade → tempo → cosmos → trânsito |
| Realismo humano | 20–22 | Depois da 7. Ontogenia, imperfeição, intriga |
| Sistêmicas | 23–25 | Emergência exige 12; jogadores exige 10; console exige 17 |
| Espectador | 26 | Última. Cinemática exige 9 (relato) e 11 (ancoragem) |

## Fases
| # | Fase | Entrega | Status |
|---|---|---|---|
| 0 | [Fundação](docs/roadmap/phase-00-foundation.md) | Solution, camadas, gate verde, testes de arquitetura | fechada |
| 1 | [Motor de tempo](docs/roadmap/phase-01-time.md) | Calendário, ticks, scheduler, pausa/velocidade, snapshot, hash | fechada |
| 2 | [Geografia mínima](docs/roadmap/phase-02-geography.md) | Regiões, células, terreno, bioma, recursos, custo de deslocamento — só dados | fechada |
| 3 | [População básica](docs/roadmap/phase-03-population.md) | NPC, idade, sexo, saúde, família, nascimento, morte | pendente |
| 4 | [Necessidades e rotina](docs/roadmap/phase-04-needs.md) | Fome, sono, trabalho, moradia, deslocamento, utility AI | pendente |
| 5 | [Economia](docs/roadmap/phase-05-economy.md) | Recursos, produção, estoque, consumo, emprego, salário, preço | pendente |
| 6 | [Habilidades](docs/roadmap/phase-06-skills.md) | Experiência, ensino, profissões, progressão | pendente |
| 7 | [Relações e famílias](docs/roadmap/phase-07-family.md) | Confiança, atração, casamento, reprodução, hereditariedade | pendente |
| 8 | [Cidades](docs/roadmap/phase-08-cities.md) | Crescimento, edifícios, migração, fundação de assentamentos, inspeção por CLI/API | pendente |
| 9 | [História degradável](docs/roadmap/phase-09-history.md) | Fato, relato, distorção por transmissão, cânone limitado, verdade vs crença | pendente |
| 10 | [Interação com LLM](docs/roadmap/phase-10-llm.md) | Contexto, diálogo, validação, memória, relação | pendente |
| 11 | [Narrativa](docs/roadmap/phase-11-narrative.md) | Resumos, jornais, rumores, biografias, crônicas | pendente |
| 12 | [Múltiplos períodos](docs/roadmap/phase-12-periods.md) | Módulos de conteúdo: pré-histórico, moderno, futurista, criaturas | pendente |
| 13 | [Unreal](docs/roadmap/phase-13-unreal.md) | Cliente 3D, personagens, voz, animação | pendente |
| 14 | [Mapa visual](docs/roadmap/phase-14-map-visual.md) | Cliente React+TS, camadas, drill-down, tipos gerados do OpenAPI | pendente |
| 15 | [Potência](docs/roadmap/phase-15-powers.md) | Mutantes, magos, artefatos: modificador unificado com custo e falha | spec |
| 16 | [Divindade](docs/roadmap/phase-16-divinity.md) | Deuses, culto, economia de crença, cisma, deus falso | spec |
| 17 | [Linhas temporais](docs/roadmap/phase-17-timelines.md) | Ramificação, inércia histórica, âncora e coleta de branch | spec |
| 18 | [Cosmos e contato](docs/roadmap/phase-18-cosmos.md) | Sistema estelar, órbitas, aliens como cultura, colônias | spec |
| 19 | [Trânsito interdimensional](docs/roadmap/phase-19-interdimensional.md) | Volta à linha de origem, catch-up preguiçoso de branch dormente | spec |
| 20 | [Ontogenia](docs/roadmap/phase-20-ontogeny.md) | Nascer sem saber nada, marcos, exposição, janelas críticas | spec |
| 21 | [Imperfeição](docs/roadmap/phase-21-imperfection.md) | Defeitos, doenças, moral emergente, orientação e ocultação | spec |
| 22 | [Intriga](docs/roadmap/phase-22-intrigue.md) | Segredo, chantagem, traição, rancor, humor, fofoca, facção | spec |
| 23 | [Emergência aberta](docs/roadmap/phase-23-emergence.md) | Raças, tecnologias e ideologias novas sem catálogo | spec |
| 24 | [Jogadores](docs/roadmap/phase-24-players.md) | Encarnação, offline vira desaparecimento, espectador, multiplayer | spec |
| 25 | [Console e modo god](docs/roadmap/phase-25-console.md) | Pausa, velocidade, busca, eventos marcantes, reescrita + rebuild | spec |
| 26 | [Motor cinematográfico](docs/roadmap/phase-26-cinematics.md) | Assistir a uma vida: texto → 2D → caminho para 3D | spec |

## Regras do roadmap
- Uma fase só fecha com `bash scripts/verify.sh` em 0 **e** os critérios da fase provados
  por teste — não por inspeção visual.
- Fase fechada → commit `feat(phase-NN): <resumo>` (ver política em `AGENTS.md`).
- Mudou o escopo de uma fase? Atualize o `.md` dela e registre em `STATE.md`. Se a mudança
  for de arquitetura ou dependência, é ADR.
- **Objetivo #1 é a régua.** Se uma task não aproxima o mundo de rodar 100 anos coerente,
  ela provavelmente pertence a uma fase posterior.
- Caminho crítico: **0–8**, incluindo a 2 — que agora é só a geografia mínima que a Fase 8
  consome. A 7 fecha o objetivo #1; a 8 fecha os objetivos #2 e #4.
- 9–14 podem deslizar sem travar objetivo nenhum. O cliente web foi para a Fase 14 e o
  objetivo #2 é atendido por CLI/API na Fase 8.
- **Status `spec` significa bloqueada.** Fases 15–26 têm objetivo, tasks e intenção de
  critério, mas os critérios finais só são escritos sob `rules/eval-criteria.md` quando a
  fase é ativada — e nenhuma delas começa antes da Fase 8 fechar. Ativar uma fase `spec`
  fora de ordem é decisão de escopo: registre em `STATE.md` antes.
