# ADR-0004: Interface `ILlmProvider` agora, provider concreto na Fase 10

- **Status**: aceito
- **Data**: 2026-07-26
- **Decisores**: Alan

## Contexto
A LLM só entra na Fase 10. Escolher provider hoje significa escolher com base em preço,
qualidade e ferramental de 2026 para um código que só roda depois de nove fases. Ao mesmo
tempo, a fronteira precisa existir desde já: `LivingWorld.AI` não pode escrever no mundo,
e todo caminho que usa LLM precisa de fallback determinístico.

## Decisão
Vamos definir agora apenas o **contrato**: `ILlmProvider` recebe um contexto somente-leitura
e devolve um DTO tipado e validável. Duas implementações acompanham o contrato desde a
Fase 0/10:

- `FakeLlmProvider` — determinística, usada nos testes e no `verify.sh`.
- `NullLlmProvider` — sempre devolve o fallback determinístico do motor.

O provider real (Ollama local, Claude API, ou ambos) é escolhido na Fase 10, num ADR novo.

## Alternativas consideradas
- **Ollama local agora** — casa com "LLM local" do escopo e não tem custo por token, mas
  fixa uma dependência de runtime nove fases antes de ser exercida.
- **Claude API agora** — melhor qualidade de diálogo, mas custo por token vira restrição
  de design (cache, batch, orçamento) que ainda não dá para dimensionar sem uso real.

## Consequências
- **Positivas**: a fronteira arquitetural (`rules/llm-boundary.md`) vale desde o dia um;
  os testes nunca dependem de rede; trocar de provider é trocar uma implementação.
- **Negativas / trade-offs**: risco de a interface não caber bem no provider escolhido
  depois — mitigado por mantê-la mínima (um método, um DTO) até haver uso real.
- **Follow-ups**: ADR-0008 na Fase 10 escolhendo o provider e registrando custo/latência.
