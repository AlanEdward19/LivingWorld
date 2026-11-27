using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Simulation.Cities;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Players;

/// <summary>#227/#226: o corpo que o jogador passa a conduzir não nasce no lugar errado. Antes a origem da planta podia ser célula de parede e o corpo
/// "em casa" ficava preso nela; com a parede em aresta (#226) toda célula da planta é piso, então só resta o posto de trabalho no campo: o corpo
/// guardado num prédio com a célula local fora da planta está na rua, não dentro. Só o corpo conduzido; o mundo de quem não é jogador não muda.</summary>
internal static class PlayerSettle
{
    /// <summary>Põe o corpo na rua se o motor o guardou num posto de trabalho do campo; devolve se mexeu.</summary>
    public static bool OutOfTheWall(WorldState world, TickContext ctx, Npc npc)
    {
        if (npc.Interior is null) return false;
        return LeaveTheField(world, ctx, npc);
    }

    /// <summary>#237: o posto de trabalho no campo guarda o prédio com a célula local fora da planta (a pose é uma célula do chão aberto).
    /// Para conduzir, o corpo é de quem está na rua: vai para a célula da pose (o campo) e sai do prédio, em vez de o app entrar numa
    /// casa em que ele não está. Dentro da planta (ou nos andares de cima) nada muda.</summary>
    private static bool LeaveTheField(WorldState world, TickContext ctx, Npc npc)
    {
        if (PlayerGround.InsideBuilding(world, npc) is not null) return false;
        var pose = PlayerGround.PoseOf(world, npc);
        npc.ExitBuilding();
        if (npc.CurrentLocation != pose) npc.MoveTo(pose, ctx.CurrentTick);
        if (npc.Fine is not null) npc.SetFine(null); // a posição contínua partiria do posto de trabalho de novo
        return true;
    }
}
