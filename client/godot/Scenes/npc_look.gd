extends RefCounted
## Aparência derivada do estado (ADR-0047 etapa 3): dos sinais visíveis que o servidor projeta (`observable`) para um "spec"
## de corpo que o NpcActor aplica. Função pura e testável: nada aqui decide comportamento nem inventa estado; o que o motor
## não manda (rosto, roupa exata) é estilização determinística por id de profissão/recurso.

const BABY_AGE := 3
const CHILD_AGE := 13
const ADULT_HEIGHT_CM := 170.0
const MOURNING_CLOTH := Color("2e2b30")
const SICK_SKIN := Color(0.86, 0.95, 0.80) ## multiplica a pele: palidez esverdeada de quem está doente
## Paleta de roupa por ofício quando o catálogo do cliente não tem a profissão (cor estável pelo id).
const CLOTH_PALETTE: Array[Color] = [Color("8f6b3a"), Color("6a8f4a"), Color("9a4a3a"), Color("5a5a66"), Color("4a6a9a"),
	Color("8a5a8a"), Color("3a8fa0"), Color("a08f3a"), Color("7a4a2a"), Color("4f7a6a")]
## Palavras do nome do ofício (catálogo do período) -> chapéu e ferramenta. Sem casar, cai no id.
const TRADE_WORDS := [
	[["lavr", "agric", "camp", "farm", "plant", "colhe", "pastor", "shepherd"], "straw", "hoe"],
	[["ferr", "smith", "forj", "metal"], "cap", "hammer"],
	[["miner", "pedr", "quarr", "mason"], "cap", "pick"],
	[["lenh", "wood", "carpin", "marce"], "cap", "axe"],
	[["pesc", "fish"], "straw", "rod"],
	[["padei", "baker", "cozin", "cook", "moleir", "miller"], "cloth", "peel"],
	[["guard", "soldad", "vigia", "milic"], "helmet", "spear"],
	[["sacerd", "priest", "monge", "monk", "curand", "heal", "médic", "medic"], "hood", "staff"],
	[["merca", "comerc", "vend", "trader", "merchant", "lojist"], "cloth", "scale"],
	[["tecel", "weav", "costur", "tailor"], "cloth", "needle"],
]
## #254: estilo do período (o cliente só estiliza a roupa; o corpo, a idade, o sexo e a cor vêm do motor). "medieval": túnica, bota;
## "modern": camiseta, calça, vestido, tênis. Quem decide o período é o mundo aberto (`era_for`); sem dado, medieval (os presets atuais).
static var era := "medieval"
const JEANS: Array[Color] = [Color("3d5a80"), Color("2f4468"), Color("4a4a52"), Color("6b5b3e"), Color("22262e")]
const HOSE: Array[Color] = [Color("5a4a3a"), Color("4a4a44"), Color("6a5a42"), Color("3f3a33")]
const SNEAKERS: Array[Color] = [Color("e8e8e8"), Color("2a2a30"), Color("c0392b"), Color("3d6fb4")]
const BOOT := Color("4a3524")
const EYE_COLORS: Array[Color] = [Color("3b2a1e"), Color("1c1410"), Color("6b4a2b"), Color("4f7fa8"), Color("5f8a62")]
const RESOURCE_PALETTE: Array[Color] = [Color("c8a25a"), Color("8a6a43"), Color("6f8f4a"), Color("9a9a9a"), Color("b0603a"),
	Color("d8cfb8"), Color("5a7a9a")]


## Estágio de vida para o corpo: bebê, criança, adulto, idoso (o motor dá `lifeStage`; bebê é a idade < 3).
static func body_stage(obs: Dictionary) -> String:
	var age := int(obs.get("age", 30))
	if age < BABY_AGE:
		return "baby"
	var stage := String(obs.get("stage", ""))
	if stage == "Child" or (stage == "" and age < CHILD_AGE):
		return "child"
	if stage == "Elder":
		return "elder"
	return "adult"


## Escala do corpo pela altura real (cm) do motor; sem altura, pelo estágio.
static func body_scale(obs: Dictionary) -> float:
	var height := float(obs.get("height_cm", 0))
	if height > 0.0:
		return clampf(height / ADULT_HEIGHT_CM, 0.32, 1.15)
	return {"baby": 0.36, "child": 0.68, "adult": 1.0, "elder": 0.95}.get(body_stage(obs), 1.0)


static func trade_of(obs: Dictionary) -> Array:
	var name := String(obs.get("profession_name", "")).to_lower()
	for entry in TRADE_WORDS:
		for word in entry[0]:
			if word in name:
				return [entry[1], entry[2]]
	return []


## Spec completo: {stage, scale, female, cloth, hat, tool, mourning, ill, sick_pose, belly, expression, posture, sack, held}.
static func spec_of(obs: Dictionary, catalog = null) -> Dictionary:
	if obs.is_empty():
		return {}
	var stage := body_stage(obs)
	var profession := int(obs.get("profession_id", -1))
	var by_catalog: Dictionary = {}
	if catalog != null and profession >= 0 and catalog.data.get("models", {}).get("professions", {}).has(str(profession)):
		by_catalog = catalog.profession_model(profession)
	var trade := trade_of(obs)
	var cloth: Color = Color(String(by_catalog.shirt)) if by_catalog.has("shirt") else (
		CLOTH_PALETTE[posmod(profession, CLOTH_PALETTE.size())] if profession >= 0 else Color("7a8b99"))
	var hat := String(by_catalog.get("hat", trade[0] if not trade.is_empty() else "none"))
	var tool: String = String(trade[1]) if not trade.is_empty() else ["hammer", "hoe", "axe", "pick"][posmod(profession, 4)]
	if stage == "baby" or stage == "child":
		cloth = cloth.lightened(0.35)
		hat = "none"
		tool = ""
	var mourning := bool(obs.get("mourning", false))
	if mourning and stage != "baby":
		cloth = MOURNING_CLOTH
	var bedridden := bool(obs.get("bedridden", false))
	var expression := String(obs.get("dominant", ""))
	if mourning and expression == "":
		expression = "grief"
	return {
		"stage": stage, "scale": body_scale(obs), "female": String(obs.get("sex", "")) == "Female",
		"cloth": cloth, "hat": hat, "tool": tool, "mourning": mourning, "ill": bool(obs.get("ill", false)),
		"sick_pose": "bed" if bedridden else ("slump" if bool(obs.get("symptomatic", false)) else ""),
		"belly": clampf(float(obs.get("pregnancy", 0.0)), 0.0, 1.0), "expression": expression,
		"posture": posture_of(expression, bool(obs.get("symptomatic", false))), "era": era,
		"sack": sack_of(obs.get("carried", [])), "held": obs.get("held", {}),
	}


## Postura pela emoção dominante: cabeça baixa (luto, vergonha, solidão), encolhido (medo), duro (raiva), leve (alegria).
static func posture_of(expression: String, symptomatic: bool) -> String:
	if symptomatic:
		return "slump"
	return {"grief": "down", "shame": "down", "loneliness": "down", "fear": "hunch", "anger": "stiff", "joy": "light"}.get(expression, "")


## Carga pessoal visível: tamanho pela quantidade, cor pelo recurso (água = jarro azul).
static func sack_of(carried: Variant) -> Dictionary:
	if not carried is Array or carried.is_empty():
		return {}
	var total := 0
	var main: Dictionary = carried[0]
	for load in carried:
		total += int(load.get("quantity", 0))
		if int(load.get("quantity", 0)) > int(main.get("quantity", 0)):
			main = load
	if total <= 0:
		return {}
	var name := String(main.get("name", "")).to_lower()
	var water := "água" in name or "agua" in name or "water" in name
	return {"size": clampf(0.6 + 0.12 * log(1.0 + total), 0.6, 1.4), "water": water,
		"color": Color("4a86b8") if water else RESOURCE_PALETTE[posmod(int(main.get("resource_id", 0)), RESOURCE_PALETTE.size())],
		"total": total, "name": String(main.get("name", ""))}


## Texto curto do corpo para a dica: "criança · grávida · doente · de luto · carrega 3 trigo".
static func hint_of(obs: Dictionary) -> String:
	if obs.is_empty():
		return ""
	var parts: Array[String] = []
	var stage := body_stage(obs)
	parts.append({"baby": "bebê", "child": "criança", "adult": "", "elder": "idoso(a)"}.get(stage, ""))
	if String(obs.get("profession_name", "")) != "" and stage != "baby" and stage != "child":
		parts.append(String(obs.profession_name))
	if float(obs.get("pregnancy", 0.0)) > 0.0:
		parts.append("grávida")
	if bool(obs.get("bedridden", false)):
		parts.append("de cama")
	elif bool(obs.get("ill", false)):
		parts.append("doente")
	if bool(obs.get("mourning", false)):
		parts.append("de luto")
	var feeling: String = {"joy": "alegre", "fear": "com medo", "anger": "com raiva", "shame": "envergonhado(a)",
		"loneliness": "sozinho(a)", "grief": "triste"}.get(String(obs.get("dominant", "")), "")
	if feeling != "" and not (feeling == "triste" and bool(obs.get("mourning", false))):
		parts.append(feeling)
	var sack := sack_of(obs.get("carried", []))
	if not sack.is_empty():
		parts.append("carrega %d %s" % [int(sack.total), String(sack.name) if String(sack.name) != "" else "unidades"])
	return " · ".join(parts.filter(func(p): return p != ""))


static func set_era(value: String) -> void:
	era = "modern" if value == "modern" else "medieval"


## Período do mundo -> estilo de roupa: o que o período declara em `Presentation.Wardrobe` (catálogo do período, #254);
## sem declaração, medieval (os presets de aldeia). Nada adivinhado pelo nome do mundo.
static func era_for(wardrobe: String) -> String:
	return "modern" if wardrobe == "modern" else "medieval"


## Variação estável por id e sal (mesma pessoa, mesmo traço; nada aleatório): inteiro não negativo.
static func pick(id: int, salt: int) -> int:
	return posmod((id * 73856093) ^ (salt * 19349663 + 83492791), 1000003)


## Estilo gerado pela semente (id): cabelo, barba, roupa e cores que o motor não diz. Nunca contradiz o corpo: sexo, idade e cor de pele
## e cabelo vêm do motor; aqui só o corte, a barba e o corte da roupa. Devolve {hair, beard (0 nenhuma, 1 curta, 2 cheia), outfit, skirt,
## belt, long_sleeves, pants, shoe, boot, eyes}.
static func style_of(id: int, stage: String, female: bool, the_era: String = "") -> Dictionary:
	var e := the_era if the_era != "" else era
	var hair := "short"
	var beard := 0
	match stage:
		"baby":
			hair = "tuft"
		"child":
			hair = ["bob", "ponytail", "long"][pick(id, 1) % 3] if female else ["short", "buzz", "short"][pick(id, 1) % 3]
		"elder":
			hair = ["bun", "bob", "bun"][pick(id, 1) % 3] if female else ["fringe", "buzz", "fringe", "short"][pick(id, 1) % 4]
			beard = (1 + pick(id, 2) % 2) if (not female and pick(id, 3) % 10 < 6) else 0
		_:
			hair = ["long", "ponytail", "bun", "bob", "long"][pick(id, 1) % 5] if female else ["short", "short", "buzz", "curly", "short"][pick(id, 1) % 5]
			beard = (1 + pick(id, 2) % 2) if (not female and pick(id, 3) % 10 < 3) else 0
	var modern := e == "modern"
	var outfit := "tunic"
	var skirt := false
	var belt := true
	var long_sleeves := true
	if modern:
		outfit = "dress" if (female and stage != "baby" and pick(id, 4) % 3 == 0) else ("shirt" if (not female and pick(id, 4) % 2 == 0 and stage == "adult") else "tee")
		skirt = outfit == "dress"
		belt = outfit == "shirt"
		long_sleeves = outfit == "shirt"
	else:
		skirt = stage != "baby" # túnica até o joelho
	var pants: Color = JEANS[pick(id, 5) % JEANS.size()] if modern else HOSE[pick(id, 5) % HOSE.size()]
	if stage == "child" or stage == "baby":
		pants = pants.lightened(0.25)
	return {"hair": hair, "beard": beard, "outfit": outfit, "skirt": skirt, "belt": belt, "long_sleeves": long_sleeves, "pants": pants,
		"shoe": SNEAKERS[pick(id, 6) % SNEAKERS.size()] if modern else BOOT, "boot": not modern, "eyes": EYE_COLORS[pick(id, 7) % EYE_COLORS.size()]}
