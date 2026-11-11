namespace LivingWorld.Domain.Population.Body;

/// <summary>Ponto de uma curva por idade (anos, valor).</summary>
public sealed record AgePoint(double Age, double Value);

/// <summary>Curva por idade, dado de cenário: interpolação linear entre pontos em ordem crescente de idade; antes do
/// primeiro ponto vale o primeiro valor, depois do último vale o último. Sem pontos, vale 0.</summary>
public sealed record AgeCurve(IReadOnlyList<AgePoint> Points)
{
    public static AgeCurve Of(params (double Age, double Value)[] points) =>
        new(points.Select(p => new AgePoint(p.Age, p.Value)).ToArray());

    public double At(double age)
    {
        var points = Points;
        if (points.Count == 0) return 0.0;
        if (age <= points[0].Age) return points[0].Value;
        for (int i = 1; i < points.Count; i++)
        {
            var hi = points[i];
            if (age > hi.Age) continue;
            var lo = points[i - 1];
            double span = hi.Age - lo.Age;
            return span <= 0 ? hi.Value : lo.Value + (hi.Value - lo.Value) * (age - lo.Age) / span;
        }

        return points[^1].Value;
    }

    /// <summary>Maior valor da curva (para normalizar fatores relativos).</summary>
    public double Max()
    {
        double max = 0;
        foreach (var p in Points)
            if (p.Value > max) max = p.Value;
        return max;
    }

    public bool IsAscendingInAge()
    {
        for (int i = 1; i < Points.Count; i++)
            if (Points[i].Age < Points[i - 1].Age) return false;
        return true;
    }
}
