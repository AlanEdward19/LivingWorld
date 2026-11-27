extends RefCounted
## #240: o registro único de popup aberto. Toda janela central (conversa, mochila, metas, ajuda, roda de ações, pausa, guia…) entra
## aqui ao abrir e sai ao fechar: UMA chamada, `modal.watch(controle)` (segue o `visible` do controle) ou `modal.set_open(chave, on)`.
## Com algum aberto (`active`): a mira, o anel do alvo, a dica do E e os números flutuantes somem, E/clique/golpe/pulo não disparam,
## o que `cover(...)` registrou (a dica de teclas) fica escondido, o popup sobe acima do HUD e o app pede o hold ao motor (popup_hold.gd).

signal changed(active: bool)

const POPUP_Z := 40 ## acima do HUD (mira, dica) que mora no mesmo CanvasLayer

var active := false
var _open: Dictionary = {} ## chave -> true
var _covered: Array = [] ## controles escondidos enquanto houver popup (a dica de teclas do HUD)


## O controle é um popup: aberto quando visível na árvore. Uma chamada; o resto (z, fechar ao sair da árvore) é daqui.
func watch(control: Control) -> void:
	control.z_index = maxi(control.z_index, POPUP_Z)
	control.visibility_changed.connect(func(): set_open(control, is_instance_valid(control) and control.is_visible_in_tree()))
	control.tree_exiting.connect(func(): set_open(control, false))
	if control.is_visible_in_tree():
		set_open(control, true)


## Controles que somem enquanto houver popup (voltam ao fechar o último).
func cover(control: Control) -> void:
	_covered.append(control)
	if active and is_instance_valid(control):
		control.visible = false


func set_open(key, on: bool) -> void:
	var was := not _open.is_empty()
	if on:
		_open[key] = true
	else:
		_open.erase(key)
	var now := not _open.is_empty()
	if now == was:
		return
	active = now
	for covered in _covered:
		if is_instance_valid(covered):
			covered.visible = not now
	changed.emit(now)


func open(key) -> void:
	set_open(key, true)


func close(key) -> void:
	set_open(key, false)


func is_open(key) -> bool:
	return _open.has(key)


func count() -> int:
	return _open.size()
