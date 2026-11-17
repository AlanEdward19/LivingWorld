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
164. **Sede crítica +5%** (horas por pessoa-ano, IC90 1,027–1,075, 40 seeds × 15 anos) com o traçado ligado; morte de sede
     segue zero. Suspeita: casa mais longe do poço da fazenda e da água natural (raio 3). Medir distância casa→água por seed e
     testar poço no largo antes de mexer na regra.
165. **Casa solitária de cidade nova**: com água em volta do largo, a primeira casa pode cair a mais de `AbsorptionRingCells`
     dos 3×3 da cidade sem gente e fica fora do limite (não é desenhada). A absorção encadeada só parte do limite existente.
166. **Ruas sobre outra cidade**: a ocupação é por cidade (já era no anel), então a rua de uma pode passar por prédio de outra
     colada a ela. Cortar o traçado no limite da vizinha sem mexer em posição derivada.
167. **Fundação por prédio solto quase some**: o limite agora acompanha a vila, então o aglomerado de overflow que fundava
     cidade nova raramente aparece (o teste de escopo de civilização passou a criar a 3ª cidade à mão). Conferir em 100 anos se
     a fundação por concentração ainda acontece e se é o comportamento desejado.
168. **Contrato `world-subscribe`**: a forma gravada perdeu os exemplos de `npcs`/`externalNpcs` porque a fixture não tem mais
     NPC fora de cidade (tipo igual); preencher um exemplo como o `ObservableSample` faz para os demais.
169. **Terreno na rua**: a rua só contorna água; altitude e declive não pesam (curva de nível, vau, ponte).
170. **Prédio derivado sem porta para a rua**: prédio sem posição gravada (snapshot antigo) mantém a orientação por hash e cai
     em lote de caminho ou degrau sem rua; toda criação nova grava posição e orientação.
171. **Números sem fonte** de `CityLayoutRules` (largo 1, travessa 13, centro 5, quintal 2/1, família 5, pobre 0,5× e
     remediado 2× a mediana, desvio 4, caminho 8, pisoteio 12, valor 0,02): calibrar contra toft/croft medidos (Wharram Percy)
     quando a célula ganhar escala em metros.
