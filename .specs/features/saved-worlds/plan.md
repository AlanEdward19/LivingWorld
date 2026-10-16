# Mundos salvos [Estado: Verificado (2026-10-16)]

## Problem

Criar outro mundo perde o acesso ao anterior; o usuário não consegue recuperar sua partida.

## Flow

Menu Godot → API de mundos → snapshots do PersistentWorldRunner existente em SQLite por mundo → WorldHost existente; relógio pausado na troca.

## Impact

- Persistência por mundo e menu de recuperação; seed duplicada passa a retornar 409, aprovado pelo usuário.

## Relations

Um catálogo possui vários mundos; cada mundo possui seu banco e seus snapshots, sem compartilhamento entre mundos.

## Surface

| Route | In | Out | Status |
| --- | --- | --- | --- |
| GET /worlds | — | lista de metadados | 200 |
| POST /worlds/{worldId}/load | id | mundo atual | 200, 404 |
| DELETE /worlds/{worldId} | id | vazio | 204, 404, 409 |
| POST /worlds/create | cenário e nome existentes | identidade existente | 200, 400, 409 |

## Landing

| One-way door | Literal shape | Alternative rejected |
| --- | --- | --- |
| persistência por mundo | worlds/<worldId>.db e identidade por seed | tabela única e identidade por criação: rejeitadas pelo usuário; ADR-0035 |

## Criteria

Acceptance Criteria:

1. WHEN cria A, avança e cria B THEN GET /worlds SHALL listar ambos e carregar A SHALL restaurar o mesmo hash canônico.
2. IF mundo não existe THEN load SHALL responder 404 sem alterar hash ou mundo atual.
3. IF apaga mundo atual THEN DELETE SHALL responder 409; outro SHALL responder 204 e desaparecer; ausente SHALL responder 404.
4. WHEN cria seed já salva THEN create SHALL responder 409 sem sobrescrever nenhum mundo.
5. WHEN reinicia o servidor THEN catálogo e mundo atual SHALL sobreviver, incluindo incorporação do banco legado.
6. WHEN o usuário abre Carregar mundo THEN o cliente SHALL listar, carregar e confirmar exclusão, mostrando vazio, carregamento e erro.
7. The persistência SHALL ocorrer fora do tick, mantendo o interceptor existente verde.
8. The mudanças SHALL passar verify.sh, client-test.sh e sensores de desempenho antes/depois.

## Out of scope

Telas entregues pela #59, modelos de famílias e plantas da #46.

## Assumptions

Open questions: none - decisões confirmadas pelo usuário.

## Observable

| Surface | Decision | Landing |
| --- | --- | --- |
| API | status e identidade | AC 1–5 |
| menu | vazio, carregamento, erro e confirmação destrutiva | AC 6 |
| API | autorização, versão e limite | existing - mesma política dos endpoints locais de criação |

## Sources

- Issue #67, ADR-0035.
- Decisões explícitas do usuário em 2026-10-15.

## Performance evidence

Criação de 6.000 NPCs, 3 pares de processos alternados, 3 amostras por processo:
medianas 1515,822 → 1516,206 ms (+0,0253%; limite +5%).
Reprodução: `bash scripts/compare-creation-cost.sh <checkout-develop>`.
Godot 500 NPCs: medianas 75,4 → 76,2 FPS, 3 rodadas por braço.

Motor, 3 pares alternados (máquina com carga): CPU 1,121 → 0,844 µs/NPC-tick;
alocação 255.345 → 256.189 B/tick (+0,33%); limites +3%/+2% preservados.
Amostras CPU base: 0,872 / 1,121 / 1,209; entrega: 0,970 / 0,805 / 0,844.
Reprodução: `bash scripts/compare-simulation-cost.sh <checkout-develop>`.

Verifier, medição serial em 2ab9fcb, código produtivo preservado em 18953eb:
CPU 1,019 → 1,040 µs/NPC-tick (+2,06%); alocação 255.351 → 256.245 B/tick (+0,35%).
Criação 6.000: 1955,349 → 1713,986 ms; Godot: mediana 73,7 → 74,1 FPS.
