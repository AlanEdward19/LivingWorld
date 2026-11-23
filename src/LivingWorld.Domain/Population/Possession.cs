namespace LivingWorld.Domain.Population;

/// <summary>Como o controlador conduz o corpo (AD-197e, decisão do dono de 2026-11-23). A mesma base serve para o jogador que
/// encarna um NPC e, no futuro, para a possessão dentro do jogo (inclusive de um personagem de jogador).</summary>
public enum PossessionMode
{
    /// <summary>Posse comum (sem admin): a pessoa segue com vontade própria e recusa ordens específicas que ferem muito o que ela
    /// valoriza (ex.: ferir alguém de quem ela gosta muito). Ordem recusada não muda o mundo.</summary>
    Common = 0,

    /// <summary>Posse plena (admin): a pessoa vira personagem do jogador e não recusa nada. Ela continua existindo presa
    /// (<see cref="CaptiveMind"/>), vendo o que o corpo faz e reagindo pelos próprios valores.</summary>
    Full = 1,
}

/// <summary>A pessoa presa no próprio corpo durante a posse plena (AD-197e). Estado canônico: <see cref="Objections"/> conta as
/// ordens que ela não daria, <see cref="Distress"/> (0..100) é o quanto o que o corpo fez a feriu, pelo vínculo com quem foi
/// atingido. Consumidor: ao ser solta, ela vive a posse (lembrança e emoção na psique, na medida do sofrimento), que molda a rotina
/// depois (AD-138). <see cref="LastObjectionTarget"/> é a pessoa atingida na última ordem contra a vontade dela (−1 = nenhuma).</summary>
public sealed record CaptiveMind(long SinceTick, int Objections = 0, int Distress = 0, long LastObjectionTarget = -1, long LastObjectionTick = -1);
