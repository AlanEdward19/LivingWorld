# História e eventos

Como o mundo registra a si mesmo: um log imutável do que aconteceu, e a narrativa que se
deriva dele.

## Append-only, sempre

O event log é **somente-acréscimo e imutável**. Nada é editado, nada é apagado. Corrigir o
passado se faz com um **evento compensatório** — nunca com `UPDATE`. Isso garante que a
linha do tempo seja reproduzível, auditável, e que uma narrativa gerada hoje continue
verdadeira amanhã.

Registram-se: nascimento, morte, casamento, migração, fundação de cidade, guerra, tratado,
invenção, epidemia, desastre natural, mudança política e interação importante com o jogador.

## A linha do tempo

Consultável por ano, por cidade, por linhagem ou por tipo:

```
Ano 12 — Fundação de Arven
Ano 27 — Primeira rota comercial
Ano 54 — Guerra do Norte
Ano 57 — Queda da dinastia Merian
Ano 80 — Descoberta da pólvora
```

Dinastias e linhagens são rastreáveis: dá para seguir uma família do fundador ao último
herdeiro sem reconstruir nada à mão.

## Retenção

Guardar cada refeição de 100 mil NPCs por 100 anos é inviável. O nível de detalhe é
**configurável**, com uma política simples:

| Classe de evento | Destino |
|---|---|
| Pessoal, NPC comum | Comprime com o tempo — vira resumo agregado |
| Pessoal, figura notável | Preservado enquanto a figura importar |
| Historicamente significativo | Preservado indefinidamente |

Compressão perde detalhe, nunca coerência: o resumo continua consistente com o que restou.

## Eventos emergentes

Os eventos que valem a pena não são roteirizados — nascem da combinação de sistemas: seca
reduz produção · família migra · jovem aprende medicina · epidemia começa · governante
morre · duas cidades entram em guerra · nova religião surge · invenção aumenta produção ·
estrada cria rota · vila vira cidade.

Nenhum tem gatilho dedicado. Todos são o estado do mundo cruzando um limiar.

## Narrativa é derivada

A LLM lê dados brutos do log e produz texto legível:

**Dados:** seca por 3 anos · queda de 40% na produção · migração acima da média ·
criminalidade em alta · revolta registrada em Valen.

**Narrativa:** "Após três colheitas perdidas, os moradores de Valen se revoltaram contra os
cobradores do rei."

A narrativa **nunca cria fato novo**. Se não está no log, não aconteceu — e a frase não
pode afirmar. Texto bonito que inventa um detalhe é um bug de veracidade, não licença
poética.

> **O log é a verdade, e a verdade é do motor.** O que o mundo conhece do próprio passado é
> outra coisa: relatos degradados, transmitidos e possivelmente falsos. Ver
> [historical-memory.md](historical-memory.md) — é lá que a retenção de fato acontece.

## Ver também
- [historical-memory.md](historical-memory.md) — degradação do passado e cânone limitado
- [llm-contract.md](llm-contract.md) — como a narrativa é pedida e validada
- [time-and-ticks.md](time-and-ticks.md) — o relógio que data os eventos
- [economy.md](economy.md) — crises que viram evento
- [cities.md](cities.md) — fundação, fusão e destruição
- [society.md](society.md) — mudança política, invenção, religião
- [genetics-and-family.md](genetics-and-family.md) — dinastias e linhagens
- [memory.md](memory.md) — a memória do NPC vs. o registro do mundo
- [simulation-lod.md](simulation-lod.md) — o que ainda é registrado em baixo detalhe
