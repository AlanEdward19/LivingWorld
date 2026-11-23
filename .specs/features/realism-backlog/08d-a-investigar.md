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
164. ~~**Fome caiu com a casa mais longe**~~ (AD-182): não se repete na base do AD-182 (rua de verdade, AD-176): 40 × 15
     `starvation-trace`, traçado ligado 49 mortes de fome, desligado 51, mesmo perfil (17 contra 22 jovens sós empregados, o
     morto do 08c 149, corrigido na causa). Sem efeito a explicar, nada a corrigir no traçado.
165. ~~**Casa solitária de cidade nova**~~ AD-192: sem prédio perto do miolo, o limite encadeia a partir do mais perto.: com água em volta do largo, a primeira casa pode cair a mais de `AbsorptionRingCells`
     dos 3×3 da cidade sem gente e fica fora do limite (não é desenhada). A absorção encadeada só parte do limite existente.
166. ~~**Ruas sobre outra cidade**~~ AD-192: lote novo não cai no limite da vizinha nem dá a porta para a rua cortada ali.: a ocupação é por cidade (já era no anel), então a rua de uma pode passar por prédio de outra
     colada a ela. Cortar o traçado no limite da vizinha sem mexer em posição derivada.
167. **Fundação de cidade-filha ~25% menor**: com o limite encadeado até 6 células do miolo, filhas em 10 anos 19 contra 26 e
     fusões 11 contra 19 (seeds 1/2/3/7, `CityFoundingReport`); encadeado até o mapa dava 9 e quase nenhuma. A causa da
     fundação ainda é geométrica (casa longe do miolo), não falta de lote, água ou trabalho perto: ligar à causa real.
168. **Contrato `world-subscribe`**: a forma gravada perdeu os exemplos de `npcs`/`externalNpcs` porque a fixture não tem mais
     NPC fora de cidade (tipo igual); preencher um exemplo como o `ObservableSample` faz para os demais.
169. **Terreno na rua**: a rua só contorna água; altitude e declive não pesam (curva de nível, vau, ponte).
170. ~~**Prédio derivado sem porta para a rua**~~ AD-192: na virada do mês grava o lote (mesmas células) com a porta para o caminho.: prédio sem posição gravada (snapshot antigo) mantém a orientação por hash e cai
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
174. ~~Lama sem chuva~~: fechado pela AD-180 (chuva por região e dia; lama na terra, neve em tudo, no passo e na carroça).
175. ~~**Carga a pé não muda o passo**~~ AD-179: a carga levada alonga o passo (0,5%/kg até 40 kg), o material de obra também.: só o veículo de terra depende do caminho (cabe ou não); quem anda carregado anda igual.
176. **Reta integra o terreno só nas pontas** (`MovementCost`, como antes); a rede integra célula a célula, com subida só entre as
     pontas. Unificar quando o custo do mapa por célula for barato no caminho quente.
177. ~~**Entrada e saída da rede pela célula de caminho mais perto**~~ AD-192: melhor combinação (mais perto, primeiro da reta, mesmo pedaço). de cada ponta, não a melhor combinação; rota boa, não ótima.
178. ~~**Diarista da obra recebe o jornal sem ir à obra**~~ AD-179: aceita pela utilidade, vai pela rota, conta e recebe por hora no trecho.: a agenda dele não muda (não anda até o trecho nem deixa de ficar à toa).
179. **Cascalho não é mercadoria**: sai do leito do rio pela rota; a pedra é comprada quando o mundo tem o recurso (pedreira).
180. ~~**Mundo novo sem trilha**~~ AD-179: nasce com o chão estacionário do mês típico da história.: a vila semeada nasce sem caminho até a primeira revisão (3 meses); pisar o chão dos anos de antes
     da fundação daria rua no ano 0.
181. ~~**`cidade-media` quase não pisa o chão**~~ AD-179: era fixture sem economia; agora ~55 viagens/pessoa-mês.: ~8 passagens/mês para 150 pessoas (quase toda viagem dentro do mesmo prédio, o
     aperto do item 172); sem trilha nenhuma em 20 anos. Ver junto com o 172.
182. ~~**Prédio vira obstáculo só na virada do mês**~~ AD-192: obstáculo no ato (canteiro e obra pronta); lote novo não cobre caminho pisado. (a rota do mês é fixa); quem constrói em cima da trilha apaga o chão ali.
183. ~~**Técnica de calçar sem protótipo pago**~~ AD-192: o trecho de teste traz o cascalho do leito (idas nas horas); sem fonte, não testa. Protótipo presencial da invenção: #196.: diferente da de mobilidade (ferro do estoque), o trecho de teste não gasta recurso.
184. **Escopo Mundo remonta a lista de ruas a cada tick** (o diff é por conteúdo); guardar como o escopo Cidade se pesar.

## Dos nomes (AD-178)
185. ~~**Costume de nome não é dado de cenário**~~ — fechado na AD-183: bloco opcional `Naming` por cultura (linhagem,
     cônjuge adota o nome, chance de homenagem, nome da cidade-filha), validado, no snapshot; ausente = o sorteio da cultura.
186. ~~**Cidade ainda não fala a língua da cultura**~~ — fechado na AD-178: nome de cidade pela fonologia da cultura que funda.
187. ~~**Nome sem consequência causal**~~ — fechado na AD-178: xará e linhagem na ajuda, luto, legado, namoro e fama
     (`NameBondRules`). ~~Aberto dele: honra não respinga; fundador da cidade-filha não existe~~ — fechado na AD-183 (honra
     dos feitos que respinga como a vergonha; fundador registrado e homenageado no nome pelo costume).
188. **Mundo salvo antes da AD-178** mantém `npc-…` (decisão); renomear na carga mudaria o hash do mundo carregado.
189. ~~**Honra só de quatro feitos**~~ — fechado na AD-186: mestria no ofício, filho criado, prosperidade e coragem dão fama.
190. **Casa do fundador no largo**: o fundador não ganha lote no largo da cidade-filha. Issue #193.
191. ~~**Nome na sociedade**~~ — fechado na AD-186: o amigo de nome melhor é chamado antes para sócio (parentes primeiro).

## Do tempo de verdade (AD-180; detalhe em docs/domain/climate.md)
192. **Sem lenha, roupa nem fogo**: a casa é o único abrigo do frio; madeira só existe como insumo de obra. Ligar a casa comprando
     lenha no inverno (demanda do lenhador) e a roupa como objeto. Issue #189.
193. **Rotina e sono não seguem a luz**: a rotina é tabela fixa por hora; a luz só encurta a jornada do campo (semear e ceifar). #149.
194. **Chuva não enche rio nem poço**: não há nível d'água; só a seca reduz a fonte natural. Issue #190.
195. **Geada sobre a lavoura só no índice do ano** (geada tardia de abril–maio); decisão: não ler dia a dia (contaria duas vezes).
196. **Viagem muda com o tempo no meio do caminho** (a duração é refeita na chegada com o tempo do dia); conferir o despertar.
