extends RefCounted
## ActionType (id numérico do servidor) -> pose procedural do NPC. O id vem do servidor; o RÓTULO
## de cada ação vem do catálogo `/visual/catalog/actions` (ver ActionLabels), nunca daqui.

const NEUTRAL_POSE := "idle"

## Os 8 valores de LivingWorld.Domain.Behavior.ActionType.
const POSE_BY_ACTION := {
	0: "eat", 1: "sleep", 2: "work", 3: "socialize", 4: "walk", 5: "idle", 6: "buy", 7: "power",
}
const POSES: Array[String] = ["idle", "walk", "sleep", "work", "eat", "socialize", "buy", "power"]


## Pose do NPC: a que o objeto em uso declara (ponto de uso, ADR-0037) quando o cliente a conhece; senão a da ação.
static func pose_for(action: int, use_pose: String = "") -> String:
	if use_pose != "" and use_pose in POSES:
		return use_pose
	return POSE_BY_ACTION.get(action, NEUTRAL_POSE)


static func is_mapped(action: int) -> bool:
	return POSE_BY_ACTION.has(action)
