extends RefCounted
## Edição de JSON em texto, sem passar pelo parser do Godot. O parser devolve todo número como float e o
## stringify reescreve `1` como `1.0`, que o System.Text.Json do servidor rejeita em campos inteiros.
## Por isso o cenário de um período viaja como texto cru: só se extrai e se troca um membro do topo.


## Intervalo [início, fim) do VALOR do membro `key` no objeto raiz de `text`, ou [-1, -1] se não existir.
static func member_span(text: String, key: String) -> Vector2i:
	var depth := 0
	var i := 0
	var size := text.length()
	while i < size:
		var c := text[i]
		if c == '"':
			var end := _string_end(text, i)
			if depth == 1 and text.substr(i + 1, end - i - 1) == key:
				var colon := _skip_space(text, end + 1)
				if colon < size and text[colon] == ":":
					var start := _skip_space(text, colon + 1)
					return Vector2i(start, _value_end(text, start))
			i = end + 1
			continue
		if c == "{" or c == "[":
			depth += 1
		elif c == "}" or c == "]":
			depth -= 1
		i += 1
	return Vector2i(-1, -1)


static func member(text: String, key: String) -> String:
	var span := member_span(text, key)
	return "" if span.x < 0 else text.substr(span.x, span.y - span.x)


## Troca o valor do membro do topo `key` por `raw_value` (já em JSON); texto igual se o membro não existe.
static func replace_member(text: String, key: String, raw_value: String) -> String:
	var span := member_span(text, key)
	if span.x < 0:
		return text
	return text.substr(0, span.x) + raw_value + text.substr(span.y)


## Troca o membro do topo `key` ou, se não existe, o acrescenta antes do `}` final do objeto raiz.
static func set_member(text: String, key: String, raw_value: String) -> String:
	if member_span(text, key).x >= 0:
		return replace_member(text, key, raw_value)
	var close := text.rfind("}")
	if close < 0:
		return text
	var before := text.substr(0, close).rstrip(" \t\n\r")
	return before + ',"%s":%s' % [key, raw_value] + text.substr(close)


static func _string_end(text: String, open_quote: int) -> int:
	var i := open_quote + 1
	while i < text.length():
		if text[i] == "\\":
			i += 2
			continue
		if text[i] == '"':
			return i
		i += 1
	return text.length() - 1


static func _skip_space(text: String, from: int) -> int:
	var i := from
	while i < text.length() and text[i] in [" ", "\t", "\n", "\r"]:
		i += 1
	return i


static func _value_end(text: String, start: int) -> int:
	if start >= text.length():
		return start
	var first := text[start]
	if first == '"':
		return _string_end(text, start) + 1
	if first == "{" or first == "[":
		var depth := 0
		var i := start
		while i < text.length():
			var c := text[i]
			if c == '"':
				i = _string_end(text, i)
			elif c == "{" or c == "[":
				depth += 1
			elif c == "}" or c == "]":
				depth -= 1
				if depth == 0:
					return i + 1
			i += 1
		return text.length()
	var j := start
	while j < text.length() and not (text[j] in [",", "}", "]", " ", "\n", "\r", "\t"]):
		j += 1
	return j
