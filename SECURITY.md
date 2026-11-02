# Política de Segurança

## Versões suportadas

O projeto ainda não tem releases: correções de segurança entram na branch `main`. Atualize antes de reportar.

## Como reportar uma vulnerabilidade

**Não abra uma issue pública.** Use o [reporte privado de vulnerabilidades](https://github.com/AlanEdward19/LivingWorld/security/advisories/new)
do GitHub ou envie um e-mail para **alan-edward@outlook.com.br** com:

- a descrição do problema e do impacto,
- os passos para reproduzir,
- o commit e a plataforma em que você testou.

Você recebe uma confirmação em até 72 horas e uma atualização sobre a correção em até 14 dias. Damos o crédito no
histórico da correção, a menos que você prefira o contrário.

## Escopo

Áreas de maior interesse: autorização das rotas de admin (`X-Admin-Token`, ADR-0015) e do escopo de verdade
histórica, validação na borda de cenários, mundos salvos e entradas da API, a fronteira da LLM (saída da LLM nunca
pode escrever no estado do mundo, [rules/llm-boundary.md](rules/llm-boundary.md)), armazenamento de chaves de
provedores de LLM e os canais em tempo real (`/visual/ws`, `/visual/sse`).
