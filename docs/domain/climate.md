# Clima, estações e tempo (AD-135, AD-180, AD-189)

O motor gera o **tempo de cada dia por região** (`Climate/WeatherSystem`, `Daily`, primeira hora) a partir da climatologia do
lugar do cenário (`WeatherRules`, dentro de `ClimateRules.Weather`) e do RNG do mundo. Tudo que depende de frio, chuva, neve,
lama, seca ou luz lê esse estado (`Climate/WeatherNow`); nada é só visual.

## Antes da AD-180
- Temperatura: base da célula (bioma/altitude) + delta da estação em **degrau de trimestre** (`TemperatureSeasonSystem`); lida pela
  lavoura (grau-dia, frio que impede lavrar) e pela flora. Sem dia × noite, sem dia a dia, sem extremos.
- Safra: fator anual AR(1) **sorteado à parte** (AD-135); "seca" era só o rótulo do fator < 600. Doença sazonal pelo mês do calendário.
- Não existiam: chuva, neve, geada, vento, lama, luz do dia, frio no corpo, seca de verdade.

## O tempo do dia (por região; `WeatherLedger` no snapshot e no hash)
- **Estação suave**: média, amplitude, dias de chuva, chuva e vento do mês interpolados entre os meios dos meses. Hemisfério sul
  (latitude < 0) desloca seis meses. A climatologia vale na célula da primeira cidade; as outras ficam acima ou abaixo pela
  diferença de base (bioma/altitude) × 0,325 (`CellOffsetScale`: 6,5 °C/km com a unidade de altitude tomada como 50 m).
- **Temperatura**: anomalia AR(1) diária (persistência 0,8; DP do mês) com parte comum às regiões (85%); mínima e máxima pela
  amplitude (menor em dia de chuva); curva do dia com mínima às 5 h e máxima às 15 h (`TemperatureAt`); sensação com vento e chuva.
- **Chuva em frentes**: cadeia de Markov (P(chuva|chuva) 0,65), intensidade exponencial com a média do mês; **neve** com média ≤ 1 °C;
  **geada** com mínima < 0; **tempestade** ≥ 15 mm (vento ≥ 14 m/s); **granizo** em tempestade ≥ 20 mm com média ≥ 12 °C.
- **Chão**: neve acumula e derrete (4 mm/°C-dia); terra molhada (‰) sobe com chuva e degelo e seca com o calor; congelada ou sob
  neve não seca. Faixas: molhado ≥150, lama ≥400, lamaçal ≥750. **Seca**: média móvel de 60 dias da chuva < 50% da normal.
- **Regime do ano**: em janeiro um AR(1) anual (persistência 0,7; parte comum do clima) muda a frequência de chuva (±15%/DP) e a
  temperatura (−0,4 °C/DP): anos chuvosos e frios em série. **Fator de safra** (1º de julho) = índice da estação jan–jun (primavera
  chuvosa ou muito seca, primavera fria, geada tardia, inverno que congela a semente sem neve), padronizado para média 1000 e DP 200,
  cauda de baixo encolhida pela metade (o pior ano tira 40–50%, não 75%): medido 1001 ± 196, autocorrelação 0,23, 2,1% < 600.
- **Luz**: horas de sol pela latitude e o dia do ano (declinação; Oxford: 7,6 h em dezembro, 16,4 h em junho).
- **Vento com direção (AD-189)**: de onde vem (graus do norte; norte = −y do mapa), streams próprios (`weather-wind`): a direção de
  ontem continua (65%, girando até ±30°) ou sai da rosa do lugar (8 setores; Inglaterra: SW 24%, W 19%, S 12%...). **Advecção**: o ar
  traz a temperatura de onde veio — no inverno o sudoeste é ameno e o leste/nordeste gelado (±2,4 °C em janeiro), no verão o sudeste
  esquenta (±0,6–1,0 °C); centrada na rosa (a média do mês não muda) e o ruído AR(1) perde a variância que ela explica. Medido:
  inverno de SW ~3 °C mais quente que de E/NE; safra 993 ± 193 (3.000 anos; `CropIndexMean` recalibrado).
- **Folhagem (AD-189)**: por região, 0–1000 e fase (`bare`, `budding`, `full`, `falling`). Grau-dia > 5 °C desde o meio do inverno: brota
  em 120 e enche 220 depois (Oxford: abril a fim de maio); com a luz < 12,5 h no outono, grau-dia < 14 °C acumula, a copa vira
  e com 110 a folha cai em 30 dias (fim de outubro a dezembro). Primeiro dia do mundo: somas pelo valor esperado da climatologia.

## Consequências (sistema que lê → teste em `WeatherTests`)
| Efeito | Consumidor |
|---|---|
| Lama na parte sem calçamento da rota, neve na rota toda: viagem mais longa | `Mobility.TravelTicks/HouseholdTicks` (soma `StreetRoutes.SoftPm`) |
| Carroça atola (neve ≥ 15 cm, lamaçal): a carga volta aos braços | `InventionMarket` (carga da compra) |
| Chuva forte, neve no chão, chão congelado: não lavra, não constrói, não calça | `CropSystem`, `ConstructionSystem`, `PavingWorksSystem` |
| Chuva: não ceifa (o grão em pé debulha); luz curta: menos semeadura e ceifa por dia | `CropSystem` (Sow, Harvest) |
| Granizo derruba 15% do grão em pé | `CropSystem` |
| Fator de safra e seca saem do tempo (uma fonte só) | `WeatherSystem` → `ClimateYears` → colheita, flora |
| Mês mais frio que o normal (estação fria) e máximas > 24 °C: risco de morte (velho e bebê mais) | `MortalitySystem` (rótulo `frio`/`calor`) |
| Relento em noite de geada: morte por frio (`Death|frio`) | `WeatherSystem.Exposure` |
| Dia mais frio/quente que o normal: catarro/diarreia mais | `OutbreakSystem` (`DiseaseProfile.ColdSensitivityPerC`) |
| Seca: o riacho e a fonte minguam | `NaturalWaterSystem` |
| Neve cobre o pasto (5 cm: metade; 15 cm: um quarto) | `FaunaLifecycleSystem` |
| Mau tempo: conversar fora pesa menos; compra que pode esperar espera | cérebro (lazer), `NeedsRestocking` |
| Direção do vento: o ar de leste gela o inverno, o de sudoeste o amansa (toda leitura de temperatura acima) | `WeatherGenerator` (advecção) |
| Vegetação nua (inverno): a mesma bocada sustenta metade o herbívoro (come mais vezes, gasta mais o pasto) | `FaunaLifecycleSystem.Graze` |

## Contrato (só acréscimo; ausente com o tempo desligado)
`climate.weather` (escopo e cada região da camada `Climate`): `precipitation` (none/rain/snow), `intensity` (none, light < 2 mm,
moderate < 8 mm, heavy), `precipitationMm`, `storm`, `hail`, `frost`, `drought`, `heatWave`, `coldWave` (3 dias seguidos), `windMs`,
`temperatureNowC` (hora atual), `feelsLikeC`, `minC`, `maxC`, `daylightHours`, `sunriseHour`, `sunsetHour` (hora solar), `ground`
(dry/wet/mud/heavy-mud/snow/frozen), `snowCm`, `groundWetPm`, `outdoorLostDays` (no ano). AD-189: `windFromDeg`, `foliage{fraction,
phase, turning}`, `hourlyC[]`/`hourlyFeelsLikeC[]` (a hora do dia pela curva do motor; o delta não reenvia o clima só pela hora).
`climate.drought` passa a ser a seca do tempo. Células de rua (`roads`): `ground` (cascalho e pedra nunca ficam em lama, só molhadas), `snowCm`, `wetPm`.

## Fontes e confiança
- Médias mensais: CET 1961–1990 (Parker, Legg & Folland 1992), alta. Amplitude, dias de chuva ≥ 1 mm, chuva mensal: Oxford,
  normais do Met Office 1981–2010, média (de memória, conferir). Medieval ±0,5 °C (Lamb 1982), média.
- Persistência da chuva (0,65) e da temperatura (0,8): ordem de grandeza de geradores de tempo (Richardson 1981), média.
- Neve ≤ 1 °C, degelo 4 mm/°C-dia (Hock 2003: 3–6), média. Jornada da luz: do nascer ao pôr (Statute of Artificers 1563), alta.
- Safra ruim com primavera/verão chuvosos (Hoskins 1964; 1315–17), média. Diarreia +7%/°C (Carlton et al. 2016), média.
- Rosa dos ventos do sul da Inglaterra (Met Office, de memória, conferir), média; advecção pelos tipos de tempo de Lamb (1972), média
  na forma, amplitudes escolhidas. Brotação por grau-dia (Cannell & Smith 1983) e senescência por luz e frio (Delpierre et al. 2009),
  média na forma; limiares escolhidos para as datas do carvalho inglês (Sparks & Carey 1995, conferir). Pasto nu = metade: escolhido.
- **Escolhidos** (sem fonte firme): faixas e passo da lama (1,05 / 1,2 / 1,35), secagem do chão, unidade de altitude = 50 m, catarro +5%/°C, risco do frio
  (3%/°C para velhos e bebês, 1% outros), do calor (3%/°C acima de 24 °C), do relento (0,1%/°C-noite, ×3 vulneráveis),
  sensação, pasto sob neve, adiamento da compra, regime do ano.

**Cenário JSON (AD-190)**: o bloco `Climate` lê também `MeanMilli`, `StdDevMilli`, `Persistence` e `SharedShare` (ausentes = faixa uniforme
antiga). Os mundos do menu declaram os da vila de código (1000 ± 200, persistência 0,3, parte comum 0,7); antes a faixa 500–1500 virava
DP 289 sem persistência (5,6% dos anos < 600 e 3,3% no piso, contra 2,1%) e a cidade média e a vila pequena morriam na quebra.

## Interruptor e diagnóstico
`LW_WEATHER=off` (relatórios pareados) volta ao sorteio anual e ao degrau de trimestre. `weather-diag calib <anos>` e
`weather-diag world <seed>` (Workers) imprimem índice da safra e o ano por mês contra a fonte.

## Não entregue (dívida, 08d)
Lenha, roupa e fogo (a casa é o único abrigo; madeira só existe como insumo de obra); horário da rotina e do sono pela luz (a rotina
é tabela fixa por hora); nível d'água de rio e poço (a chuva não enche: só a seca reduz a fonte); geada sobre a lavoura no estágio
(só a geada tardia no índice do ano); continentalidade e distância da água na amplitude.
