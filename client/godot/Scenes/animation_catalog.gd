extends RefCounted
## ActionType (id numérico do servidor) -> pose procedural do NPC. O id vem do servidor; o RÓTULO
## de cada ação vem do catálogo `/visual/catalog/actions` (ver ActionLabels), nunca daqui.

const NEUTRAL_POSE := "idle"

## Os 8 valores de LivingWorld.Domain.Behavior.ActionType.
const POSE_BY_ACTION := {
	0: "eat", 1: "sleep", 2: "work", 3: "socialize", 4: "walk", 5: "idle", 6: "buy", 7: "power",
}
const POSES: Array[String] = ["idle", "walk", "sleep", "work", "eat", "socialize", "buy", "power"]


static func pose_for(action: int) -> String:
	return POSE_BY_ACTION.get(action, NEUTRAL_POSE)


static func is_mapped(action: int) -> bool:
	return POSE_BY_ACTION.has(action)
