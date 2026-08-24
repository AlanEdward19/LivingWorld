# Phase 16 — critérios de fechamento

**Parent**: [spec.md](spec.md) · **Fonte**: `docs/roadmap/phase-16-powers.md`

## Requisitos restantes

- **POW-13 — disponibilidade por modo**: `Passive` alimenta somente consumidores contínuos;
  `Active` aceita invocação explícita; `Triggered` aceita somente origem causal do sistema;
  `Conditional` aceita invocação explícita apenas enquanto manifestado. Custo e confiabilidade
  não alteram essa decisão. Modo desconhecido falha na borda sem produzir runtime.
- **POW-14 — resolução e consequência**: `Guaranteed` não consome RNG de resolução.
  `ResolutionCheck` autorado usa `Resolver` e stream estável por portador/poder/invocação; o
  cliente não escolhe o resultado. Custos são idênticos em sucesso e falha. `PartialSuccess`
  aplica metade da magnitude declarada, afastada de zero; falha registra cada modo declarado e
  `carrier.health:N` também debita saúde do portador. Uma falha nunca desaparece do estado causal.
- **POW-15 — prevalência e LOD**: `Prevalence` é probabilidade de cenário em `[0,1]`, default
  zero. Na criação, um stream estável por cidade seleciona ids em ordem e atribui um descritor
  sem materializar o pool. A projeção global reporta apenas `KnownCarrierCount` por cidade; a
  projeção detalhada continua expondo identidade somente para NPC materializado.
- **POW-16 — gates causais finais**: cenários pareados provam efeitos declarados, custo em
  falha/sucesso, ausência total quando desligado, conservação por tick, hereditariedade,
  reação cultural oposta e diferença de hash quando o sistema participa.

## Resultados exatos e bordas

- Invocação rejeitada por modo, alvo, ocupação ou recurso preserva hash e próximo id de evento.
- Uma tentativa válida com `ResolutionCheck` reserva o id mesmo quando o resultado falha.
- `Prevalence = 0` cria zero portadores; `1` cria um portador por pessoa elegível; valor fora da
  faixa falha nomeando `Extraordinary.Prevalence` e não cria mundo parcial.
- Sem descritores, prevalência positiva é inválida; escolha entre vários descritores usa ordem
  autorada e RNG da região, nunca nome nominal.
- Portador agregado permanece no pool: contagem, riqueza, saúde e ids não mudam ao atribuir poder.

## Rastreabilidade

| ID | Design | Task | Status |
|---|---|---|---|
| POW-13, POW-14 | `design-closeout.md` resolução/availability | T11 | Verified |
| POW-15 | `design-closeout.md` prevalence/LOD | T12 | Verified |
| POW-16 | matriz pareada | T13 | Verified |
