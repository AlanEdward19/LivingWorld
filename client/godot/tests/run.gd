extends SceneTree
## Runner headless. Uso: godot --headless --path client/godot --script res://tests/run.gd -- [--filter arquivo::teste]
## Cada tests/test_*.gd define métodos test_* (podem usar await). Sai com 1 se algo falhar.


const TEST_TIMEOUT := 60.0 ## segundos por teste


## Erro de script (ex.: chamada em nulo) não derruba o teste sozinho: o motor só imprime e segue. Este Logger o captura
## para o runner marcar o teste como FALHA em vez de `ok` (antes só o grep do client-test.sh barrava).
class ScriptErrorLogger extends Logger:
	var messages: Array[String] = []

	func _log_error(_function: String, _file: String, _line: int, code: String, rationale: String, _editor_notify: bool, error_type: int, _script_backtraces: Array) -> void:
		if error_type == ERROR_TYPE_SCRIPT:
			messages.append(rationale if rationale != "" else code)


func _init() -> void:
	await process_frame # a árvore só existe de verdade após o primeiro frame
	var filter := ""
	var args := OS.get_cmdline_user_args()
	for i in args.size():
		if args[i] == "--filter" and i + 1 < args.size():
			filter = args[i + 1]
	var file_filter := filter.get_slice("::", 0)
	var test_filter := filter.get_slice("::", 1) if "::" in filter else ""

	var script_errors := ScriptErrorLogger.new()
	OS.add_logger(script_errors)
	var passed := 0
	var failed := 0
	var ran := 0
	var files: Array[String] = []
	for file in DirAccess.get_files_at("res://tests"):
		if file.begins_with("test_") and file.ends_with(".gd") and file != "test_base.gd":
			files.append(file)
	files.sort()
	for file in files:
		var name := file.trim_suffix(".gd")
		if file_filter != "" and name != file_filter:
			continue
		var script: GDScript = load("res://tests/" + file)
		for method in script.get_script_method_list():
			if not String(method.name).begins_with("test_"):
				continue
			if test_filter != "" and method.name != test_filter:
				continue
			var instance = script.new()
			instance.tree = self
			ran += 1
			var errors_before := script_errors.messages.size()
			# Vigia: teste pendurado (await que nunca volta) derruba o runner com falha e nome, em vez de travar o gate.
			var finished := [false]
			var running := "%s::%s" % [name, method.name]
			create_timer(TEST_TIMEOUT).timeout.connect(func():
				if not finished[0]:
					print("  FAIL %s (sem terminar em %d s)" % [running, int(TEST_TIMEOUT)])
					print("client-test: teste pendurado")
					quit(1))
			await instance.call(method.name)
			finished[0] = true
			for message in script_errors.messages.slice(errors_before):
				instance.failures.append("SCRIPT ERROR: " + message)
			if instance.failures.is_empty():
				passed += 1
				print("  ok   %s::%s" % [name, method.name])
			else:
				failed += 1
				print("  FAIL %s::%s" % [name, method.name])
				for message in instance.failures:
					print("       - ", message)
	print("client-test: %d passed, %d failed" % [passed, failed])
	if ran == 0:
		print("client-test: nenhum teste casou com o filtro '%s'" % filter)
	quit(1 if (failed > 0 or ran == 0) else 0)
