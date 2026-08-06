# Fase 13 (Multiplos periodos) Specification

## Problem Statement
A Fase 3 já permite cenário como dado, mas o conceito de "período" ainda corre risco de virar catálogo fixo e bloquear emergência de profissões/habilidades novas. Precisamos tratar período como startpoint (viés inicial), mantendo o mundo vivo e evolutivo sem forks de código. Também falta um fluxo claro para cadastrar períodos personalizados no projeto e uma documentação para orientar uma IA externa a produzir definições válidas.

## Goals
- [ ] Período passa a ser startpoint dinâmico, não lista fixa de conteúdo.
- [ ] Profissões, habilidades e papéis sociais podem surgir, mudar e desaparecer em runtime por regras declaradas.
- [ ] Adicionar período novo exige apenas dados de cenário, sem alterar `src/`.
- [ ] API expõe rota para cadastrar período personalizado e reutilizá-lo como template oficial.
- [ ] Documentação operacional ensina uma IA externa (fora do projeto) a gerar `periodDefinition` válida.

## Out of Scope
| Feature | Reason |
| --- | --- |
| Criar mecânicas novas de simulação para suportar um período | Se precisar mecânica inédita, vira fase própria antes |
| Cliente 3D/voz/assets por período | Fase 14 |
| Cliente visual React para exploração de período | Fase 15 |
| LLM escrevendo estado do mundo diretamente | Violação da fronteira do motor (Fase 11 + rules/llm-boundary.md) |

## Assumptions & Open Questions
| Assumption / decision | Chosen default | Rationale | Confirmed? |
| --- | --- | --- | --- |
| Definição de período | Período é startpoint de vieses e regras de evolução, não catálogo imutável | Mantém dinamismo e evita hardcode histórico | y |
| Trilha de autoria por IA | P1 entrega documentação operacional para IA externa; o projeto só cadastra/valida períodos via API | Mantém escopo do projeto sem acoplar runtime de IA interna | y |
| Pacotes de referência | Medieval/pré-histórico/moderno/futurista/criaturas são exemplos, não whitelist | Evita dependência de lista fixa | y |
| Open questions | none | Direção de produto da fase está definida | n/a |

## User Stories
### P1: Período como startpoint dinâmico
**User Story**: Como designer do mundo, quero que período represente apenas vieses iniciais para que o conteúdo evolua durante a simulação.  
**Acceptance Criteria**:
1. WHEN um período é carregado THEN o sistema SHALL interpretá-lo como startpoint de distribuições/pesos/restrições/gatilhos.
2. WHEN ticks avançam THEN o sistema SHALL permitir nascimento, fusão, divisão e desaparecimento de profissões/habilidades conforme regras do cenário.
3. WHEN uma transformação geraria elemento fora do contrato THEN o sistema SHALL rejeitar a transformação com erro determinístico e referência de campo/regra.

### P1: Extensibilidade por dados
**User Story**: Como engenheiro, quero adicionar períodos sem alterar código para manter o motor único para qualquer época.  
**Acceptance Criteria**:
1. WHEN um período novo é adicionado THEN o sistema SHALL inicializar mundo válido sem editar arquivos `.cs`.
2. WHEN testes de arquitetura rodam THEN o sistema SHALL reprovar literais de período usados como decisão em `LivingWorld.Domain` e `LivingWorld.Simulation`.
3. WHEN catálogo ativo é consultado THEN o sistema SHALL retornar apenas elementos derivados do período + evolução do mundo.

### P1: Cadastro de período personalizado
**User Story**: Como operador do sistema, quero cadastrar períodos personalizados para usá-los como templates oficiais do projeto.  
**Acceptance Criteria**:
1. WHEN `POST /periods` recebe `periodDefinition` THEN o sistema SHALL validar schema, referências e invariantes antes de persistir.
2. WHEN `periodDefinition` é válido THEN o sistema SHALL registrar o período e disponibilizá-lo como template para criação de mundos.
3. WHEN `periodDefinition` é inválido THEN o sistema SHALL rejeitar cadastro com mensagem determinística apontando caminho/campo.
4. WHEN período está cadastrado THEN o sistema SHALL permitir inicialização por id registrado com o mesmo pipeline dos templates base.

### P1: Documentação para IA externa
**User Story**: Como operador de conteúdo, quero enviar um guia para uma IA fora do projeto para que ela produza períodos válidos.  
**Acceptance Criteria**:
1. WHEN `docs/domain/period-authoring.md` é entregue THEN o documento SHALL conter contrato canônico, schema, exemplos positivos/negativos e checklist de validação.
2. WHEN a IA externa seguir o guia THEN o payload gerado SHALL ser compatível com a rota de cadastro sem transformação manual ad hoc.
3. WHEN o guia for usado por humanos sem IA THEN ele SHALL continuar suficiente para autoria manual de períodos.

### P1: Determinismo e causalidade de vieses
**User Story**: Como mantenedor da simulação, quero provar que dinamismo de período não quebra determinismo nem causalidade.  
**Acceptance Criteria**:
1. WHEN o mesmo período e a mesma seed são executados no mesmo horizonte THEN o sistema SHALL produzir hash canônico idêntico.
2. WHEN períodos distintos rodam com a mesma seed e horizonte THEN o sistema SHALL produzir hashes diferentes.
3. WHEN um viés declarado é testado THEN o sistema SHALL usar par controle/tratamento com a mesma seed e confirmar direção esperada em múltiplas seeds.

### P2: Pacotes de referência como regressão
**User Story**: Como time de produto, quero manter períodos de referência para regressão sem transformá-los em contrato fixo do motor.  
**Acceptance Criteria**:
1. WHEN pacotes de referência são executados THEN o sistema SHALL mantê-los verdes como baseline de compatibilidade.
2. WHEN período fora dos pacotes é executado THEN o sistema SHALL tratar com o mesmo pipeline de validação e execução.

### P1: Habilidade como catálogo aberto (adicionada pós-T10, feedback do usuário)
**User Story**: Como designer do mundo, quero que habilidade seja catálogo aberto por dado — igual profissão — para que novas habilidades surjam/desapareçam por período sem editar `src/`.  
**Acceptance Criteria**:
1. WHEN um período declara uma habilidade nova (id + nome opcional) THEN o sistema SHALL aceitá-la sem exigir alteração em `LivingWorld.Domain`/`LivingWorld.Simulation`.
2. WHEN testes de arquitetura rodam THEN o sistema SHALL reprovar nome de habilidade usado como literal de decisão no motor (mesmo padrão de `PeriodArchitectureTests`/`PopulationArchitectureTests`).
3. WHEN uma regra do motor hoje depende de uma habilidade específica por identidade (ex.: multiplicador de tutoria por `Teaching`) THEN o sistema SHALL expressá-la como id declarado por regra de cenário, nunca enum fixo.

### P1: Leitura do catálogo ativo (adicionada pós-T10, feedback do usuário)
**User Story**: Como operador, quero ler quais profissões/habilidades estão ativas num período/mundo para inspecionar o que a simulação gerou.  
**Acceptance Criteria**:
1. WHEN o catálogo de um período é consultado THEN o sistema SHALL expor os ids (e nomes, quando declarados) de profissão e habilidade daquele período.
2. WHEN a consulta ocorre por API THEN o sistema SHALL responder num formato reaproveitando o padrão de resposta já usado por `GET /periods`.

## Edge Cases
- WHEN dois períodos definem aliases iguais para entidades semânticas diferentes THEN sistema SHALL rejeitar conflito no registro.
- WHEN período personalizado omite regra obrigatória de transformação THEN sistema SHALL falhar no validador antes da criação do mundo.
- WHEN evolução remove profissão em uso THEN sistema SHALL aplicar política declarada de migração/reclassificação sem quebrar integridade referencial.
- WHEN startpoint tenta injetar ação fora do motor permitido THEN sistema SHALL rejeitar definição sem efeito parcial.

## Requirement Traceability
| Requirement ID | Story | Status |
| --- | --- | --- |
| PERIOD-01..03 | Período como startpoint dinâmico | Pending |
| PERIOD-04..06 | Extensibilidade por dados | Pending |
| PERIOD-07..10 | Cadastro de período personalizado | Pending |
| PERIOD-11..13 | Documentação para IA externa | Pending |
| PERIOD-14..16 | Determinismo e causalidade de vieses | Pending |
| PERIOD-17..18 | Pacotes de referência como regressão | Pending |
| PERIOD-19..21 | Habilidade como catálogo aberto | Pending |
| PERIOD-22..23 | Leitura do catálogo ativo | Pending |

## Success Criteria
- [ ] Qualquer período novo pode ser adicionado como dados sem alteração de código de domínio/simulação.
- [ ] O mundo gera e transforma catálogos dinamicamente sem fugir do contrato do cenário.
- [ ] Período personalizado cadastrado por rota passa por validação automática reproduzível.
- [ ] Documentação permite geração de períodos por IA externa sem runtime de IA dentro do projeto.
- [ ] Determinismo e testes causais continuam válidos com períodos dinâmicos.
