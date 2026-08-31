# Cognição — o motor de decisão inspecionável

Não é um cérebro novo: é o utility AI de [behavior.md](behavior.md) tornado **auditável** —
cada decisão relevante grava o que pesou, para um painel visual e um sandbox de pesquisa.

## O rastro

Uma decisão de `behavior.md` já calcula `nota = utilidadeBase(necessidade, contexto) *
pesoPersonalidade` por ação candidata. O rastro grava, por decisão tomada:

| Campo | Exemplo |
|---|---|
| Necessidade dominante | fome 90 |
| Traço aplicado | disciplinado alto → peso +0,3 em Trabalhar |
| Memória consultada | "mercado ficou sem pão semana passada" |
| Opção vencedora | Comer (95) |
| Opção descartada e por quê | Trabalhar (72) — nota menor |

Log estruturado (campos tipados, não texto livre), sob o mesmo teto de bytes/NPC da Fase 9 —
e só grava quando o NPC está no escopo observado (`observational-lod.md` task 13): ninguém
audita a decisão de quem ninguém está olhando.

## Painel "ver o cérebro" (web)

Selecionar um NPC detalhado expõe o rastro em duas formas, as duas lendo o mesmo dado —
nenhuma recalcula nada:
- **Dados**: tabela/timeline das últimas N decisões, com os campos acima.
- **Visual**: fluxo estímulo → ponderação → decisão, animável, no mesmo cliente da Fase 15.

## Sandbox de decisão (pesquisa, fora do mundo)

Ambiente isolado que roda o **mesmo motor** da task 9, mas com entrada substituída: injeta
estímulo, traço de personalidade ou memória sintéticos num "cérebro" solto e mostra a
decisão resultante — sem tick de mundo, sem conservação, sem efeito em NPC nenhum. É
ferramenta de autoria/pesquisa (o cruzamento com entrevista humana e literatura de decisão
mencionado no dossiê de relevância vira cenário de teste aqui, não sistema novo do mundo).

## Por que isto não é IA nova

O objetivo desta fase é instrumentação, não um segundo motor de decisão. Se um experimento
do sandbox pedir um mecanismo que `behavior.md` não tem (ex.: correntes de decisão, ver a
seguir), a extensão entra em `behavior.md` primeiro — a fase 28 só garante que qualquer
mecanismo de decisão, presente ou futuro, seja gravável e visível.

## Fora do escopo desta fase
Fatores biológicos indiretos (metabolismo, fadiga, atividade física, nutrição) como *input*
novo do motor — ideia registrada, não especificada aqui; entra como extensão desta fase ou
fase própria só depois que o rastro básico (estímulo→decisão já existente) estiver provado.

## Ver também
[behavior.md](behavior.md) · [observational-lod.md](observational-lod.md) ·
[npc.md](npc.md) · [memory.md](memory.md) ·
[phase-28-cognition.md](../roadmap/phase-28-cognition.md) ·
[llm-contract.md](llm-contract.md)
