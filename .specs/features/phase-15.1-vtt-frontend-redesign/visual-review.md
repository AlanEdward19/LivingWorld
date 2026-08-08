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

**Não aprovado em 2026-08-07.** O World Builder foi considerado confuso, com botões demais e
pouco visual. A nova ordem é: (1) personagens como pawns top-down dinâmicos em SVG, inspirados na
legibilidade de jogos de colônia sem copiar assets; (2) World Builder como configuração visual de
jogo; (3) revisão posterior da cidade. Os ajustes permanecem no Estágio 1.

## Ajuste de personagens — T35

- Pawn top-down original em SVG: sombra, roupa, cabeça, cabelo e indicador de ação.
- Identidade visual deriva somente do ID; ação não altera pele, cabelo ou roupa.
- Mapa reutiliza o SVG por cache de imagem dentro do canvas único; inspector exibe o mesmo pawn.
- Gate em 2026-08-07: 39 arquivos, 212 testes web; TypeScript limpo.
- Naquele checkpoint, a aprovação visual ainda estava pendente antes de iniciar o World Builder.

## Ajuste do World Builder — T36

- Tela inicial refeita como configuração de jogo: cartões de escala, origem e preview conceitual.
- Preview reage a nome, seed e escala sem alterar o cenário canônico.
- Editor recebeu dock visual de ferramentas e cartões-resumo; configurações avançadas continuam
  fechadas por categoria.
- Validado no navegador em 2026-08-07; gate com 39 arquivos/214 testes e TypeScript limpo;
  naquele checkpoint a aprovação visual ainda estava pendente.

## Paisagem e arquitetura — T37/T38

- Mapa-múndi recebeu superfície verde/solo determinística, rios em tile, céu e nuvens cosméticas.
- Cidade recebeu chão vivo no lugar do vazio cinza; detalhes de grama aparecem no zoom próximo.
- Prédios usam telhado, pedra/madeira e porta com detalhe top-down no canvas.
- Bug Z corrigido: andar observado não participa mais da seed de footprint, porta ou portão.
- Gate em 2026-08-07: 40 arquivos, 220 testes web; TypeScript limpo.
- Mapa-múndi revisado no navegador; abertura da cidade pelo canvas não ficou disponível no
  controle automatizado, mas navegação, materiais e regressão Z permanecem cobertos por testes.

## Correções após review — T39

- Cidade no mapa deixou de ser anel retangular vazio: agora tem cantos cortados, ruas e telhados.
- Prédios vistos de fora usam cobertura contínua; a planta técnica fica restrita ao interior.
- Cidade abre com zoom 8 px/tile, exibindo uma área duas vezes maior que antes.
- Interior usa piso `#3d382d` e paredes com 80% de opacidade, sem azul atmosférico.
- Gate em 2026-08-07: 40 arquivos, 221 testes web; TypeScript limpo.

## Segunda correção visual — T40

- Cidade e prédio deixaram de ser matrizes pintadas, sem contorno individual por tile.
- Cidade tem muralha chanfrada, torres, portão, vias e telhados variados; prédios têm fachada,
  duas águas contrastantes, porta e chaminé com paleta estável por identidade.
- A cidade usa chão finito 34x24 e `showGrid: false`; o grid segue no interior e no editor.
- Captura do mapa-múndi revisada no navegador; naquele checkpoint a aprovação ainda estava pendente.
- Direção visual aprovada pelo usuário em 2026-08-07; contrato dinâmico da API será definido na
  fase de integração.

## World Builder espacial — T41

- Seed e escala compartilham a mesma paisagem procedural entre preview e editor; escala anima.
- Mapa usa enquadramento `cover`, pintura por arraste e assentamentos renomeáveis/móveis.
- Assentamento abre editor local com ruas e construções posicionáveis/móveis; o rascunho interno
  permanece client-side até o contrato da fase de integração.
- Configuração virou capítulos temáticos com uma área visível, transição e ação final persistente.
- Revisado no navegador em 1280x720; gate web: 42 arquivos, 233 testes, TypeScript/build limpos.

## World Builder — review 2

- Preview desenha todas as células nas dimensões reais escolhidas; seed, proporção e posição
  inicial do assentamento seguem para o editor sem a antiga grade fixa 12x8.
- Água e terreno autorados reutilizam a paleta procedural do mapa em vez de cores desconectadas.
- Assentamentos do editor aparecem como conjunto de telhados, sem muralha/caixa externa.
- Assentamentos e construções podem ser removidos por ferramenta, inspector e Delete/Backspace;
  Ctrl+Z/Ctrl+Y também funcionam no mapa-múndi e no editor local da cidade.
- Capítulos avançados começam por pergunta, efeito e direção sugerida; valores técnicos ficam no
  segundo nível e seus rótulos exibem ajuda contextual ao passar o mouse.
- Preview, editor e capítulo Povos revisados no navegador; naquele checkpoint a aprovação pendia.
- Assentamentos e construções selecionados rotacionam em passos de 90° pelo inspector ou tecla R;
  campos de edição preservam a digitação e não capturam o atalho.

## Aprovação final do Estágio 1

- Aprovado explicitamente pelo usuário em 2026-08-07 após a entrega da rotação.
- T29 está concluída; o Backend começa pelo inventário E2.0 de `backend-gaps.md`.
