# docs/adr/ — Architecture Decision Records (para HUMANOS)

Uma decisão por arquivo: `ADR-NNNN-<slug>.md`. ADR é **imutável** — para reverter, crie
um novo com status "substitui ADR-NNNN". O `AGENTS.md` só aponta para cá; não copie
conteúdo de ADR para dentro dele.

**Gatilho**: dependência nova ou decisão de arquitetura. Use `ADR.template.md`.

| ADR | Decisão | Status |
|---|---|---|
| [0001](ADR-0001-monolito-modular-dotnet.md) | Monólito modular em .NET 10, uma solution, camadas por projeto | aceito |
| [0002](ADR-0002-sqlite-agora-postgres-depois.md) | SQLite agora, Postgres depois — sem recurso exclusivo de SQLite | aceito |
| [0003](ADR-0003-cliente-web-react-ts.md) | Cliente web em React + TypeScript, separado da API | substituído pelo 0022 |
| [0004](ADR-0004-abstracao-de-provider-llm.md) | Interface `ILlmProvider` agora, provider concreto na Fase 11 | aceito |
| [0005](ADR-0005-simulacao-deterministica-semeada.md) | Simulação determinística com RNG semeado por stream | aceito |
| [0006](ADR-0006-snapshot-mais-event-log.md) | Persistência por snapshot periódico + event log append-only | aceito |
| [0007](ADR-0007-memoria-historica-degradavel.md) | História como relato degradável (especializa a retenção do 0006) | aceito |
| [0008](ADR-0008-ramificacao-como-modelo-temporal.md) | Ramificação como único modelo de viagem no tempo — sem paradoxo, sem fusão | aceito |
| [0009](ADR-0009-branchid-no-esquema-desde-a-fase-3.md) | `BranchId` no esquema e no hash desde a Fase 3 | aceito |
| [0010](ADR-0010-potencia-como-modificador-unificado.md) | Potência (mutante, deus, mago, alien) como modificador unificado | aceito |
| [0011](ADR-0011-primitivo-unico-de-resolucao.md) | Um primitivo de resolução (o "d20"), com variância declarada por domínio | aceito |
| [0012](ADR-0012-catchup-preguicoso-de-branch.md) | Catch-up preguiçoso de branch dormente — determinismo compra preguiça | aceito |
| [0013](ADR-0013-emergencia-aberta-motor-estrutura-llm-nome.md) | Emergência aberta: o motor cria a estrutura, a LLM só nomeia | aceito |
| [0014](ADR-0014-canonico-vs-volatil.md) | Canônico se alimenta uma decisão; volátil se é recomputável ou cosmético | aceito |
| [0015](ADR-0015-pausa-global-e-auditoria-de-admin.md) | Pausa global de admin; auditoria de leitura separada da intervenção | aceito |
| [0016](ADR-0016-provider-llm-real.md) | Ollama `qwen3.5:4b` como provider LLM real | aceito |
| [0017](ADR-0017-identidade-publica-do-mundo.md) | `WorldId` como hash puro da seed (nunca persistido); nome do mundo como campo volátil novo | aceito |
| [0018](ADR-0018-endereco-espacial-com-andar-e-escala-de-dominio.md) | Endereço espacial (`SpaceKind`+`FloorLevel`+`CellCoord`) e escala World/City/Building como dado de domínio, não do cliente | aceito |
| [0019](ADR-0019-cidade-fundida-como-tombstone-historico.md) | Cidade fundida sai da simulação ativa, mas permanece como tombstone histórico | aceito |
| [0020](ADR-0020-mapa-inicial-dimensionado-pela-populacao.md) | Mapa procedural inicial reserva espaço físico conforme a população | aceito |
| [0021](ADR-0021-locomocao-e-construtos-extraordinarios.md) | Potência estende movimento e ocupação existentes | aceito |
| [0022](ADR-0022-godot-substitui-cliente-web.md) | Godot como motor gráfico; cliente web aposentado do gate | aceito |
| [0023](ADR-0023-snapshot-binario-posicional.md) | Snapshot binário posicional, base completa mais diffs por campo | aceito |
| [0024](ADR-0024-familias-autoradas-no-cenario.md) | Famílias autoradas no cenário (`AuthoredHouseholds`), só ao criar mundo | aceito |
| [0025](ADR-0025-godot-substitui-cliente-3d-da-fase-14.md) | Godot substitui o cliente 3D da Fase 14; Unreal e voz adiados | aceito |
| [0026](ADR-0026-rota-decision-sandbox.md) | Rota `POST /npcs/{id}/decision-sandbox` ("E se…?", só leitura) | aceito |
| [0027](ADR-0027-identidade-do-mundo-e-pausa-publica.md) | `GET /world/info`, `periodId` na criação e pausa de admin visível a todos | aceito |
| [0028](ADR-0028-desenvolvimento-por-npc-no-snapshot.md) | Estado de desenvolvimento por NPC no snapshot; ontogenia ligada por padrão | aceito |
| [0029](ADR-0029-janela-critica-e-exposicao-binaria.md) | Exposição binária, janela crítica por teto e aquisição sem sorteio | aceito |
| [0030](ADR-0030-idioma-de-criacao-e-regressao-por-doenca.md) | Idioma de criação, regressão por doença e adultos no pool | aceito |
| [0031](ADR-0031-acolhimento-de-orfaos-e-criacao-adotiva.md) | Acolhimento de órfãos, choro atendido, exposição graduada e criação adotiva | aceito |
| [0032](ADR-0032-aparencia-herdada-e-autoria-de-pessoa.md) | Aparência herdada opt-in por período, habilidades iniciais autoradas, consumidor no cortejo | aceito |
| [0033](ADR-0033-criacao-ao-vivo-de-familia-e-local-de-trabalho.md) | Criação ao vivo de família e local de trabalho por rotas de autoria de admin | aceito |
| [0034](ADR-0034-catalogo-de-objetos-no-cenario.md) | Catálogo de objetos no cenário (formato versionado, interior servido por id) | aceito |
| [0035](ADR-0035-mundos-salvos-por-arquivo.md) | Mundos salvos em SQLite por mundo, identidade por seed e troca pausada | aceito |
| [0036](ADR-0036-diversidade-genetica-calibrada.md) | Diversidade genética calibrada e correções demográficas de FAM-32 (antes 0034 nesta branch) | aceito |
| [0037](ADR-0037-uso-de-objeto-pose-e-ponto-de-uso.md) | Uso de objeto: pose e ponto de uso declarados, objeto em uso informado pelo servidor | aceito |
