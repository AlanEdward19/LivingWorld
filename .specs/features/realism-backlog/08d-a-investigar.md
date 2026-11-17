# A investigar (continuação de [08c](08c-a-investigar.md), mesma numeração)

## Do traçado da cidade: ruas, lotes, quintal e caminho (AD-173)
158. **A rua não nasce do trajeto real**: o traçado (largo, principal, travessas, vielas) é derivado do mapa e do ponto de
     fundação; o NPC viaja em linha reta (`MovementCost`), nunca pela rua. Abrir trilha pelo pisoteio pede viagem por rua
     (rota, custo, decisão) — é o que liga a superfície ao mundo.
159. **Superfície sem consequência causal**: trilha/terra batida não mudam passo, carga nem veículo (`Mobility.BonusPct`),
     nem viram lama na estação chuvosa; depende do item 158. Cascalho e pedra existem no tipo, mas nenhuma técnica (invenção)
     nem obra (construção) calça rua; custo de abrir e manter rua também não existe.
160. **Rua sem memória**: a superfície vem do uso de agora (pessoas servidas); trecho abandonado some na hora em vez de virar
     mato aos poucos. Precisa de estado (desgaste/abandono) no snapshot.
161. **Quintal só pesa no valor do imóvel** (`CroftValuePerCell`, aluguel e diária): horta doméstica, bichos da casa, poço,
     monte de lenha e espaço para ampliar a casa não existem no motor. Primeiro consumidor natural: horta → despensa.
162. **O lote não entra no custo nem no tempo da obra**: casa da semeadura não tem obra; a obra da cidade não tem família dona;
     casal novo não constrói casa (hospeda ou aluga). Terra, cerca e trabalho por célula de quintal ficam para a obra da família.
163. **Meios iguais na fundação**: o pecúlio inicial é o mesmo para todos, então a faixa de riqueza só separa casas
     construídas depois (oficinas e obras). Época e tecnologia não entram na escolha do lote.
164. **Fome caiu com a casa mais longe**: pareado 40 seeds × 15 anos, mortes de fome ×0,47 (IC90 0,29–0,64) e natalidade
     ×1,02 (1,003–1,037), embora a distância casa→trabalho medida suba ~10–20% (17,9 × 15,4 na seed 1). Achar a causa (mais
     casas com porta livre? aperto?) antes de atribuir ao traçado; sede crítica igual (×0,99, 0,97–1,02).
165. **Casa solitária de cidade nova**: com água em volta do largo, a primeira casa pode cair a mais de `AbsorptionRingCells`
     dos 3×3 da cidade sem gente e fica fora do limite (não é desenhada). A absorção encadeada só parte do limite existente.
166. **Ruas sobre outra cidade**: a ocupação é por cidade (já era no anel), então a rua de uma pode passar por prédio de outra
     colada a ela. Cortar o traçado no limite da vizinha sem mexer em posição derivada.
167. **Fundação de cidade-filha ~25% menor**: com o limite encadeado até 6 células do miolo, filhas em 10 anos 19 contra 26 e
     fusões 11 contra 19 (seeds 1/2/3/7, `CityFoundingReport`); encadeado até o mapa dava 9 e quase nenhuma. A causa da
     fundação ainda é geométrica (casa longe do miolo), não falta de lote, água ou trabalho perto: ligar à causa real.
168. **Contrato `world-subscribe`**: a forma gravada perdeu os exemplos de `npcs`/`externalNpcs` porque a fixture não tem mais
     NPC fora de cidade (tipo igual); preencher um exemplo como o `ObservableSample` faz para os demais.
169. **Terreno na rua**: a rua só contorna água; altitude e declive não pesam (curva de nível, vau, ponte).
170. **Prédio derivado sem porta para a rua**: prédio sem posição gravada (snapshot antigo) mantém a orientação por hash e cai
     em lote de caminho ou degrau sem rua; toda criação nova grava posição e orientação.
171. **Números sem fonte** de `CityLayoutRules` (largo 1, travessa 13, centro 5, quintal 2/1, família 5, pobre 0,5× e
     remediado 2× a mediana, desvio 4, caminho 8, pisoteio 12, valor 0,02): calibrar contra toft/croft medidos (Wharram Percy)
     quando a célula ganhar escala em metros.
172. **Aperto na `cidade-media`** (mapa 20×20, 150 pessoas): com lote, rua e porta livre cabem 11–15 casas; o resto das
     famílias divide a casa menos cheia além dos andares (antes: 29 casas, 28 encostadas, 10 sobre água, portas fechadas).
     Ver se algum sistema lê lotação acima dos andares (sono, doença, aluguel) e se o período deve ter mapa maior.
173. **Custo da vila espalhada no fixture de escala** (1.000 NPCs, vila no canto, uma fazenda): +15% CPU e +5% alocação
     por NPC-tick contra a base, todo do mundo diferente (casa→trabalho 67 células, mais viagens e despertares); com o
     traçado desligado o custo do código é o da base (alocação +0,03%). Caminhos: miolo denso que cresce com a cidade
     (sem quintal, parede-meia, porta na rua), rua até o lado do mapa, ofícios e roças perto das casas.

## Da rua de verdade (AD-176; itens 158–160 e 167 fechados por ela)
174. **Lama sem chuva**: o motor não tem chuva nem umidade (o fator de clima é de safra), então a rua de terra não vira lama na
     estação; ligar quando houver precipitação por região e mês.
175. ~~**Carga a pé não muda o passo**~~ AD-179: a carga levada alonga o passo (0,5%/kg até 40 kg), o material de obra também.: só o veículo de terra depende do caminho (cabe ou não); quem anda carregado anda igual.
176. **Reta integra o terreno só nas pontas** (`MovementCost`, como antes); a rede integra célula a célula, com subida só entre as
     pontas. Unificar quando o custo do mapa por célula for barato no caminho quente.
177. **Entrada e saída da rede pela célula de caminho mais perto** de cada ponta, não a melhor combinação; rota boa, não ótima.
178. ~~**Diarista da obra recebe o jornal sem ir à obra**~~ AD-179: aceita pela utilidade, vai pela rota, conta e recebe por hora no trecho.: a agenda dele não muda (não anda até o trecho nem deixa de ficar à toa).
179. **Cascalho não é mercadoria**: sai do leito do rio pela rota; a pedra é comprada quando o mundo tem o recurso (pedreira).
180. ~~**Mundo novo sem trilha**~~ AD-179: nasce com o chão estacionário do mês típico da história.: a vila semeada nasce sem caminho até a primeira revisão (3 meses); pisar o chão dos anos de antes
     da fundação daria rua no ano 0.
181. ~~**`cidade-media` quase não pisa o chão**~~ AD-179: era fixture sem economia; agora ~55 viagens/pessoa-mês.: ~8 passagens/mês para 150 pessoas (quase toda viagem dentro do mesmo prédio, o
     aperto do item 172); sem trilha nenhuma em 20 anos. Ver junto com o 172.
182. **Prédio vira obstáculo só na virada do mês** (a rota do mês é fixa); quem constrói em cima da trilha apaga o chão ali.
183. **Técnica de calçar sem protótipo pago**: diferente da de mobilidade (ferro do estoque), o trecho de teste não gasta recurso.
184. **Escopo Mundo remonta a lista de ruas a cada tick** (o diff é por conteúdo); guardar como o escopo Cidade se pesar.
