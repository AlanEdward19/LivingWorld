# ADR-0016: Provider real da LLM — Ollama local, `qwen3.5:4b` Q4_K_M

- **Status**: aceito
- **Data**: 2026-08-03
- **Decisores**: Alan

## Contexto
ADR-0004 definiu o contrato (`ILlmProvider`) e adiou a escolha do provider real para a
Fase 11 (o roadmap da fase referencia esta decisão como "ADR-0007", mas esse número já foi
ocupado por ADR-0007-memoria-historica-degradavel.md antes desta fase rodar — a decisão é
registrada aqui como ADR-0016, próximo número livre). `FakeLlmProvider` continua sendo o
único provider usado em `scripts/test.sh`/`scripts/verify.sh`; esta escolha vale só para
diálogo real fora do gate.

Hardware alvo: GPU local de 6 GB de VRAM (RTX 3050). Job da LLM aqui é estreito por design
(fronteira `rules/llm-boundary.md`): interpretar fala do jogador, responder como o NPC,
escolher intenção dentro de um enum pequeno, preencher um DTO validável — o motor faz toda
validação/aplicação (T5/T6 desta fase). Não precisa de um modelo grande ou agente autônomo.

## Decisão
Provider de produção inicial: **Ollama local**, modelo **`qwen3.5:4b` (Q4_K_M)** — ~3,4 GB,
4,66B parâmetros, cabe com folga nos 6 GB de VRAM (deixa espaço pra KV cache/contexto/
overhead do runtime), suporte multilíngue incluindo português, tool use e geração
estruturada. Configuração de baseline:

- `think: false`, `temperature: 0`, `seed` fixa, `num_ctx: 4096`, `num_predict: 192–256`.
- Saída forçada via JSON Schema no `format` do Ollama (`dialogue`, `emotion` enum,
  `proposedActions` com `type`/`magnitude`) — não substitui o parser tipado + validação de
  schema + `AllowedActions` que o motor já faz (T5/T6); é só uma primeira peneira mais barata
  do lado do provider.
- Fixar sempre tag e, se possível, digest do modelo (nunca `qwen3.5:latest`, que hoje aponta
  pra variante 9B de ~6,6 GB — maior que a VRAM disponível).
- Perfil opcional de qualidade: `qwen3:8b` (~5,2 GB) — fica perto do limite de 6 GB, latência
  menos previsível (camadas podem cair pra CPU); entra só depois de benchmark, não como
  baseline.
- `FakeLlmProvider` continua sendo o provider de todos os testes e gates — nenhuma mudança
  aqui.

### Determinismo e replay
Mesmo com `temperature: 0` e seed fixa, inferência local **não é** parte do estado canônico
reproduzível entre versões do Ollama, drivers, CPU vs GPU, quantizações ou atualizações de
modelo — não dá pra assumir bit-exact reproducibility da LLM em si. Para replay, o motor
grava como input externo (fora do hash canônico, já que `ILlmProvider`/`LlmResponse` nunca
tocam o mundo antes da validação — T5) o DTO validado e aprovado, mais metadados:
`ProviderId`, `ModelId`, `ModelDigest`, `PromptTemplateVersion`, `SchemaVersion`,
`RequestId`, `SessionId`, `TurnId`, `AcceptedDtoHash`. Replay reaplica o DTO aprovado — nunca
chama a LLM de novo — então o hash canônico nunca depende da inferência real.

## Alternativas consideradas
- **`qwen3:8b` como baseline** — melhor qualidade, mas ~5,2 GB fica perto demais do limite de
  6 GB de VRAM; contexto + desktop + overhead do runtime empurram pra latência imprevisível
  (camadas caindo pra CPU/RAM). Vira perfil opcional pós-benchmark, não baseline.
- **`qwen3.5:latest`** — descartado: alias hoje aponta pra variante 9B (~6,6 GB), maior que a
  VRAM disponível; usar tag/digest fixos evita esse risco de regressão silenciosa.
- **Claude API como baseline** — melhor qualidade de diálogo (ADR-0004 já considerou), mas
  custo por token e dependência de rede não combinam com o objetivo de baseline offline/
  previsível de custo para desenvolvimento e CI local; fica como opção b) explicitamente
  habilitável depois, não descartada — só não é o padrão.

## Consequências
- **Positivas**: baseline roda inteiro local, sem custo por token, previsível em CI/dev;
  contrato `ILlmProvider` não muda; `FakeLlmProvider` continua isolando todo o gate de rede
  real (T9, bloqueio de egress).
- **Negativas / trade-offs**: qualidade de diálogo de um modelo 4B é o teto até virar
  `qwen3:8b`/provider remoto; determinismo bit-exact da LLM não existe — só o DTO validado e
  aprovado é reproduzível, nunca a chamada em si.
- **Follow-ups**: implementar `OllamaLlmProvider` concreto (fora do escopo desta fase —
  `NullLlmProvider`/`FakeLlmProvider` seguem sendo os únicos providers com código; este ADR
  registra a decisão, não a implementação); medir custo por interação e latência p95 reais
  quando o provider concreto existir; avaliar `qwen3:8b` como perfil de qualidade após
  benchmark.
