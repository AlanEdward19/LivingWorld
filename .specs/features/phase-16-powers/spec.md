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
- **POW-08** — WHEN uma aquisição declarar taxa-base THEN a chance efetiva SHALL ser
  `taxa-base × RateGene` limitada a `[0,1]`; nascimento herda esse multiplicador pela Fase 6,
  mas SHALL iniciar sem copiar poderes ou domínio dos pais.
- **POW-09** — WHEN os fixtures Vampiro, Lobisomem, Lanterna Verde, Kryptoniano e Velocista
  forem executados THEN todos SHALL adquirir, manifestar e aplicar efeito pelo mesmo motor
  genérico; nomes nominais SHALL existir somente no código de teste.
- **POW-10** — WHEN potência manifestada modificar locomoção THEN velocidade SHALL mudar células
  efetivamente percorridas por hora e voo SHALL atravessar terreno sem teleportar nem atravessar
  paredes/interiores; pouso, colisão, custo e posição autoritativa permanecem válidos.
- **POW-11** — WHEN `construct.create` for aplicado THEN forma, células, durabilidade e expiração
  declaradas SHALL criar ocupação canônica temporária, causal e sem criar recursos ou dinheiro.
- **POW-12** — WHEN o operador selecionar um NPC THEN a web SHALL listar o catálogo do cenário e
  enviar concessão, revogação e invocação ao motor autoritativo; construtos SHALL aceitar uma
  célula escolhida, e nenhum comando inválido SHALL produzir mutação parcial.

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

Casos nominais no motor, alinhamento herói/vilão, divindades, viagem temporal, contato alienígena,
contramedidas inventadas e balanceamento de prevalência.

## Rastreabilidade

Critérios de fechamento do roadmap: [spec-closeout.md](spec-closeout.md).

| ID | Design | Task | Status |
|---|---|---|---|
| POW-01, POW-03 | borda + plano vazio | T1 | Verified |
| POW-02 | modelo composicional | T1 | Verified |
| POW-04 | registro runtime | T2 | Verified |
| POW-05 | aplicação/custos | T3 | Implemented |
| POW-06 | aquisição/manifestação | T4 | Implemented |
| POW-07 | estado consultável | T2 | Verified |
| POW-08 | predisposição herdável, poder não herdado | T6 | Verified |
| POW-09 | cinco fixtures nominais de regressão | T7 | Implemented |
| POW-10 | locomoção física e projeção visual | T8 | Implemented |
| POW-11 | ocupação temporária e conservação | T9 | Implemented |
| POW-12 | comandos de autoria e UX operacional | T10 | Verified |
