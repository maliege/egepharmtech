#nullable enable
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PTCalc.Core.Models;

/// <summary>
/// Üçgen faz diyagramı sayfasının kaydedilebilir "belgesi": veri satırları, diyagram/grafik
/// ayarları ve grup başına stil. Aynı JSON hem tarayıcının localStorage'ında (otomatik
/// kayıt) hem de kullanıcının indirdiği .json dosyasında kullanılır; ileride sunucuya
/// kaydetme eklenirse yine bu biçim taşınır.
/// </summary>
public sealed class TernaryDocument
{
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;
    public DateTimeOffset SavedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Grid satırları olduğu gibi: [grup, sıra, oil, sur/cosur, water]. Boş satırlar atılır.</summary>
    public List<object?[]> Rows { get; set; } = new();

    public TernarySettings Settings { get; set; } = new();

    /// <summary>Grup ADINA göre stil (sıraya değil): satır eklenip çıkarılınca renk kaymaz.</summary>
    public Dictionary<string, GroupStyle> Styles { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public string ToJson() => JsonSerializer.Serialize(this, JsonOpts);

    /// <summary>Geçersiz/uyumsuz JSON'da null döner (istisna fırlatmaz).</summary>
    public static TernaryDocument? FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            var doc = JsonSerializer.Deserialize<TernaryDocument>(json, JsonOpts);
            if (doc is null || doc.Version < 1 || doc.Version > CurrentVersion) return null;
            doc.Styles = new Dictionary<string, GroupStyle>(doc.Styles, StringComparer.OrdinalIgnoreCase);
            doc.Rows = doc.Rows.Select(NormalizeRow).Where(r => r.Any(c => c is not null)).ToList();
            return doc;
        }
        catch (JsonException) { return null; }
    }

    /// <summary>
    /// Deserializasyon hücreleri JsonElement olarak bırakır; grid'e ve hesaba giden yolda
    /// ilkel değerler (string/double/null) gerekir.
    /// </summary>
    private static object?[] NormalizeRow(object?[] row) => row.Select(c => c switch
    {
        JsonElement e => e.ValueKind switch
        {
            JsonValueKind.Number => e.GetDouble(),
            JsonValueKind.String => (object?)e.GetString(),
            JsonValueKind.True => 1.0,
            JsonValueKind.False => 0.0,
            _ => null
        },
        _ => c
    }).ToArray();
}

/// <summary>Diyagram ve grafik sekmelerindeki bütün ayarlar.</summary>
public sealed class TernarySettings
{
    public string ChartTitle { get; set; } = "Ternary Phase Diagram";
    public string TitleOil { get; set; } = "Oil";
    public string TitleSurfactant { get; set; } = "Surfactant";
    public string TitleWater { get; set; } = "Water";
    public int PolygonCloseType { get; set; } = 1;
    public int EdgeType { get; set; } = 2;
    public int AxisInterval { get; set; } = 10;
    public int BorderAlpha { get; set; } = 180;
    public int FillAlpha { get; set; } = 80;
    public bool SmoothEdges { get; set; }
    public int SmoothTension { get; set; } = 60;
    public bool ShowGradientFill { get; set; }
    public int GradientOpacity { get; set; } = 40;
}

/// <summary>
/// Bir grubun kullanıcı tarafından değiştirilmiş stili. Her alan isteğe bağlıdır; null olan
/// alan için sayfanın genel varsayılanı (palet rengi, ortak opaklık kaydırıcıları) geçerlidir.
/// </summary>
public sealed class GroupStyle
{
    /// <summary>#rrggbb</summary>
    public string? Fill { get; set; }
    /// <summary>#rrggbb</summary>
    public string? Stroke { get; set; }
    /// <summary>0–255</summary>
    public int? FillAlpha { get; set; }
    /// <summary>0–255</summary>
    public int? StrokeAlpha { get; set; }
    public double? StrokeWidth { get; set; }

    [JsonIgnore]
    public bool IsEmpty => Fill is null && Stroke is null && FillAlpha is null && StrokeAlpha is null && StrokeWidth is null;
}

/// <summary>Çizimde kullanılan, tüm alanları çözülmüş stil.</summary>
public sealed record EffectiveStyle(string Fill, string Stroke, double FillOpacity, double StrokeOpacity, double StrokeWidth);

public static class GroupStyles
{
    public const double DefaultStrokeWidth = 2.0;

    /// <summary>Grup stilini genel varsayılanlarla birleştirir. Opaklıklar 0–1'e çevrilir.</summary>
    public static EffectiveStyle Resolve(GroupStyle? style, string defaultColor, int defaultFillAlpha, int defaultStrokeAlpha)
    {
        string fill = ValidHex(style?.Fill) ?? defaultColor;
        string stroke = ValidHex(style?.Stroke) ?? defaultColor;
        int fa = Math.Clamp(style?.FillAlpha ?? defaultFillAlpha, 0, 255);
        int sa = Math.Clamp(style?.StrokeAlpha ?? defaultStrokeAlpha, 0, 255);
        double w = style?.StrokeWidth is double sw && sw >= 0 && sw <= 20 ? sw : DefaultStrokeWidth;
        return new EffectiveStyle(fill, stroke, fa / 255.0, sa / 255.0, w);
    }

    /// <summary>"#rrggbb" biçimindeyse normalize eder (küçük harf), değilse null.</summary>
    public static string? ValidHex(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        s = s.Trim();
        if (s.Length != 7 || s[0] != '#') return null;
        for (int i = 1; i < 7; i++)
            if (!Uri.IsHexDigit(s[i])) return null;
        return s.ToLowerInvariant();
    }
}
