extends SceneTree
## Runner headless. Uso: godot --headless --path client/godot --script res://tests/run.gd -- [--filter arquivo::teste]
## Cada tests/test_*.gd define métodos test_* (podem usar await). Sai com 1 se algo falhar.


func _init() -> void:
	await process_frame # a árvore só existe de verdade após o primeiro frame
	var filter := ""
	var args := OS.get_cmdline_user_args()
	for i in args.size():
		if args[i] == "--filter" and i + 1 < args.size():
			filter = args[i + 1]
	var file_filter := filter.get_slice("::", 0)
	var test_filter := filter.get_slice("::", 1) if "::" in filter else ""

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
			await instance.call(method.name)
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
