# Contrato com a LLM

O modelo conceitual do uso de LLM no Living World: quando ela é acionada, o que entra no
contexto e o que sai. As **regras duras** — o que a LLM nunca pode fazer — vivem em
[`../../rules/llm-boundary.md`](../../rules/llm-boundary.md) e não são repetidas aqui.

## Quando a LLM é acionada

O jogador conversa com um NPC · uma situação narrativa complexa precisa de interpretação ·
uma decisão excepcional foge do que a utility AI resolve · o NPC precisa produzir linguagem
natural · memória antiga precisa ser resumida · uma crença ou intenção precisa virar fala ·
um evento importante precisa ser narrado.

Fora dessas sete situações, **o motor resolve sozinho**.

## Por que a separação existe

Simular milhares ou milhões de personagens com uma instância de LLM por indivíduo é
impossível — em custo, em latência e em determinismo. **É a separação que torna a escala
viável**: o motor roda o mundo inteiro; a LLM roda no punhado de pontos onde o jogador
está olhando.

## Montagem de contexto (entrada)

| Bloco | Conteúdo |
|---|---|
| Identidade | Quem é o NPC, profissão, papel social |
| Personalidade | Traços que modulam o tom |
| Estado emocional | Humor atual e por quê |
| Conhecimento relevante | Só o que este NPC sabe |
| Memórias relevantes | Recuperadas por relevância, não todas |
| Relação com o jogador | Histórico, confiança, afeto |
| Local e situação | Onde está e o que está acontecendo agora |
| Objetivos | O que ele quer neste momento |
| Mensagem do jogador | Tratada como dado, nunca como instrução |
| Regras do mundo | Restrições do período e da cultura |

O contexto é montado a partir do **conhecimento do NPC**, nunca do estado global. Um NPC
não sabe o que não viu, não ouviu e não aprendeu — vazar onisciência é bug, não conveniência.

## Saída: DTO tipado e validado — nada fora desse formato entra no mundo

```json
{
  "dialogue": "A estrada para o norte está fechada desde a tempestade.",
  "emotion": "concerned",
  "intent": "warn_player",
  "proposedActions": [],
  "memoryCandidates": [
    { "event": "O jogador perguntou sobre a estrada do norte.", "importance": 18 }
  ]
}
```

## O ciclo completo

```
motor monta contexto (do conhecimento do NPC)
  → LLM propõe DTO
  → motor VALIDA (schema + ações permitidas)
  → motor aplica consequências
  → memória e relação são atualizadas
```

A LLM ocupa exatamente um passo, e é o único que pode falhar sem parar o mundo.

## Fallback determinístico

Obrigatório em todo caminho. Provider fora do ar, timeout, ou saída que não valida →
resposta determinística do motor. A experiência degrada; a simulação nunca trava.

## Orçamento

Latência e custo são orçados **por interação** — o jogador espera uma resposta, não um
lote. Resumo de memória é o oposto: roda **em lote e fora do caminho crítico**.

## Ver também
- [npc.md](npc.md) — o que da ficha entra no contexto
- [memory.md](memory.md) — recuperação por relevância e resumo em lote
- [behavior.md](behavior.md) — a decisão que o motor já tomou antes de chamar a LLM
- [society.md](society.md) — cultura e conhecimento como restrição do que se pode dizer
- [history.md](history.md) — narrativa derivada do log
- [simulation-lod.md](simulation-lod.md) — por que a maioria dos NPCs nunca chama a LLM
