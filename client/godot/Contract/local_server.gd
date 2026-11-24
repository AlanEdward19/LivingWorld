extends RefCounted
## O jogo aberto sem servidor: acha a API que o lançador (`Jogar.command`/`scripts/play.sh`) já ligou, ou liga ele mesmo
## o binário JÁ COMPILADO do projeto (caminho fixo, relativo a este projeto, rodado pelo `dotnet` de um lugar fixo) e o
## desliga ao sair. Nunca compila, nunca baixa nada e só encerra o processo que ele mesmo criou.

const API_DLL := "../../src/LivingWorld.Api/bin/Release/net10.0/LivingWorld.Api.dll" ## relativo a res://
const DOTNET_PATHS: Array[String] = ["/usr/local/share/dotnet/dotnet", "/opt/homebrew/bin/dotnet", "/usr/local/bin/dotnet", "/usr/bin/dotnet"]
const FIRST_PORT := 5214
const LAST_PORT := 5240

var pid := -1 ## processo que ESTE cliente ligou (-1: nenhum)
var port := 0


## Pasta dos mundos, a mesma do lançador: ~/Library/Application Support/LivingWorld no macOS (LW_DATA_DIR troca).
static func data_dir() -> String:
	if OS.get_environment("LW_DATA_DIR") != "":
		return OS.get_environment("LW_DATA_DIR")
	if OS.get_name() == "macOS":
		return OS.get_environment("HOME").path_join("Library/Application Support/LivingWorld")
	var xdg := OS.get_environment("XDG_DATA_HOME")
	return (xdg if xdg != "" else OS.get_environment("HOME").path_join(".local/share")).path_join("LivingWorld")


static func dll_path() -> String:
	return ProjectSettings.globalize_path("res://").path_join(API_DLL).simplify_path()


static func dotnet_path() -> String:
	var home_dotnet := OS.get_environment("HOME").path_join(".dotnet/dotnet")
	for candidate in DOTNET_PATHS + [home_dotnet]:
		if FileAccess.file_exists(candidate):
			return candidate
	return ""


## Só liga sozinho num jogo de verdade: com janela, sem endereço fixado por fora e com o binário já compilado.
static func can_start() -> bool:
	return DisplayServer.get_name() != "headless" and OS.get_environment("LW_API_URL") == "" \
		and FileAccess.file_exists(dll_path()) and dotnet_path() != ""


## Estado escrito pelo lançador (`api.state`): {port, token}; vazio se não houver.
static func launcher_state(dir: String = data_dir()) -> Dictionary:
	var file := FileAccess.open(dir.path_join("api.state"), FileAccess.READ)
	if file == null:
		return {}
	var values := {}
	for line in file.get_as_text().split("\n", false):
		if "=" in line:
			values[line.get_slice("=", 0)] = line.get_slice("=", 1)
	if not values.has("PORT"):
		return {}
	return {"port": int(values.PORT), "token": String(values.get("TOKEN", ""))}


## Aponta este processo (e todo transporte criado depois) para a API: endereço e token de admin da sessão.
static func use(p_port: int, token: String) -> void:
	OS.set_environment("LW_API_URL", "http://localhost:%d" % p_port)
	if token != "":
		OS.set_environment("LW_ADMIN_TOKEN", token)


static func port_free(candidate: int) -> bool:
	var server := TCPServer.new()
	var free := server.listen(candidate, "127.0.0.1") == OK
	server.stop()
	return free


## Liga a API em segundo plano numa porta livre. O token de admin é desta sessão: o jogador controla o tempo sem saber dele.
func start() -> bool:
	if pid > 0 or not can_start():
		return false
	port = 0
	for candidate in range(FIRST_PORT, LAST_PORT + 1):
		if port_free(candidate):
			port = candidate
			break
	if port == 0:
		return false
	DirAccess.make_dir_recursive_absolute(data_dir())
	var token := Crypto.new().generate_random_bytes(16).hex_encode()
	OS.set_environment("ADMIN_TOKEN", token)
	OS.set_environment("TICK_LOOP_ENABLED", "true")
	OS.set_environment("ConnectionStrings__World", "Data Source=" + data_dir().path_join("mundo.db"))
	pid = OS.create_process(dotnet_path(), [dll_path(), "--urls", "http://localhost:%d" % port])
	if pid <= 0:
		pid = -1
		return false
	use(port, token)
	return true


func running() -> bool:
	return pid > 0 and OS.is_process_running(pid)


## Ao sair do jogo: encerra só o processo que este cliente criou (SIGTERM: a API fecha o banco direito).
func stop() -> void:
	if running():
		if OS.get_name() != "Windows" and FileAccess.file_exists("/bin/kill"):
			OS.execute("/bin/kill", ["-TERM", str(pid)])
		else:
			OS.kill(pid)
	pid = -1
