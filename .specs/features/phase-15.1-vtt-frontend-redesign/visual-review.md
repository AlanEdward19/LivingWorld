# Fase 15.1 — validação visual do Estágio 1

## Build apresentado

- Data: 2026-08-07
- Execução: Vite em `http://127.0.0.1:5173`, sem backend iniciado
- Fontes de simulação: `MockSnapshotSource`, `MockTickStreamSource`,
  `MockTimeControlSource` e `MockPortalSource`, injetadas somente em `main.tsx`
- Gate: 38 arquivos, 208 testes web aprovados; `tsc --noEmit` limpo

## Percurso validado

- Menu inicial → mapa-múndi
- Movimento acumulado por tick mock; pause/velocidades disponíveis
- Seleção de cidade → inspector → Follow
- Mundo → cidade → prédio e retorno por breadcrumb
- Camadas: painel abriu e Biome foi ativada
- World Creator: preset em branco → editor visual; mapa principal, toolbar e inspector
- Configuração avançada em seis accordions fechados por padrão

## Correção durante a validação

O mock emitia ticks, mas repetia sempre a posição inicial `x + 1`. A posição agora acumula entre
ticks; `MockTickStreamSource.test.ts` prova que o segundo tick chega em `x + 2`.

## Decisão do usuário

**Aguardando revisão.** Ajustes pedidos permanecem no Estágio 1; aprovação explícita libera o
Estágio 2.
