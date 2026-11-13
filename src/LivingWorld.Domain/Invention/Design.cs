using System.Globalization;
using System.Text;
using LivingWorld.Domain.Shared;

namespace LivingWorld.Domain.Invention;

/// <summary>Primitivas de forma, as mesmas de <c>ObjectPart</c>. Eixo longo sempre em X (sentido do movimento).</summary>
public enum PartShape : byte { Box = 0, Cylinder = 1, Sphere = 2, Capsule = 3 }

/// <summary>Junção de uma parte à mãe: fixa (cavilha), eixo, dobradiça, corrediça ou flexível (amarração).</summary>
public enum JointKind : byte { Fixed = 0, Axle = 1, Hinge = 2, Slider = 3, Flexible = 4 }

/// <summary>Como a pessoa chegou a conhecer um design.</summary>
public enum DesignOrigin : byte { Invented = 0, Taught = 1, Copied = 2, ReverseEngineered = 3 }

/// <summary>Uma parte primitiva (ADR-0045). Medidas em mm: <see cref="LengthMm"/> no eixo X; caixa usa largura (Y) e altura (Z);
/// cilindro e cápsula têm diâmetro = <see cref="WidthMm"/>; esfera tem diâmetro = <see cref="LengthMm"/>. <see cref="WallMm"/> 0 =
/// maciça; &gt; 0 = oca e aberta em cima (casco). <see cref="Material"/> e <see cref="Coating"/> (0 = sem revestimento) são ids de
/// <see cref="MaterialSpec"/>. <see cref="Parent"/> -1 = raiz. Offset: centro em X e Y, base em Z, relativo à origem do design.</summary>
public readonly record struct DesignPart(
    PartShape Shape, short LengthMm, short WidthMm, short HeightMm, short WallMm, short Material, short Coating,
    JointKind Joint, sbyte Parent, short OffsetXMm, short OffsetYMm, short OffsetZMm)
{
    public bool IsHollow => WallMm > 0;

    /// <summary>Altura que a parte ocupa (Z): caixa = altura; redondas = diâmetro.</summary>
    public int SpanZ => Shape switch { PartShape.Box => HeightMm, PartShape.Sphere => LengthMm, _ => WidthMm };

    /// <summary>Largura que a parte ocupa (Y).</summary>
    public int SpanY => Shape == PartShape.Sphere ? LengthMm : WidthMm;

    public int Top => OffsetZMm + SpanZ;
}

/// <summary>A concepção (ADR-0045): até <see cref="MaxParts"/> partes. O id é o hash estável da estrutura (igual em dois mundos);
/// a proveniência diz quem concebeu, quando e a partir de quê (pais = outros designs, 0 = nenhum). Nada aqui diz para que serve:
/// a função é o que o avaliador físico mede.</summary>
public sealed record Design(
    int Id, IReadOnlyList<DesignPart> Parts, long InventorId, long Tick, int ParentId, int SecondParentId, DesignOrigin Origin)
{
    public const int MaxParts = 16;

    public static Design Create(IReadOnlyList<DesignPart> parts, long inventor, long tick, int parent, int secondParent, DesignOrigin origin) =>
        new(IdOf(parts), parts, inventor, tick, parent, secondParent, origin);

    /// <summary>FNV-1a de 32 bits sobre os campos das partes, na ordem do design; positivo.</summary>
    public static int IdOf(IReadOnlyList<DesignPart> parts)
    {
        uint hash = 2166136261;
        void Mix(int value)
        {
            for (int b = 0; b < 4; b++)
            {
                hash ^= (byte)(value >> (8 * b));
                hash *= 16777619;
            }
        }

        foreach (var p in parts)
        {
            Mix((int)p.Shape); Mix(p.LengthMm); Mix(p.WidthMm); Mix(p.HeightMm); Mix(p.WallMm); Mix(p.Material); Mix(p.Coating);
            Mix((int)p.Joint); Mix(p.Parent); Mix(p.OffsetXMm); Mix(p.OffsetYMm); Mix(p.OffsetZMm);
        }
        int id = (int)(hash & 0x7FFFFFFF);
        return id == 0 ? 1 : id;
    }

    /// <summary>Partes como inteiros separados por vírgula (12 por parte): forma compacta do payload de evento agendado.</summary>
    public static string Encode(IReadOnlyList<DesignPart> parts)
    {
        var sb = new StringBuilder(parts.Count * 40);
        foreach (var p in parts)
        {
            if (sb.Length > 0) sb.Append(',');
            sb.Append(CultureInfo.InvariantCulture, $"{(int)p.Shape},{p.LengthMm},{p.WidthMm},{p.HeightMm},{p.WallMm},{p.Material},{p.Coating},{(int)p.Joint},{p.Parent},{p.OffsetXMm},{p.OffsetYMm},{p.OffsetZMm}");
        }
        return sb.ToString();
    }

    public static IReadOnlyList<DesignPart>? Decode(string text)
    {
        var fields = text.Split(',');
        if (fields.Length == 0 || fields.Length % 12 != 0) return null;
        var parts = new List<DesignPart>(fields.Length / 12);
        for (int i = 0; i < fields.Length; i += 12)
        {
            short F(int k) => short.Parse(fields[i + k], NumberStyles.Integer, CultureInfo.InvariantCulture);
            parts.Add(new DesignPart(
                (PartShape)F(0), F(1), F(2), F(3), F(4), F(5), F(6), (JointKind)F(7), (sbyte)F(8), F(9), F(10), F(11)));
        }
        return parts;
    }
}

/// <summary>O que alguém sabe de um design: fidelidade 0..1000 (o quanto da concepção está certa na cabeça) e como aprendeu.</summary>
public readonly record struct KnownDesign(long NpcId, int DesignId, short FidelityPm, DesignOrigin Origin, long SinceTick);

/// <summary>Instância produzida, de um domicílio, até gastar. À venda (AD-143): <see cref="MarketId"/> não nulo é o mercado onde está
/// exposta, <see cref="SellerId"/> quem a fez e recebe e <see cref="Price"/> o preço pedido agora (moedas); a casa de quem vendeu não a
/// usa enquanto está exposta.</summary>
public readonly record struct CraftedItem(long Id, int DesignId, long HouseholdId, CityId City, long BuiltTick, long ExpiresTick, short QualityPm,
    long SellerId = 0, long? MarketId = null, long Price = 0)
{
    public bool ForSale => MarketId is not null;
}

/// <summary>Lápide pequena (decisão 5 do ADR-0045): design que ninguém sabe e de que não sobrou instância. <see cref="City"/> é onde
/// nasceu; <see cref="Accounts"/>, quantos fatos e relatos da história ainda falam dele quando se perdeu (AD-143): os relatos
/// apontam para a lápide pelo id do design.</summary>
public readonly record struct DesignTombstone(int DesignId, long InventorId, long InventedTick, long LostTick, int PartCount,
    CityId City = default, int Accounts = 0);

/// <summary>Material bruto guardado numa casa (g): sobra de uma construção que falhou ou o que veio de fora (AD-143).</summary>
public readonly record struct HouseholdMaterial(long HouseholdId, int MaterialId, long Grams);

/// <summary>Encomenda em andamento na oficina de quem sabe (AD-143): faltam <see cref="MinutesLeft"/> de trabalho; <see cref="MarketId"/>
/// não nulo = para vender lá, nulo = para a própria casa. A fidelidade de quem faz vira erro de fabricação na conclusão; <see cref="Coins"/>
/// é o que se pagou no mercado pelo material (entra no preço pedido).</summary>
public readonly record struct DesignOrder(long NpcId, int DesignId, long MinutesLeft, long? MarketId, short FidelityPm, long StartTick, long Coins = 0);

/// <summary>Casa que foi ao mercado precisando atravessar água sem ter com quê, no mês corrente (AD-143); <see cref="Bought"/> = levou.</summary>
public readonly record struct DesignWant(long HouseholdId, CityId City, bool Bought);

/// <summary>Casas distintas que quiseram no mês passado, por cidade: a demanda que o vendedor e o preço leem (AD-143).</summary>
public readonly record struct DesignDemand(CityId City, int Households);

/// <summary>Peixe na água perto de uma cidade (g): cresce sozinho até a capacidade da água e cai com o que se pesca (AD-143).</summary>
public readonly record struct FishStock(CityId City, long Grams);
