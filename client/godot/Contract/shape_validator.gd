extends RefCounted
## Valida uma resposta contra a FORMA congelada em tests/contracts/shape.*.json:
## objeto -> propriedades obrigatórias; lista -> todo elemento tem a forma do primeiro;
## folha -> tipo JSON. Campo extra é aceito (compatibilidade para frente); campo ausente
## ou de tipo diferente é rejeitado. "null" e "[]" na gravação aceitam qualquer valor/lista.

const CONTRACTS_DIR := "../../tests/contracts/"


static func contracts_path(file: String) -> String:
	return ProjectSettings.globalize_path("res://").path_join(CONTRACTS_DIR + file).simplify_path()


static func load_shape(name: String) -> Variant:
	var path := contracts_path("shape.%s.json" % name)
	if not FileAccess.file_exists(path):
		return null
	return JSON.parse_string(FileAccess.get_file_as_string(path))


## Devolve a lista de divergências; vazia = a resposta cumpre o contrato.
static func validate(value: Variant, shape: Variant, path: String = "$") -> Array[String]:
	var errors: Array[String] = []
	_check(value, shape, path, errors)
	return errors


static func _check(value: Variant, shape: Variant, path: String, errors: Array[String]) -> void:
	if shape is Dictionary:
		if not (value is Dictionary):
			errors.append("%s: esperado objeto, veio %s" % [path, type_string(typeof(value))])
			return
		for key in shape:
			if not value.has(key):
				errors.append("%s.%s: campo ausente" % [path, key])
			else:
				_check(value[key], shape[key], "%s.%s" % [path, key], errors)
	elif shape is Array:
		if not (value is Array):
			errors.append("%s: esperado lista, veio %s" % [path, type_string(typeof(value))])
			return
		if shape.size() == 1:
			for i in value.size():
				_check(value[i], shape[0], "%s[%d]" % [path, i], errors)
	elif shape is String:
		_check_leaf(value, shape, path, errors)


static func _check_leaf(value: Variant, kind: String, path: String, errors: Array[String]) -> void:
	match kind:
		"string":
			if not (value is String):
				errors.append("%s: esperado string" % path)
		"number":
			if not (value is float or value is int):
				errors.append("%s: esperado número" % path)
		"boolean":
			if not (value is bool):
				errors.append("%s: esperado boolean" % path)
		"[]":
			if not (value is Array):
				errors.append("%s: esperado lista" % path)
		_:
			pass # "null" gravado: qualquer valor serve
