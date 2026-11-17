extends RefCounted
## Ilustração e frase de cada época/cenário no Novo mundo, desenhadas por código (sem arquivo de imagem): céu, morros
## e tantas casinhas quanto o lugar costuma ter. A API ainda não manda descrição nem era do período (lacuna no
## backlog 09): os cenários padrão ganham frase própria; os outros, uma frase montada do tamanho.

const LINES := {
	"vila-pequena": "Poucas famílias num vale tranquilo. Dá para conhecer cada pessoa pelo nome.",
	"cidade-media": "Uma cidade com ofícios, mercado e vizinhos de todo tipo.",
	"grande-metropole": "Centenas de vidas ao mesmo tempo, para quem gosta de observar multidões.",
}
const NAME_START: Array[String] = ["Al", "Bel", "Cor", "Dra", "El", "Fen", "Gar", "Hel", "Is", "Kor", "Lum", "Mor", "Nor", "Or", "Tar", "Val"]
const NAME_END: Array[String] = ["dor", "ia", "heim", "mar", "ria", "thal", "vik", "wen", "land", "gard", "mere", "ton"]
const SIZES: Array[Dictionary] = [
	{"name": "Vila pequena", "side": 12, "population": 40, "line": "Umas poucas famílias. Dá para conhecer todo mundo."},
	{"name": "Vila", "side": 20, "population": 150, "line": "Ofícios, vizinhos, conversa e mercado."},
	{"name": "Cidade", "side": 32, "population": 400, "line": "Muita gente e muita coisa acontecendo. O computador trabalha mais."},
]


static func random_name(rng: RandomNumberGenerator) -> String:
	return NAME_START[rng.randi() % NAME_START.size()] + NAME_END[rng.randi() % NAME_END.size()]


static func line_for(period: Dictionary) -> String:
	var id := String(period.get("periodId", ""))
	if LINES.has(id):
		return LINES[id]
	return "Um mundo de %d por %d terras, pronto para ganhar vida." % [int(period.get("width", 0)), int(period.get("height", 0))]


static func name_for(period: Dictionary) -> String:
	var source := String(period.get("source", ""))
	return source if source != "" else String(period.get("periodId", "Mundo")).replace("-", " ").capitalize()


## Rótulo humano para uma quantidade de pessoas (o número aparece ao lado).
static func crowd_label(population: int) -> String:
	if population <= 60:
		return "Vila pequena"
	if population <= 250:
		return "Vila"
	return "Cidade"


static func nearest_size(population: int) -> int:
	var best := 0
	for i in SIZES.size():
		if absi(int(SIZES[i].population) - population) < absi(int(SIZES[best].population) - population):
			best = i
	return best


## Desenho 160x90: céu em degradê, dois morros, chão e `houses` casinhas com telhado (mais casas = lugar maior).
static func picture(houses: int, seed_value: int) -> ImageTexture:
	var w := 160
	var h := 90
	var image := Image.create(w, h, false, Image.FORMAT_RGBA8)
	var rng := RandomNumberGenerator.new()
	rng.seed = seed_value
	for y in h:
		var sky := Color("f3b77a").lerp(Color("3a4f86"), 1.0 - float(y) / h)
		for x in w:
			image.set_pixel(x, y, sky)
	for hill in 2:
		var cx := rng.randi_range(20, 140)
		var top := rng.randi_range(36, 52)
		var color := Color("4e7a4a") if hill == 0 else Color("5f8f4e")
		for x in w:
			var crest := top + int(pow(abs(x - cx) / 40.0, 2) * 14)
			for y in range(clampi(crest, 0, h), h):
				image.set_pixel(x, y, color)
	image.fill_rect(Rect2i(0, 70, w, 20), Color("6f9b52"))
	var count := clampi(houses, 1, 14)
	for i in count:
		var x := 8 + int(float(i) / count * 140) + rng.randi_range(-3, 3)
		var y := 58 + (i % 2) * 8 + rng.randi_range(0, 3)
		image.fill_rect(Rect2i(x, y, 10, 8), Color("e8dcc4"))
		image.fill_rect(Rect2i(x + 4, y + 4, 2, 4), Color("6b4a32"))
		for r in 5:
			image.fill_rect(Rect2i(x - 1 + r, y - 1 - r, 12 - r * 2, 1), Color("a4553a"))
	return ImageTexture.create_from_image(image)
