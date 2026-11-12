# Backlog de realismo e desempenho (trilha #142)

> Tudo o que o dono pediu nesta trilha, o que já entrou, o que está sendo feito e o que falta começar.
> Escrito em 2026-11-12 para não se perder entre sessões. Atualize o estado aqui quando uma fatia for juntada.

## Diretriz do dono (palavras dele)

- "Quero o sistema mais realista em todos os aspectos, desde relações sociais ao nível biológico."
- "Toda a existência de um npc e tudo que ele faz deve mirar na realidade da vida real, em todos os aspectos, até na tomada de decisão." "Na ponta do lápis, igual seria no mundo real, até os pensamentos."
- "Quero o mundo totalmente simulado." "Tudo no livingworld é determinístico e totalmente simulation driven, em nada escrito. Então o npc desenvolve tudo e tem a concepção da ideia (que pode ser reproduzível depois)."
- "Única coisa que deve fugir da realidade são os poderes, mas neles iremos adotar uma abordagem de genética (como seria na vida real se fossem existir)."
- Desempenho "absurdo" e testes rápidos: "rodar tudo em no máximo 5 minutos ou menos sem perder qualidade"; "não quero precisar mover coisas pro noturno". Compartilhar um mundo entre testes está liberado.
- Pode mudar o mundo e regravar goldens e baselines, desde que NPCs e mundo fiquem mais realistas.
- Trabalho delegável vai para subagente Opus em worktree; validado, junta no branch e dispara o próximo. Primeiro desempenho, depois o resto.
- Pediu para ser avisado quando tudo terminar.

## Estado

Branch de trabalho: `perf/decision-scan-costs`, sem push, sem PR. Último commit conhecido: ver `git log` (a junção do AD-134 veio depois de `d28d31ee`).

| Área | Estado | Arquivo |
|---|---|---|
| Custo de estado, vínculos, sobrevivência, trabalho (AD-127, AD-128) | juntado | `docs/decisions-log.md` |
| Fila de despertar (AD-129), snapshot numa passada (AD-131) | juntado | idem |
| Demografia de época (AD-130), números conferidos (AD-133) | juntado | idem |
| Aprendizado por idade e criação (AD-132) | juntado | idem |
| Gate em paralelo, mundo compartilhado (ADR-0044) | juntado | `docs/adr/` |
| Corpo que cresce, variância genética, saúde dinâmica, amamentação (AD-134) | juntado | `docs/decisions-log.md` |
| Doença de época ligada por padrão (item 5 da fatia de corpo) | não começado | [06](06-violencia-poderes-e-restantes.md) |
| Agricultura, estações, água de beber (AD-135) | em worktree | [01](01-em-andamento.md) |
| Vocação que emerge, traição, separação, novas uniões, tabu de parentesco (AD-136) | juntado | `docs/decisions-log.md` |
| Hormônios, ciclo menstrual, adolescência, doença genética, contracepção | não começado | [02](02-biologia-e-hormonios.md) |
| Cognição: memória, emoção, pensamento real; cérebro neural | não começado | [03](03-cognicao-e-cerebro.md) |
| Empreendedorismo, crédito, preço e salário | não começado | [04](04-economia-e-empreendedorismo.md) |
| Invenção emergente: tecnologia, veículos, robôs, aparência e animação | não começado | [05](05-invencao-emergente.md) |
| Violência, furto, poderes por genética, achados restantes da auditoria | não começado | [06](06-violencia-poderes-e-restantes.md) |
| Desempenho e tempo dos testes | parcial | [07](07-desempenho-e-testes.md) |
| Pontos em aberto a investigar (afrouxamentos, números sem fonte, efeitos sem explicação) | aberto | [08](08-a-investigar.md) |

## Como trabalhar uma fatia

1. Ler `relatorios/regras-para-fatias.md` (regras comuns dadas a todo subagente) e o arquivo da área.
2. Um worktree por fatia, partindo do último commit do branch. Fatias que mudam o mundo: no máximo duas ou três ao mesmo tempo, em arquivos diferentes.
3. Só juntar com `bash scripts/verify.sh --full` saindo 0. Na junção, goldens e baselines são regravados sobre a árvore combinada e o gate roda de novo.
4. Depois de cada junção: `bash scripts/demography-report.sh 1 20 100 10` e comparar com a referência (população final ~168, natalidade ~34‰, mortalidade ~29‰, e0 ~33, 1º ano ~212‰).
5. Todo número do mundo real entra como dado de cenário e vai para uma lista "precisa de conferência"; conferir em fonte antes de fechar (ver `relatorios/conferencia-demografia.md` como modelo).

## Relatórios guardados (evidência bruta, passam de 100 linhas de propósito)

- `relatorios/relatorio-realismo.md`: auditoria de todos os sistemas, 30 achados com arquivo:linha, 9 fatias.
- `relatorios/relatorio-empreendedorismo.md`: como é hoje e desenho em 8 fatias.
- `relatorios/relatorio-testes.md`: para onde vai o tempo do gate e o que foi proposto.
- `relatorios/conferencia-demografia.md`: 28 números conferidos em fonte, com o que foi lido e o que é lembrança.
- A auditoria foi feita no commit `baea58fd`; confirme cada achado no código antes de agir.
