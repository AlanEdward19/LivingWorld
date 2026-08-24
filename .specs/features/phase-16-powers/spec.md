# Phase 16 — Potência

## Problema e objetivo

O extraordinário precisa alterar regras existentes sem criar um motor por fantasia. O objetivo
é representar capacidades como dados composicionais, opcionais por mundo e determinísticos.

## Escopo

- P1: `Extraordinary.Enabled = false` não registra sistema, portador ou evento extraordinário.
- P1: cenário declara descritores validados de fonte, efeito, modo, custo, confiabilidade,
  falha, vulnerabilidade, manifestação e aquisição; nenhum poder vira `enum` ou caso nominal.
- P2: aquisição, manifestação, uso, custo e falha geram cadeia causal no event log.
- P2: efeitos modificam apenas sistemas-alvo declarados e preservam conservação.
- P3: cultura interpreta manifestações; predisposição, LOD e contramedidas integram fases afins.

## Composições exigidas, sem arquétipos nominais

- Fonte externa + vontade pode produzir construtos, custo, assinatura visível e indisponibilidade.
- Manifestação noturna pode alterar aparência/metabolismo e declarar vulnerabilidade intrínseca.
- Manifestação cíclica pode alterar escala, movimento e controle durante uma condição do mundo.
- Cada eixo combina livremente; nenhuma dessas composições cria enum, classe ou nome especial.

## Requisitos e critérios de aceite

- **POW-01** — WHEN o bloco extraordinário estiver ausente ou desabilitado THEN o plano runtime
  SHALL conter zero portadores, eventos e sistemas extraordinários.
- **POW-02** — WHEN um descritor válido for carregado THEN cada eixo SHALL permanecer consultável
  como dado de cenário, sem tipo nominal por poder.
- **POW-03** — WHEN id/eixo obrigatório estiver vazio, id for duplicado ou falha existir sem
  `ResolutionCheck` THEN a borda SHALL retornar `Failure` nomeando o campo e criar zero runtime.
- **POW-04** — WHEN o extraordinário estiver ligado THEN só sistemas explicitamente registrados
  SHALL entrar no relógio; a ordem será estável e nenhuma resolução usará RNG global.
- **POW-05** — WHEN um efeito for invocado THEN somente alvos declarados SHALL mudar; custo
  declarado será debitado no uso e toda mutação causal será registrada.
- **POW-06** — WHEN houver aquisição ou manifestação THEN as transições SHALL seguir regras de
  cenário e relógio/RNG do mundo, nunca nomes codificados.
- **POW-07** — WHEN um descritor declarar aparência, necessidade substituta ou senescência THEN
  SHALL expor escala/tom/trilha, recurso consumido e multiplicador de idade separadamente;
  multiplicador zero não concede imunidade a outras causas de morte.

## Assumptions & Open Questions

| Decisão | Default | Razão |
|---|---|---|
| Cenário legado sem bloco | extraordinário desabilitado | compatibilidade sem placeholder |
| Efeito temporal | bloqueado até Fase 18 | ainda não há contrato causal seguro |
| Custo de longevidade | reagenda morte e registra evento | evita dois relógios de mortalidade |
| Portador agregado | stream determinístico da região | não força materialização |
| Atenção hostil sem entidade | evento causal genérico | entidade concreta pertence à Fase 17 |

Dimensões externas, autenticação, retry, concorrência e expiração: N/A; entrada é cenário local,
o motor é single-thread determinístico e não há I/O externo neste escopo.

## Fora do escopo

Nomes/franquias, alinhamento herói/vilão, divindades, viagem temporal, contato alienígena,
contramedidas inventadas e balanceamento de prevalência.

## Rastreabilidade

| ID | Design | Task | Status |
|---|---|---|---|
| POW-01, POW-03 | borda + plano vazio | T1 | Verified |
| POW-02 | modelo composicional | T1 | Verified |
| POW-04 | registro runtime | T2 | Verified |
| POW-05 | aplicação/custos | T3 | Pendente |
| POW-06 | aquisição/manifestação | T4 | Pendente |
| POW-07 | estado consultável | T2 | Verified |
