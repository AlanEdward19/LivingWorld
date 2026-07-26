# Fase 14 — Mapa visual

**Objetivo**: um cliente web mostra o mundo que já existe — camadas sobre o grid da Fase 2,
navegação do mapa-múndi até o NPC, tudo em **leitura**. Recebe o que saiu da Fase 2 para
tirar o React do caminho crítico do objetivo #1. Depende da Fase 8 (cidades e migração).

## Tasks
1. **Endpoints REST de mapa** na `Api`, somente leitura: listar regiões, detalhar região,
   detalhar nível filho, consultar camada. A API não escreve no mundo.
2. **Camadas de visualização** como projeções sobre o mesmo grid: terreno, biomas, rios,
   montanhas, recursos, estradas, fronteiras, reinos, cidades, aldeias, rotas, migrações,
   conflitos, clima. Camada é leitura derivada — não duplica o estado do mundo.
3. **Hierarquia de drill-down**: mundo → região → cidade → bairro → local → NPCs presentes.
   Cada nível conhece o pai e lista os filhos; profundidade fixa e declarada.
4. **Cliente React+TS**: renderiza o mapa, troca de camada, seleciona célula/local e faz
   drill-down até a lista de NPCs presentes.
5. **Geração de tipos TS a partir do OpenAPI** (ADR-0003): os tipos do cliente são artefato
   gerado, versionado, nunca escrito à mão.
6. **Estender os scripts** `build.sh`, `lint.sh`, `test.sh` e `verify.sh` para o ramo web:
   um comando só continua fechando o gate dos dois lados.

## Critérios de verificação
- **Tipos gerados não divergem do DTO**: `verify.sh` regenera os tipos TS a partir do
  OpenAPI e roda `git diff --exit-code` sobre o diretório gerado. Alterar um DTO sem
  regenerar reprova o gate — sem depender de alguém lembrar de rodar o gerador.
- **Nenhuma rota de mapa altera o mundo**: o teste enumera as rotas via `EndpointDataSource`,
  chama **cada uma** com payload sintético e compara o hash canônico antes/depois. O teste
  **falha se alguma rota enumerada não estiver na lista coberta** — rota nova sem cobertura
  reprova, em vez de passar despercebida.
- **Toda camada declarada é navegável**: o teste enumera o catálogo de camadas e exige, para
  cada uma, endpoint respondendo e renderer registrado no cliente. Camada nova sem os dois
  reprova.
- **Drill-down é total**: para **todos** os NPCs vivos do cenário de teste (não amostra),
  sobe-se local → bairro → cidade → região → mundo sem encontrar pai nulo.
- **O gate do ramo web sabe reprovar**: os mutantes da Fase 0 ganham um irmão no lado web —
  um teste de cliente com assert invertido faz `bash scripts/verify.sh` sair ≠ 0.

## Fora do escopo
O **objetivo #2** (selecionar um NPC vivo e ver identidade, família, profissão, atributos,
rotina e memórias) é atendido por CLI/API na **Fase 8** e **não depende desta fase**. Se
esta fase deslizar indefinidamente, nenhum objetivo técnico fica em aberto.
Escrita pela UI (a API de mapa é leitura), cliente 3D, personagens, voz e animação são
Fase 13. Regras de simulação novas não entram aqui: se a visualização revelar uma mecânica
faltando, ela vira task da fase dona da mecânica.

## Ver também
[phase-02-geography.md](phase-02-geography.md) ·
[phase-08-cities.md](phase-08-cities.md) ·
[world-map.md](../domain/world-map.md) ·
[ADR-0003](../adr/ADR-0003-cliente-web-react-ts.md) ·
[rules/eval-criteria.md](../../rules/eval-criteria.md)
