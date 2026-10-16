extends RefCounted
## Base mínima dos testes headless: `check`/`eq` registram falhas; o runner as relata.

var failures: Array[String] = []
var tree: SceneTree


func check(condition: bool, message: String = "check falhou") -> void:
	if not condition:
		failures.append(message)


func eq(actual: Variant, expected: Variant, message: String = "") -> void:
	if actual != expected:
		failures.append("%s esperado %s, veio %s" % [message, str(expected), str(actual)])
