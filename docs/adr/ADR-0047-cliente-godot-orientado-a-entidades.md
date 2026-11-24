# ADR-0047: Cliente Godot orientado a entidades sobre uma projeção do observável

- **Status**: aceito (aprovado pelo dono em 2026-11-17, com as opções recomendadas)
- **Data**: 2026-11-17
- **Decisores**: dono do projeto (Alan); rascunho por agente

## Contexto
O dono pediu "uma refatoração completa do motor visual, que hoje é muito abstrato; não consigo interagir direito
com nada, muito menos usá-lo para assistir os mundos" (`.specs/features/realism-backlog/09-cliente-visual-e-jogador.md`).
O levantamento de 2026-11-17 no cliente (`client/godot/`, 3D isométrico ortográfico, ADR-0022 e ADR-0025) achou:

- **Abstrato**: nenhum asset; tudo é caixa, cápsula e esfera. Lavoura, animais, estação, doença e luto não são
  desenhados. Idade e profissão só aparecem no corpo do NPC cuja ficha está aberta. `Steal` e `Cry` caem em "parado".
  No mapa-múndi, quem está dentro de uma cidade é invisível. Evento vira só um aviso de texto.
- **Sem interação**: prédio não tem ficha (clicar entra direto); dentro do interior nada é clicável; parentesco,
  emprego e casa são texto morto ou número; a ficha não se atualiza; seguir só vale para uma pessoa e se perde ao
  trocar de escopo; as cinco janelas disputam a mesma área.
- **Dados descartados ou ausentes**: o delta já traz processos em andamento (lavoura inclusive) e o cliente os
  ignora; prédio novo não é desenhado até recarregar. A API não expõe estação, fauna, estado de doença, emoções
  (luto, medo), corpo (altura, gravidez) nem os envolvidos de eventos como `SupportPaid`.
- **Código**: `app.gd` (947 linhas) concentra cena, UI, seleção, atalhos e escopos; cada painel chama a API direto.
- **Medida de FPS fraca**: `stress:500` roda com vsync (teto de 77–79), sem cidade aberta nem painéis.

## Decisão
Vamos manter o Godot 3D isométrico e refazer o cliente em torno de **entidades observáveis**, em três etapas:

1. **Interação, sem mudar contrato**: quebrar `app.gd` em módulos (sessão, seleção, inspetor, janelas, um
   renderizador por tipo); registro local de entidades; **inspetor genérico** para pessoa, prédio, lavoura, animal
   e objeto, que se atualiza e tem links (parentesco, emprego, casa, dono); clique e hover dentro do interior;
   janelas que convivem; seguir pessoa, família ou cidade sem perder ao trocar de escopo; consumir o que já chega
   (processos, lavoura, prédio novo, posição dos eventos no mapa).
2. **Projeção do observável no servidor** (muda o contrato público, só por acréscimo): por escopo, ação com alvo,
   estágio da lavoura, animais, estação e clima, sinais visíveis do corpo (idade, altura, gravidez, doença, luto),
   função, dono e moradores do prédio, objetos inventados com material e forma, envolvidos dos eventos; e uma rota
   de inspeção por tipo de entidade. Só leitura: o cliente nunca escreve no mundo.
3. **Aparência derivada do estado e direção**: corpo, roupa e ferramenta vindos do estado em todos os NPCs; safra e
   estação no terreno; LOD em três níveis (mundo: densidade e eventos; cidade: corpos, prédios, lavoura; perto:
   ferramenta, objeto em uso, expressão); câmera "diretor" (seguir, pular para o próximo evento).

Antes da etapa 1 a medida de FPS é recalibrada (vsync desligado, cidade real aberta, tempo de quadro p95, além
do `stress:500`) e vira o piso de toda etapa: nenhuma entra com regressão.

## Alternativas consideradas
- **Só refatorar o 3D atual, sem contrato novo**: barato e sem risco, resolve a interação, mas continua abstrato e
  sem estação, fauna, doença e luto. É a etapa 1 desta decisão, não o fim.
- **Trocar para 2D de cima com tiles e sprites** (como as fases 15 e 27 previam): leitura ótima e FPS folgado, mas
  reescreve câmera, cena, interior multi-andar e boa parte dos 291 testes, reabre o ADR-0025 e precisa das mesmas
  mudanças de contrato.

## Consequências
- **Positivas**: ataca as três queixas (abstrato, interação, assistir); a projeção do observável e o inspetor
  servem à fatia do jogador (fase 25) e às cinemáticas (fase 27).
- **Negativas / trade-offs**: contrato público cresce; mais nós e meshes pedem MultiMesh por tipo, materiais
  compartilhados e LOD para segurar o FPS; a projeção custa no servidor e tem de ser medida por NPC-tick (só para
  escopos assinados). Estimativa: 5 a 8 lotes entre servidor e cliente.
- **Follow-ups**: ADR próprio se entrar pacote de arte (dependência nova e licença); fases 15, 27 e 28 do roadmap
  ajustadas ao que for aprovado.

## Decisões do dono (2026-11-17: "Aprovo o recomendado")
1. **2D ou 3D**: recomendado manter 3D isométrico.
2. **Arte**: continuar com formas geradas por código (mais ricas, derivadas do estado) ou adotar um pacote
   low-poly de licença livre (dependência nova). Recomendado: formas por código agora; pacote só com ADR próprio.
3. **Detalhe ao aproximar**: ferramenta na mão, objeto em uso e expressão simples (recomendado), ou rosto e cada
   objeto da casa detalhados.
4. **Contrato**: liberar acréscimos na API para a projeção do observável (rotas e campos novos, nada removido).
5. **Crime e violência na tela**: recomendado mostrar o fato como evento no mapa e na ficha (autor, vítima, caso,
   consequências), sem cena; violência sexual, doméstica e contra criança só como registro, igual ao motor.
6. **Tempo**: sem recomendação na proposta; fica como hoje (só admin, ADR-0015) até o dono dizer o contrário.
7. **Piso de FPS**: manter 50 FPS no M1 Pro com 500 NPCs, agora medido sem vsync e com cidade aberta.
