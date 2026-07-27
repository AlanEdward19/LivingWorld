# Fase 14 — Cliente Unreal

**Objetivo**: existe um cliente 3D que mostra o mundo em locais, personagens e voz,
consumindo **a mesma API** do cliente web. O Unreal é uma janela para o mundo, nunca uma
segunda fonte da verdade.

## Tasks
1. **Projeto Unreal fora de `src/`**: cliente separado, sem referência a nenhum projeto
   .NET. Fala com o servidor só por HTTP e realtime, como o cliente web.
2. **Wrapper HTTP único**: todo tráfego do cliente passa por **uma** classe de transporte,
   com DTOs derivados do mesmo OpenAPI que a web consome. Nenhum endpoint exclusivo,
   nenhum campo só-para-Unreal, nenhuma chamada solta espalhada pelo código.
3. **Regra de lint/AST no build do Unreal**: falha o build se qualquer verbo não-GET
   aparecer fora do wrapper e do endpoint de conversa. É o substituto executável da
   "auditoria de código" — revisão humana nunca reprova um CI.
4. **Autorização de escrita no servidor**: o token do cliente de visualização carrega um
   escopo somente-leitura; toda rota de escrita rejeita esse escopo. A garantia mora onde é
   executável, não onde é revisável. Flag de teste desliga a checagem, só para a mutação.
5. **Streaming de estado por realtime**: assinatura por região; o servidor empurra deltas
   de NPCs, locais e eventos enquanto o mundo avança. O jogador conectado não pausa nada.
6. **Locais e personagens em 3D**: mapear tipos de local do catálogo do cenário para cenas,
   e NPCs para personagens com aparência derivada de atributos já existentes (idade, sexo,
   profissão). Nada de aparência inventada no cliente.
7. **Animação por estado**: a animação lê a atividade atual do NPC vinda do servidor
   (dormir, trabalhar, deslocar-se, conversar). O cliente não decide o que o NPC faz.
8. **LOD visual desacoplado do Simulation LOD**: região em LOD alto ganha personagens
   individuais; LOD baixo vira agregado visual. Nem a câmera nem o LOD visual escrevem no
   LOD de simulação — o caminho não existe, e o critério tenta provar que existe.
9. **Voz via ACE (NVIDIA)**: fala do jogador → texto → **o mesmo** pipeline de conversa da
   Fase 11 → texto validado → voz e lip sync. Nenhum atalho da voz direto para o provider.
10. **Medição de latência no servidor**: instrumentar tick → delta publicado e gravar o
    baseline por classe de máquina. O fim a fim (servidor → pixel) vira dashboard, não gate.

## Critérios de verificação
- **Escrita bloqueada no servidor**: para cada rota de escrita enumerada por reflexão, uma
  requisição com o escopo do cliente de visualização é rejeitada e o hash canônico não
  muda. **Par de mutação**: com a flag que desliga a autorização, **este** critério falha.
  Se passar sem autorização, ele media só a boa vontade do cliente.
- **Lint do cliente**: um commit de fixture que insere um `POST` fora do wrapper faz o
  build do Unreal falhar. Sem esse mutante, a regra pode estar desligada e ninguém vê.
- Desconectar o cliente no meio de uma execução: o mundo continua avançando e o hash após
  N ticks é **idêntico** ao de rodar os mesmos N ticks sem cliente nenhum.
- **Mesma resposta para qualquer cliente**: para cada rota enumerada por reflexão, N
  conjuntos de headers distintos (`User-Agent` de Unreal, de browser, ausente, inventado)
  devolvem corpo **byte-idêntico**. "O servidor não lê o header" é indecidível
  estaticamente; igualdade de bytes não é. Rota nova sem cobertura reprova.
- **LOD visual não realimenta a simulação**: durante 1000 ticks o LOD visual **varia**
  (alto → baixo → alto, com a câmera se movendo entre regiões) e, ao final, o campo de LOD
  de simulação e o hash canônico estão inalterados. Câmera parada passaria por construção —
  o teste precisa mexer justamente no acoplamento que a fase teme.
- Conversa por voz roda o **mesmo corpus de injeção da Fase 11**, com os mesmos asserts
  (`proposedActions ⊆ açõesPermitidas`, hash canônico inalterado, zero campo fora do
  schema). Nenhuma exceção por a fala ter chegado como áudio.
- Provider de voz fora do ar degrada para texto na tela — a sessão não cai e a simulação
  não trava.
- **Latência medida no servidor**: p95 de tick → delta publicado sobre ≥ 100 deltas,
  comparado ao baseline gravado da mesma classe de máquina em `tests/baselines/`. Fim a fim
  fica fora do gate: em CI ele mede o runner, não o código.

## Fora do escopo
Nenhuma regra de simulação nova, nenhum período novo (Fase 13) e nenhuma mudança no
contrato de LLM (Fase 11). Esta fase **depende da Fase 15 (mapa visual) apenas para
paridade de navegação** — sem ela o cliente 3D funciona, só não espelha a navegação do
mapa web. Se o 3D exigir dado que a API não expõe, é endpoint novo compartilhado com a
web — nunca um caminho paralelo.

## Ver também
[simulation-lod.md](../domain/simulation-lod.md) ·
[world-map.md](../domain/world-map.md) ·
[llm-contract.md](../domain/llm-contract.md) ·
[ADR-0003](../adr/ADR-0003-cliente-web-react-ts.md) ·
[rules/llm-boundary.md](../../rules/llm-boundary.md)
