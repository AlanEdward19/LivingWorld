# LivingWorld — Frontend Experience, Living World Navigation & Spatial Simulation

> Documento final de direção de produto, UX, UI e representação espacial do LivingWorld.
>
> Este documento consolida a filosofia visual, navegação, World View, Settlement View, Agent Experience, interiores, semantic zoom, representação de veículos, timeline, causalidade e os modos Observe / Table / Inhabit.
>
> O objetivo central é simples:
>
> **LivingWorld não deve parecer uma interface que exibe dados sobre uma simulação. Deve parecer uma janela aberta para uma realidade que continua existindo e se movendo mesmo quando ninguém está olhando.**

---

# 1. Visão do produto

LivingWorld possui três experiências fundamentais sobre o mesmo mundo:

```text
1. OBSERVE
   assistir, compreender e explorar uma simulação viva

2. TABLE
   usar o mundo como cenário vivo para RPG

3. INHABIT
   futuramente assumir um Agent e viver dentro do mesmo mundo
```

Essas experiências compartilham:

```text
mesmo mundo
mesmo mapa
mesma timeline
mesmos Agents
mesmas cidades
mesmos vilarejos
mesmos acampamentos
mesmos prédios
mesmas casas
mesmos eventos
mesma história
mesmas regras
```

O frontend muda somente:

```text
perspectiva
prioridade da informação
controles disponíveis
nível de conhecimento apresentado
```

A interface nunca deve parecer três produtos diferentes.

---

# 2. Princípio principal de UX

A interface não deve parecer:

```text
um dashboard administrativo
um editor de banco de dados
uma planilha de simulação
uma ferramenta de desenvolvedor
um VTT tradicional cheio de toolbars
um RTS genérico
um conjunto de páginas desconectadas
```

Ela deve parecer:

> **uma janela para um mundo que continua existindo mesmo quando ninguém está olhando.**

O usuário deve sentir:

```text
Algo está acontecendo aqui.
↓
Quero saber o que está acontecendo naquela cidade.
↓
Quem é aquela pessoa?
↓
Onde ela está indo?
↓
Por que ela fez isso?
↓
Com quem ela está falando?
↓
Onde ela mora?
↓
O que existe dentro daquela casa?
↓
O que aconteceu antes?
↓
O que aconteceu por causa disso?
```

Esse ciclo de curiosidade é o núcleo da experiência.

---

# 3. Regra espacial fundamental

Se uma entidade possui uma localização física no mundo, então, sempre que a escala permitir, o usuário deve conseguir **vê-la ocupando essa localização**.

Isso vale para:

```text
Agents
animais
veículos
carroças
carros
aviões
navios
prédios
casas
estabelecimentos
portas
cômodos
móveis
objetos
```

A UI pode complementar essas entidades com Inspector, Timeline, dados e contexto.

Mas a representação espacial é primária.

Não substituir:

```text
uma pessoa
```

por:

```text
um card sobre a pessoa
```

quando a pessoa pode ser vista no mapa.

Não substituir:

```text
uma cidade
```

por:

```text
uma dashboard sobre a cidade
```

quando o usuário pode entrar nela.

---

# 4. North Star de navegação espacial

A navegação principal deve ser contínua:

```text
PLANET / WORLD
      ↓
WORLD MAP
      ↓
REGION
      ↓
SETTLEMENT
      ↓
DISTRICT
      ↓
LOT
      ↓
BUILDING
      ↓
FLOOR
      ↓
ROOM
      ↓
OBJECT
```

Em paralelo, a navegação por entidade deve permitir:

```text
WORLD
  ↓
AGENT
  ↓
CURRENT ACTIVITY
  ↓
LOCATION
  ↓
INTERACTION
  ↓
EVENT
  ↓
CAUSE
  ↓
CONSEQUENCE
```

O usuário deve atravessar essas escalas sem sentir que abriu páginas diferentes.

---

# 5. Princípio de câmera contínua

A transição entre escalas deve parecer aproximação e afastamento sobre a mesma realidade.

Fluxo ideal:

```text
World Map
↓ zoom
Region
↓ zoom
Settlement
↓ enter
Settlement Map
↓ click building
Building Interior
↓ floor selector
Room
```

Evitar:

```text
World Map
→ City Page
→ Household Page
→ Building Modal
→ NPC Page
```

A experiência correta é:

> **não abrir a cidade; entrar nela.**

---

# 6. Shell principal

O mesmo shell deve permanecer em todas as escalas.

```text
┌──────────────────────────────────────────────────────────────────┐
│ TOP BAR                                                         │
├──────────────┬───────────────────────────────────────┬───────────┤
│              │                                       │           │
│ EXPLORER     │            WORLD VIEWPORT             │ INSPECTOR │
│              │                                       │           │
│ contextual   │ map / scene / building / interior    │ selected  │
│ navigation   │                                       │ entity    │
│              │                                       │           │
├──────────────┴───────────────────────────────────────┴───────────┤
│ TIMELINE / EVENT STREAM                                          │
└──────────────────────────────────────────────────────────────────┘
```

O shell permanece.

O conteúdo central muda de escala.

---

# 7. Modos de experiência

No topo:

```text
[ Observe ▼ ]
```

Opções:

```text
Observe
Table
Inhabit
```

Enquanto Inhabit ainda não estiver implementado:

```text
Inhabit
Coming later
```

Não apresentar como feature quebrada.

---

# 8. Observe Mode

Objetivo:

> Assistir, compreender e explorar o mundo.

Prioridades:

```text
mapa
movimento
acontecimentos
tempo
população
sociedades
Agents
locais
causalidade
história
```

Controles principais:

```text
Pause
Play
Speed
Timeline
Follow
Layers
Search
```

---

# 9. Table Mode

Objetivo:

> Usar LivingWorld como mundo vivo para RPG.

Mesmo mundo.

A interface passa a priorizar:

```text
locations
NPCs
factions
relationships
events
secrets known to GM
session context
quick references
```

O mapa continua sendo protagonista.

Table Mode não deve substituir o simulador por um VTT tradicional.

---

# 10. Inhabit Mode

Objetivo futuro:

> Controlar um Agent existente dentro do mesmo mundo.

Adicionar:

```text
action bar
local awareness
inventory
interaction controls
conversation controls
```

Sem reconstruir a UI.

O Agent controlado continua sendo um Agent normal do simulation core.

---

# 11. World View

A tela padrão ao entrar no mundo deve ser o mapa.

Pequeno overlay:

```text
ELDORIA
Year 328 · Spring

23,481 inhabitants
37 settlements
```

O mundo deve dominar visualmente.

---

# 12. O mapa mundi é um mundo vivo

O mapa mundi deve mostrar fisicamente:

```text
continentes
oceanos
rios
montanhas
florestas
biomas
estradas
rotas
fronteiras
portos
cidades
vilarejos
acampamentos
postos
fortalezas
ruínas
fazendas
minas
colônias
```

Esses locais devem possuir representação visual diferente de acordo com:

```text
tipo
tamanho
população
importância
arquitetura / tema do mundo
```

Não usar pins genéricos de mapa.

---

# 13. Settlement Types

Tipos possíveis incluem:

```text
Camp
Hamlet
Village
Town
City
Metropolis
Outpost
Fort
Station
Colony
Port
Custom
```

Não hardcode medieval.

A forma visual deve mudar conforme o período e o tema.

Exemplo:

```text
medieval village
→ casas espalhadas, estrada de terra, fazendas

modern city
→ quadras, avenidas, prédios, veículos

space colony
→ módulos, airlocks, habitações, hangares
```

---

# 14. Agents são sempre visíveis no mapa

Regra importante:

> **Agents não desaparecem apenas porque a câmera está distante.**

A representação muda semanticamente conforme o zoom.

### Zoom distante

Agents aparecem como pequenas bolinhas coloridas.

```text
• • •  •
    •       •
  •     •
```

As bolinhas continuam se movendo pelo mapa.

O usuário consegue observar:

```text
pessoas viajando
fluxos locais
pessoas saindo de cidades
pessoas entrando em cidades
caravanas
migração
movimento entre regiões
```

### Zoom médio

As bolinhas podem ganhar:

```text
ligeira diferenciação visual
nome em hover
atividade
origem / destino
```

### Zoom próximo

A representação muda para sprite, token ou personagem visual completo.

O Agent nunca muda de entidade.

Somente muda de representação.

---

# 15. Cores dos Agents

As bolinhas devem ter cores discretas e legíveis.

A cor pode representar, dependendo da layer ativa:

```text
identidade visual estável
grupo / cultura
faction
estado de seleção
atividade
relationship layer
```

Evitar rainbow UI permanente.

Por padrão, usar identidade visual estável do Agent ou grupo.

Layers podem temporariamente recolorir.

---

# 16. Veículos no mapa

Veículos também devem permanecer visíveis em qualquer zoom relevante.

Exemplos:

```text
carroça
carruagem
carro
ônibus
caminhão
trem
navio
avião
nave
```

Em zoom distante, aparecem como círculos maiores que Agents.

Exemplo:

```text
Agent:       •
Carruagem:   ●
Ônibus:      ●
Navio:       ◉
```

A diferenciação exata pode usar:

```text
tamanho
contorno
ícone interno simples
trail opcional
```

Sem transformar o mapa em radar militar.

---

# 17. Movement rendering

O movimento visual deve ser interpolado.

A simulação pode atualizar posição em menor frequência.

O renderer deve exibir movimento suave.

```text
simulation state
5–10 updates/sec

visual interpolation
60fps quando disponível
```

Não é necessário refletir cada simulation tick.

---

# 18. World travel

Selecionando um Agent em viagem:

```text
Mira Valen
Traveling

Oakbridge
──────────────→
Dawnport

2.4 days remaining
```

O usuário pode clicar:

```text
Follow
```

A câmera acompanha o Agent sem impedir exploração manual.

---

# 19. Follow Agent

Follow deve ser uma das experiências centrais.

Exemplo:

```text
Mira leaves Oakbridge
↓
camera follows
↓
Mira crosses Westreach
↓
stops at a camp
↓
continues in the morning
↓
reaches Dawnport
↓
camera enters Dawnport
↓
Mira walks through streets
↓
enters an inn
↓
roof fades
↓
Mira sits
↓
talks to innkeeper
```

O usuário pode interromper Follow a qualquer momento.

---

# 20. Migration e grandes fluxos

A layer de Migration pode mostrar fluxos sutis.

Mas Agents continuam individualmente visíveis como pontos quando tecnicamente possível.

Fluxos são uma camada analítica adicional.

Não substituir Agents por flow lines permanentemente.

---

# 21. Entrando em um Settlement

Ao aproximar a câmera de um Settlement:

```text
world marker
↓
settlement silhouette
↓
roads appear
↓
blocks / lots appear
↓
buildings appear
↓
Agents resolve into local representation
```

A transição deve acontecer sem tela de loading sempre que possível.

Se houver streaming:

```text
terrain remains visible
local geometry resolves progressively
```

---

# 22. Settlement View

A Settlement View deve ser um mapa espacial real.

Exemplo conceitual:

```text
┌──────────────────────────────────────────────────────────────┐
│ 🌲 🌲                       FARM                            │
│                                                             │
│             ┌──────────────┐                                │
│             │ Miller Farm  │                                │
│             └───────┬──────┘                                │
│                     │                                       │
│ ════════════════════╪══════════════════════════════════════ │
│                     │                                       │
│ ┌─────────┐   ┌──────────┐   ┌──────────────┐              │
│ │ House   │   │ Bakery   │   │ Blacksmith   │              │
│ │ Valen   │   │          │   │              │              │
│ └─────────┘   └──────────┘   └──────────────┘              │
│     Mira ● →       ● Tomas                                  │
│                                                             │
│ ┌─────────┐                 Market Square                   │
│ │ Tavern  │               ● ● ● ●                          │
│ └─────────┘                                                  │
└──────────────────────────────────────────────────────────────┘
```

---

# 23. Settlement não é dashboard

Settlement Pulse, Economy, People e History continuam existindo.

Mas são painéis secundários.

A cidade em si é o mapa.

Evitar experiência:

```text
Oakbridge
Population 842
Food Scarce
Employment Stable
[People] [Economy] [History]
```

como tela principal.

Preferir:

```text
visualizar Oakbridge fisicamente
+
abrir dados quando necessário
```

---

# 24. Estrutura espacial de Settlement

Settlement pode conter:

```text
Districts
Roads
Streets
Paths
Lots
Buildings
Public Spaces
Farms
Walls
Gates
Bridges
Markets
Docks
Industrial Areas
Religious Buildings
Administrative Buildings
Housing
```

Tudo deve ser derivado dos sistemas reais do mundo quando esses sistemas existirem.

---

# 25. NPC local representation

Em Settlement View, Agents deixam de ser apenas círculos.

Podem usar:

```text
2D sprites
top-down characters
minimal silhouettes
low-detail animated tokens
```

O estilo depende da direção artística.

O requisito é comportamental, não gráfico.

O usuário deve conseguir perceber:

```text
walking
running
standing
sitting
sleeping
working
eating
talking
fighting
carrying
using object
entering building
leaving building
boarding vehicle
```

---

# 26. Activity animation

Cada atividade importante deve possuir representação visual simples.

Exemplos:

```text
Walking
→ walk cycle

Talking
→ faces target + subtle speech indicator

Sleeping
→ lying animation + minimal sleep indicator

Eating
→ seated / standing eat animation

Working
→ context animation

Reading
→ holding object / seated animation
```

Não precisa ser cinematográfico.

Precisa ser compreensível.

---

# 27. Conversas visíveis

Quando dois ou mais Agents conversam:

```text
Agents stop or slow
↓
orient toward one another
↓
subtle conversation indicator appears
```

Exemplo:

```text
Mira ●  ↔  ● Rowan
        Talking
```

Hover:

```text
Mira Valen
Talking with Rowan Arl
Topic: Food shortage
```

---

# 28. Conteúdo de conversa

Se o simulation core possuir diálogo real:

```text
Conversation

Mira Valen
Rowan Arl

Mira:
"Have prices gone up again?"

Rowan:
"The northern caravan hasn't arrived."
```

Se o core possuir apenas evento social, tópico ou intent:

```text
Discussing food shortage
```

Não inventar diálogo.

O frontend apresenta o que o core sabe.

---

# 29. Edifícios são entidades físicas

Cada prédio deve existir no espaço.

Exemplos:

```text
House
Bakery
Inn
Temple
Workshop
Warehouse
Market
School
Hospital
Factory
Office
Barracks
Farmhouse
Hangar
Station
```

O prédio pode possuir:

```text
owner
residents
organization
purpose
opening state
access rules
floors
rooms
objects
```

---

# 30. Entrando em edifícios

Quando um Agent atravessa uma porta:

```text
Agent approaches door
↓
door opens
↓
Agent crosses threshold
↓
interior becomes visible
↓
Agent continues movement inside
```

O usuário pode acompanhar sem mudar de página.

---

# 31. Roof cutaway

Para interiores, usar lógica semelhante a jogos top-down como RimWorld.

Exterior:

```text
roof visible
interior hidden
```

Ao entrar, selecionar ou aproximar:

```text
roof fades / cuts away
↓
walls remain readable
↓
rooms appear
↓
furniture appears
↓
Agents inside become visible
```

Esse comportamento pode ser automático ou controlável.

---

# 32. Building interior

Exemplo:

```text
┌───────────────────────────────────────┐
│ VALEN HOUSE — Ground Floor            │
│                                       │
│ ┌───────────────┐ ┌───────────────┐  │
│ │ Kitchen       │ │ Dining Room   │  │
│ │ stove         │ │ table         │  │
│ │ counter       │ │ chairs        │  │
│ │      ● Mira   │ │               │  │
│ └───────┬───────┘ └───────┬───────┘  │
│         │                   │          │
│ ┌───────┴───────────────────┴───────┐  │
│ │ Hall                      ● Nora  │  │
│ └────────────────┬──────────────────┘  │
│                  │ stairs ↑            │
└───────────────────────────────────────┘
```

---

# 33. Floors

Buildings podem possuir:

```text
Basement
Ground Floor
Floor 1
Floor 2
...
Roof
```

Controlador compacto:

```text
Floor
[B1] [G] [1] [2] [Roof]
```

NPC em outro andar pode gerar indicador discreto:

```text
↑ Tomas
↓ Mira
```

Clicar muda para o andar correspondente.

---

# 34. Escadas, elevadores e transições verticais

O pathfinding deve considerar elementos reais:

```text
stairs
elevators
ladders
ramps
portals
lifts
```

Exemplo:

```text
Bedroom
↓
Hall
↓
Stairs
↓
Ground Floor
↓
Front Door
↓
Street
```

A visualização deve acompanhar isso.

---

# 35. Rooms

Cada andar pode conter Rooms.

Exemplo:

```text
Kitchen
Bedroom
Bathroom
Storage
Workshop
Office
Dining Room
Living Room
Hall
Cellar
Garage
Classroom
Ward
Shop Floor
```

Room não precisa ser hardcoded.

Pode ser semanticamente derivado.

---

# 36. Furniture e Objects

Cômodos possuem objetos físicos.

Exemplos:

```text
beds
chairs
tables
stoves
ovens
shelves
cabinets
desks
workbenches
lamps
storage
machinery
computers
vehicles
```

Objetos interativos podem possuir:

```text
position
orientation
state
owner
availability
capacity
current user
```

---

# 37. Object interaction

Quando um Agent usa um objeto:

```text
Agent moves to interaction point
↓
faces / aligns with object
↓
activity animation begins
↓
object state may change
```

Exemplos:

```text
sit on chair
sleep in bed
cook at stove
work at bench
use computer
open storage
board vehicle
```

---

# 38. Household não é somente uma ficha

Household continua possuindo dados:

```text
members
resources
income
pressures
history
relationships
```

Mas quando possuir uma residência física, deve existir conexão direta:

```text
Household
→ Home
→ Building
→ Floor
→ Room
```

Exemplo:

```text
Valen Household
4 members
Home: 17 Market Street

[Go to Home]
```

---

# 39. Agent location model

Um Agent pode possuir localização espacial hierárquica:

```text
World
Region
Settlement
District
Building
Floor
Room
Tile / Position
```

Exemplo:

```text
Mira Valen
Eldoria
→ Westreach
→ Oakbridge
→ Market District
→ Valen House
→ Ground Floor
→ Kitchen
→ (14,22)
```

O frontend pode mostrar apenas o nível relevante.

---

# 40. Agent Inspector

Ao selecionar um Agent:

```text
Mira Valen
Baker · 34
Oakbridge

CURRENTLY
Walking home from Dawn Oven

Hungry · Tired · Healthy
```

Primeira pergunta respondida:

> O que ela está fazendo agora?

Depois:

```text
Why?
Where?
Household
Relationships
Needs
Body
Beliefs
Memories
Goals
Recent Life
```

---

# 41. Where?

Botão importante:

```text
Where?
```

Pode:

```text
center map
change floor
open building
follow Agent
```

A localização deve ser visual, não apenas textual.

---

# 42. Why?

Ao clicar em Why?:

```text
WHY IS MIRA GOING HOME?

1. Her work shift ended.
2. She is tired.
3. Her household is nearby.
4. She expects to eat at home.
```

Detailed mode pode mostrar dados técnicos reais.

---

# 43. Agent movement inside buildings

Movement deve continuar funcionando dentro de interiores.

Exemplo:

```text
bedroom
→ hallway
→ stairs
→ kitchen
→ front door
→ street
```

Não teleportar visualmente entre room states salvo quando LOD exigir.

---

# 44. Off-screen simulation vs visible simulation

O core pode simular entidades fora da viewport com LOD diferente.

Quando uma entidade entra na área visível:

```text
state is resolved
↓
position and activity are reconstructed from canonical state
↓
visual representation resumes
```

Nunca mudar canonical truth por causa do renderer.

---

# 45. Semantic zoom

Semantic zoom não significa esconder o mundo.

Significa mudar **como** a mesma informação é representada.

Exemplo:

```text
Agent
far:       tiny colored dot
medium:    larger dot + hover info
near:      sprite / token
inside:    detailed animated character
```

Settlement:

```text
far:       symbol
medium:    footprint / silhouette
near:      streets and districts
inside:    buildings and Agents
```

Vehicle:

```text
far:       larger circle
medium:    circle + class cue
near:      vehicle sprite/model
```

---

# 46. Map visual hierarchy

Prioridade visual:

```text
1. selected entity
2. followed entity
3. important current event
4. Agents / vehicles in motion
5. settlements / buildings
6. routes / roads
7. terrain
8. secondary labels
```

O usuário deve sempre saber onde olhar.

---

# 47. Layers

Layers possíveis:

```text
Population
Migration
Economy
Resources
Political
Relationships
Extraordinary
Events
Terrain
Traffic
Ownership
Housing
```

Uma ou duas layers fortes por vez.

O mapa base continua legível.

---

# 48. Event markers

Evento importante pode gerar:

```text
small pulse once
```

Depois vira marker discreto.

Sem piscar permanentemente.

Eventos que ocorreram dentro de prédio podem apontar diretamente para:

```text
Settlement
Building
Floor
Room
```

---

# 49. Causal Explorer

Eventos devem permitir navegação causal.

```text
BEFORE
Poor Harvest
↓
Grain Stock Fell
↓

SELECTED
Grain Prices Rose
↓

AFTER
Purchases Failed
↙            ↘
Mira Hungry   Bakery Costs ↑
```

Preferir spine vertical e consequências ramificadas.

---

# 50. Causal navigation back to world

Qualquer fator causal clicável deve permitir voltar ao mapa.

Exemplo:

```text
Mira became hungry
→ Go to Mira

Dawn Oven reduced production
→ Go to Dawn Oven

Grain stock declined
→ Go to warehouse / market if spatially represented
```

Causalidade e espaço devem se conectar.

---

# 51. Timeline

Timeline inferior permanece disponível.

Collapsed:

```text
▴ Year 328 · Spring ━━━━━━━●━━━━━━━━━━━━
```

Expanded:

```text
Timeline

328.11 Grain price rose
328.10 Valen household failed purchase
328.09 Grain stock became low
328.03 Harvest estimate declined
```

---

# 52. Historical replay

Ao navegar para o passado:

```text
VIEWING HISTORY
Year 312
Simulation currently at Year 328
[Return to Live]
```

Idealmente o mapa também mostra o estado histórico correspondente quando snapshots permitirem.

---

# 53. Context preservation

Ao navegar:

```text
World
→ Oakbridge
→ Valen House
→ Kitchen
→ Mira
→ Event
```

Back deve preservar:

```text
camera position
zoom
selected floor
selected entity
layers
filters
timeline state
```

Não resetar a câmera.

---

# 54. Breadcrumb

Breadcrumb pode refletir espaço:

```text
World / Westreach / Oakbridge / Market District / Valen House / Ground Floor
```

ou entidade:

```text
World / Mira Valen / Current Event
```

Cada item clicável.

---

# 55. Global Search

Atalho:

```text
Ctrl / Cmd + K
```

Placeholder:

```text
Search people, places, buildings, events...
```

Resultados:

```text
PEOPLE
Mira Valen

PLACES
Oakbridge
Valen House
Dawn Oven

EVENTS
Mira left Oakbridge
```

Selecionar resultado navega espacialmente até ele quando possível.

---

# 56. Explorer Sidebar

Estrutura sugerida:

```text
Overview
Followed
Places
People
Organizations
Threads
Events
```

Quando dentro de Settlement, Places pode mostrar:

```text
Districts
Buildings
Public Spaces
Roads
Homes
Businesses
```

Sem substituir o mapa.

---

# 57. Settlement Inspector

Selecionar cidade mostra painel contextual:

```text
OAKBRIDGE
Market Town · Population 842
Westreach

Population
Food
Employment
Migration
Housing

Recent Events

[Open Overview]
[Timeline]
[Layers]
```

Mas o centro continua mostrando Oakbridge fisicamente.

---

# 58. Building Inspector

Selecionando um prédio:

```text
DAWN OVEN
Bakery
Market District · Oakbridge

Open
Owner: Corvin Hale
Workers: 6
Visitors: 3

Current Activity
Baking bread

Floors: 2
Rooms: 8

[Enter]
[People Inside]
[History]
```

---

# 59. Room Inspector

Selecionando um cômodo:

```text
KITCHEN
Valen House
Ground Floor

Occupants: 2
Objects: 11

Currently
Mira is preparing food
Nora is sitting at the table
```

---

# 60. People Inside

Para prédio ou room:

```text
PEOPLE INSIDE

Mira Valen — Cooking
Nora Valen — Sitting
Tomas Valen — Upstairs
```

Clicar seleciona e centraliza.

---

# 61. Day / Night

Se o mundo possuir ciclo:

```text
lighting changes smoothly
windows may light up
streets become darker
Agents change routines
businesses close
people go home
```

Interface permanece legível.

Não escurecer painéis excessivamente.

---

# 62. Seasons

Mudanças sutis:

```text
foliage
snow
terrain tint
weather
clothing if supported
activity patterns
```

A UI estrutural não muda.

---

# 63. Weather

Weather pode aparecer fisicamente:

```text
rain
snow
fog
wind cues
storms
```

Sem prejudicar leitura do mapa.

Agents podem responder visualmente quando o simulation core suportar.

---

# 64. Sound

Opcional e discreto.

World:

```text
wind
water
ambient landscape
```

Settlement:

```text
crowd ambience
market
animals
industry
vehicles
```

Interior:

```text
room tone
fireplace
workshop
conversation murmur
```

Não usar som constante de UI.

---

# 65. Design philosophy

A estética combina:

```text
living map
+
simulation observer
+
interactive atlas
+
historical archive
+
character inspector
```

O mundo é protagonista.

A interface recua visualmente.

---

# 66. Visual identity

Base:

```text
dark-neutral
warm
quiet
precise
atmospheric
```

Evitar estética excessivamente:

```text
fantasy
sci-fi
corporate
game HUD
```

porque o mundo pode representar qualquer era.

---

# 67. Color system

```css
:root {
  --bg-world: #0B0E12;

  --surface-1: #11151B;
  --surface-2: #171C23;
  --surface-3: #20262F;

  --text-primary: #ECEFF4;
  --text-secondary: #AAB2BF;
  --text-muted: #707A89;
  --text-disabled: #4D5561;

  --accent: #D5A85A;
  --accent-secondary: #6FA6A1;

  --positive: #7FA77B;
  --warning: #C69B58;
  --danger: #C86F6F;
  --info: #6F91B8;
  --unknown: #7D748F;

  --cause: #728EB6;
  --consequence: #A77A68;

  --border: rgba(255,255,255,0.07);

  --radius-sm: 4px;
  --radius-md: 6px;
  --radius-lg: 10px;

  --panel-left: 260px;
  --panel-right: 340px;
  --topbar-height: 48px;
}
```

Valores são ponto de partida.

---

# 68. Typography

Usar fonte sans-serif altamente legível.

Exemplos:

```text
Inter
Geist
```

Escala compacta e clara.

Não usar tiny unreadable text.

---

# 69. Panels

Explorer e Inspector devem ser recolhíveis.

O mapa deve poder ocupar quase toda a tela.

Inspector nunca deve bloquear a entidade selecionada se puder ser reposicionado logicamente.

---

# 70. Microinteractions

Selecionar Agent:

```text
representation gains subtle selection ring
Inspector updates
camera subtly centers if needed
```

Duração curta:

```text
150–220ms
```

Sem spring exagerado.

---

# 71. Animation principle

Animações devem comunicar:

```text
movement
state transition
entry / exit
selection
activity
```

Não adicionar animações decorativas permanentes.

---

# 72. Reduced Motion

Quando ativo:

```text
disable smooth camera travel where unnecessary
reduce interpolation
replace pulses with static markers
reduce panel motion
```

Mas manter localização compreensível.

---

# 73. Performance architecture

Frontend não deve depender de full world rerender.

Preferir:

```text
initial snapshot
+
delta updates
+
event stream
```

O renderer não recalcula simulation truth.

---

# 74. Spatial read models

Backend deve oferecer read models específicos.

Exemplos:

```text
WorldOverview
WorldSpatialSlice
RegionSpatialSlice
SettlementOverview
SettlementSpatialSlice
BuildingOverview
BuildingSpatialSlice
FloorSpatialSlice
RoomOverview
AgentOverview
AgentDetails
AgentPosition
AgentActivity
AgentDecisionExplanation
AgentLife
HouseholdOverview
VehicleOverview
EventOverview
CausalNeighborhood
TimelineSlice
StoryThread
```

---

# 75. Spatial delta updates

Exemplos de deltas:

```text
AgentMoved
AgentEnteredBuilding
AgentExitedBuilding
AgentChangedFloor
AgentEnteredRoom
AgentStartedActivity
AgentStoppedActivity
ConversationStarted
ConversationEnded
VehicleMoved
DoorOpened
DoorClosed
ObjectStateChanged
```

O frontend anima a transição.

---

# 76. LOD

LOD pode reduzir custo de simulação e rendering.

Mas não deve destruir continuidade visual.

Exemplo:

```text
far away
Agent position updated less frequently
rendered as dot

nearby
Agent position updated more frequently
rendered as sprite

inside selected building
high-detail activity rendering
```

---

# 77. Rendering budget

Não renderizar todos os detalhes do planeta simultaneamente.

Priorizar:

```text
viewport
nearby areas
followed entities
selected entities
important events
```

Mas Agents distantes no world map continuam representados como pontos usando agregação / efficient rendering quando necessário.

---

# 78. Large populations

Para milhares ou milhões de Agents, usar técnicas como:

```text
GPU instancing
batched point rendering
tile-based streaming
LOD updates
spatial partitioning
culling outside viewport
```

Evitar DOM element por Agent.

---

# 79. Vehicles and passengers

Vehicle pode conter Agents.

Exemplo:

```text
Carriage #41
Passengers: 5
Driver: Rowan Arl

Oakbridge → Dawnport
```

No world map, mostrar o veículo como círculo maior.

Não mostrar cinco bolinhas separadas sobrepostas durante transporte se os Agents estiverem fisicamente dentro do veículo.

Inspector permite abrir passageiros.

---

# 80. Aircraft

Em períodos que possuem aviação:

```text
avião aparece como marcador maior
movimento mais rápido
altitude opcional no Inspector
rota visual opcional
```

Quando selecionado:

```text
Flight 217
Dawnport → Northreach
Altitude: 9,400m
Passengers: 84
```

A informação depende do simulation core real.

---

# 81. Ships

Navios aparecem fisicamente nos oceanos e rios navegáveis.

Em zoom distante:

```text
larger circle / maritime marker
```

Em zoom próximo:

```text
ship sprite / model
```

Podem ser seguidos como qualquer Agent ou Vehicle.

---

# 82. No fake world

Frontend nunca inventa:

```text
conversation
position
room
object
historical event
relationship
cause
activity
```

Se o core não fornece informação suficiente, mostrar estado genérico legítimo.

Exemplo:

```text
Social interaction
```

em vez de inventar diálogo.

---

# 83. World Creator continuity

A criação do mundo deve terminar no mesmo universo visual.

Fluxo:

```text
Create World
↓
Generate
↓
planet forms
↓
settlements appear
↓
routes grow
↓
history advances
↓
THE WORLD IS ALIVE
↓
Enter World
↓
camera descends
↓
World View
```

Não:

```text
menu
→ form
→ loading
→ dashboard
```

---

# 84. Enter World transition

Ao clicar Enter World:

```text
camera descends from orbit
↓
planet fills screen
↓
world map resolves
↓
Agents appear as moving colored dots
↓
settlements resolve
↓
UI shell appears around the world
```

---

# 85. Continue World

Ao continuar:

```text
planet preview
↓
camera approaches
↓
return to last map center
↓
restore zoom
↓
restore selected entity
↓
restore open building / floor if applicable
↓
restore timeline state
```

---

# 86. Resume Context

Salvar:

```text
map center
zoom
selected entity
selected settlement
selected building
selected floor
open panel
mode
layers
timeline state
follow target
```

---

# 87. Accessibility

Obrigatório:

```text
WCAG contrast
keyboard navigation
visible focus
screen-reader labels
not relying only on color
reduced motion
```

Agents representados por cor devem possuir alternativas:

```text
hover label
selection outline
shape / size variation where appropriate
accessible list equivalents
```

---

# 88. Keyboard controls

Sugestão:

```text
Space    Pause / Play
1        1×
2        5×
3        20×
F        Follow selected
W        World View
/        Search
Esc      Close current overlay
?        Keyboard help
```

Para floors:

```text
PageUp / PageDown
```

se não houver conflito.

---

# 89. Important Event Presentation

Evento importante pode gerar toast:

```text
KING ARVEN HAS DIED
Year 328 · Spring 11
[View Event]
```

Clicar:

```text
centers relevant location if applicable
opens Event Inspector
preserves map context
```

---

# 90. No gamification noise

Evitar:

```text
XP popups
achievement spam
confetti
+10 floating numbers
loot effects
```

LivingWorld deve parecer mundo, não mobile game.

---

# 91. Visual anti-patterns

Não usar:

```text
20 cards simultâneos
gradients neon
glassmorphism pesado
borders brilhantes
huge rounded corners
rainbow status colors
giant sidebar icons
permanent decorative animations
glowing selected objects
tiny unreadable text
dashboard charts everywhere
```

---

# 92. UX anti-patterns

Não:

```text
abrir nova página para qualquer entidade
perder mapa ao inspecionar pessoa
resetar zoom ao voltar
obrigar modal para tudo
mostrar todos os atributos simultaneamente
misturar debug com experience mode
atualizar tela inteira a cada tick
transformar cidade em dashboard
transformar casa em card
teleportar visualmente Agents sem necessidade
esconder Agents apenas porque o zoom está distante
```

---

# 93. Settlement QA

Antes de aprovar Settlement View:

```text
Consigo ver as ruas?
Consigo ver os prédios?
Consigo distinguir casas e estabelecimentos?
Consigo ver Agents andando?
Consigo ver Agents entrando e saindo?
Consigo selecionar uma pessoa diretamente?
Consigo entrar em um prédio?
Consigo ver pessoas dentro?
Consigo acompanhar uma pessoa sem perder contexto?
A cidade parece um lugar ou um dashboard?
```

---

# 94. Building QA

```text
Consigo ver os cômodos?
Consigo ver móveis relevantes?
Consigo saber em qual andar estou?
Consigo ver quem está dentro?
Consigo acompanhar alguém entrando pela porta?
Consigo seguir alguém pelas escadas?
O roof cutaway é legível?
Posso sair do prédio sem perder posição?
```

---

# 95. Agent QA

```text
Consigo ver onde ele está?
Consigo saber o que ele está fazendo?
Consigo saber para onde está indo?
Consigo vê-lo se mover?
Consigo vê-lo entrar em prédios?
Consigo vê-lo conversar?
Consigo vê-lo dormir, trabalhar ou comer?
Consigo saber por quê?
Consigo acessar família e relações?
Consigo seguir sua vida espacialmente?
```

---

# 96. World Map QA

```text
Consigo ver Agents mesmo em zoom distante?
Eles aparecem como bolinhas legíveis?
Consigo perceber movimento entre cidades?
Veículos aparecem maiores que Agents?
Consigo distinguir settlements por tipo e tamanho?
Consigo acompanhar uma viagem?
O mapa continua legível com muitos Agents?
O zoom muda representação sem quebrar continuidade?
```

---

# 97. Implementação sugerida por etapas

## UI-1 — Core Shell

```text
Top Bar
Explorer
World Viewport
Inspector
Timeline
```

## UI-2 — Spatial World Map

```text
terrain
settlements
roads
routes
Agent dots
vehicle dots
movement interpolation
semantic zoom
```

## UI-3 — Entity Navigation

```text
World
Region
Settlement
Building
Floor
Room
Agent
Event
```

com deep links e back context.

## UI-4 — Settlement Spatial View

```text
roads
lots
buildings
public spaces
local Agent movement
local vehicles
entry / exit
```

## UI-5 — Building & Interior View

```text
roof cutaway
floors
rooms
doors
stairs
furniture
Agents inside
```

## UI-6 — Agent Activity Experience

```text
walking
working
sleeping
eating
talking
sitting
object interaction
Follow Agent
```

## UI-7 — Agent Inspector

```text
Currently
Where?
Why?
Body
Needs
Relationships
Beliefs
Memories
Goals
Life View
```

## UI-8 — Conversation Experience

```text
conversation indicator
participants
topic
actual dialogue if available
social event fallback
```

## UI-9 — Causal Experience

```text
Event View
Causal Explorer
Causes
Consequences
Story Threads
```

## UI-10 — Timeline & Replay

```text
World history
Entity history
Causal threads
Replay navigation
```

## UI-11 — Follow, Search & Navigation

```text
Follow
Global Search
Notifications
Explorer shortcuts
Where?
```

## UI-12 — Table Mode

```text
Session
Cast
Locations
Threads
Notes
GM context
```

## UI-13 — Spatial Performance

```text
GPU point rendering
instancing
spatial streaming
LOD
culling
batched deltas
```

## UI-14 — Visual Polish

Somente depois:

```text
colors
spacing
typography
motion
icons
sounds
empty states
loading
```

---

# 98. Product North Star

LivingWorld não deve ser uma interface que mostra:

```text
Population: 842
Food: Scarce
Mira: Traveling
```

como experiência principal.

Deve permitir que o usuário veja:

```text
842 pessoas ocupando uma cidade
mercado ficando vazio
Mira saindo da padaria
Mira caminhando pela rua
Mira entrando em casa
Mira subindo as escadas
Mira sentando à mesa
Mira falando com Tomas
Mira indo dormir
```

E então, quando quiser entender:

```text
Who?
Where?
What?
Why?
Before?
After?
```

A interface fornece a resposta.

---

# 99. Frase guia de implementação

> **Mostrar primeiro. Explicar depois.**

Se algo pode ser observado no mundo, ele deve ser observado no mundo.

O Inspector existe para explicar o que o usuário está vendo.

Não para substituir o que deveria estar visível.

---

# 100. Regra final

O teste definitivo para qualquer tela é:

> **Estou olhando para uma interface sobre um mundo, ou estou olhando para o próprio mundo?**

A resposta correta para LivingWorld deve ser:

> **Estou olhando para o mundo.**

O usuário deve conseguir começar vendo um planeta inteiro, identificar uma pequena bolinha atravessando um continente, aproximar a câmera, descobrir que aquela bolinha é Mira Valen, segui-la até Oakbridge, vê-la entrar por uma rua, atravessar a porta de sua casa, subir um andar, entrar no quarto e dormir — sem que, em nenhum momento, pareça que ele saiu da mesma realidade.

Esse é o contrato visual e de experiência do LivingWorld.
