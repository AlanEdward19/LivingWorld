namespace LivingWorld.Workers;

/// <summary>AD-191, só para os relatórios: <c>LW_AD191=off</c> desliga as regras da fatia (a lavoura que vale o grão na escolha de
/// emprego, a ida de água que não volta vazia e as demais chaves da AD-191) para o pareado antes × depois no mesmo binário.</summary>
public static class Ad191
{
    public static bool Off => Environment.GetEnvironmentVariable("LW_AD191") == "off";
}
