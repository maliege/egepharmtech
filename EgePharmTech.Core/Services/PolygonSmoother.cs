#nullable enable
using EgePharmTech.Core.Models;
using EgePharmTech.Core.Localization;

namespace EgePharmTech.Core.Services;

/// <summary>
/// Üçgen faz diyagramındaki faz bölgesi sınırını kırık çizgi yerine ölçüm noktalarından
/// <b>geçen</b> yumuşak bir eğriyle çizmek için geometri yardımcıları. SVG'den bağımsızdır:
/// veri (x, y) uzayında çalışır, çağıran taraf noktaları ekrana kendi dönüşümüyle taşır
/// (afin dönüşüm Bezier kontrol noktalarını korur).
///
/// <para><b>Yöntem:</b> centripetal Catmull-Rom (α = 0,5) → kübik Bezier. Eğri her ölçüm
/// noktasından geçer; α = 0,5 düzensiz aralıklı noktalarda kıvrım/ilmek oluşmasını önler.
/// <c>tension</c> 0 → kontrol noktaları uç noktalara çöker (düz çizgi), 1 → tam Catmull-Rom.</para>
///
/// <para><b>Çakışan noktalar:</b> sayfa, kapatma modunda başa ve sona üçgen kenarı üzerinde
/// iki nokta ekler; bunlar zaman zaman ilk ya da son ölçüm noktasıyla <i>aynı</i> çıkar
/// (ölçüm zaten kenardaysa). Ardışık çakışan noktalar sıfır uzunluklu segment → NaN teğet
/// üretir; bu yüzden her giriş önce <see cref="Dedupe"/>'tan geçer.</para>
/// </summary>
public static class PolygonSmoother
{
    public const double DefaultEpsilon = 1e-9;

    /// <summary>Bir kübik Bezier parçası: P0'dan P3'e, kontrol noktaları C1, C2.</summary>
    public readonly record struct CubicSegment(PointD P0, PointD C1, PointD C2, PointD P3);

    /// <summary>
    /// Yumuşatılmış, kapalı bir bölge çizimi için hazır bileşenler.
    /// <see cref="Start"/>/<see cref="End"/> kapatma noktalarıdır (varsa) ve düz çizgiyle
    /// bağlanır; <see cref="Curve"/> ölçüm noktalarından geçen Bezier parçalarıdır.
    /// </summary>
    public sealed record SmoothedOutline(
        PointD? Start,
        IReadOnlyList<CubicSegment> Curve,
        PointD? End,
        IReadOnlyList<PointD> DataPoints);

    /// <summary>Ardışık çakışan noktaları (ve kapalı çizgide son≈ilk çakışmasını) eler.</summary>
    public static List<PointD> Dedupe(IReadOnlyList<PointD> points, double eps = DefaultEpsilon)
    {
        var result = new List<PointD>(points.Count);
        foreach (var p in points)
        {
            if (result.Count == 0 || !Same(result[^1], p, eps))
                result.Add(p);
        }
        while (result.Count > 1 && Same(result[0], result[^1], eps))
            result.RemoveAt(result.Count - 1);
        return result;
    }

    /// <summary>
    /// Ölçüm noktaları + isteğe bağlı kapatma noktalarından yumuşatılmış çerçeve üretir.
    /// Kapatma noktası komşu ölçüm noktasıyla çakışıyorsa düşürülür. Yumuşatma için en az
    /// iki farklı ölçüm noktası gerekir; yoksa <see cref="SmoothedOutline.Curve"/> boş döner
    /// ve çağıran taraf kırık çizgiye düşmelidir.
    ///
    /// <para><paramref name="region"/> verilirse Bezier kontrol noktaları bu dışbükey bölgeye
    /// sıkıştırılır. Kübik Bezier her zaman dört kontrol noktasının dışbükey zarfı içinde
    /// kaldığından, uç noktalar (ölçümler) bölgedeyse eğrinin tamamı da bölgede kalır —
    /// kenara yakın verilerde eğri üçgen dışına, min-değer kapatmasında kapatma çizgisinin
    /// ötesine <b>çıkamaz</b>. Ölçüm noktalarına dokunulmaz.</para>
    /// </summary>
    public static SmoothedOutline Build(
        IReadOnlyList<PointD> dataPoints, PointD? start, PointD? end,
        double tension, ConvexRegion? region = null, double eps = DefaultEpsilon)
    {
        var data = Dedupe(dataPoints, eps);

        if (start is not null && data.Count > 0 && Same(start, data[0], eps)) start = null;
        if (end is not null && data.Count > 0 && Same(end, data[^1], eps)) end = null;
        if (start is not null && end is not null && Same(start, end, eps)) end = null;

        IReadOnlyList<CubicSegment> curve = data.Count >= 2
            ? CatmullRomToBeziers(data, tension)
            : Array.Empty<CubicSegment>();

        if (region is not null && curve.Count > 0)
            curve = curve.Select(s => s with { C1 = region.Clamp(s.C1), C2 = region.Clamp(s.C2) }).ToArray();

        return new SmoothedOutline(start, curve, end, data);
    }

    /// <summary>
    /// Dışbükey çokgen bölge (köşeler sıralı; yön fark etmez). Faz diyagramında üçgenin
    /// kendisi ya da min-değer kapatma çizgisiyle kesilmiş üçgen için kullanılır.
    /// </summary>
    public sealed class ConvexRegion
    {
        public IReadOnlyList<PointD> Vertices { get; }

        public ConvexRegion(IReadOnlyList<PointD> vertices)
        {
            if (vertices.Count < 3) throw new ArgumentException(CoreText.T("Bölge için en az 3 köşe gerekir."));
            Vertices = vertices;
        }

        public static ConvexRegion Triangle(PointD a, PointD b, PointD c) => new(new[] { a, b, c });

        /// <summary>
        /// Bölgeyi (l1, l2) doğrusuyla keser; <paramref name="keep"/> noktasının bulunduğu
        /// yarı düzlem kalır (Sutherland–Hodgman, tek kenar). <paramref name="keep"/> doğru
        /// üzerindeyse bölge değişmeden döner.
        /// </summary>
        public ConvexRegion ClipByLine(PointD l1, PointD l2, PointD keep)
        {
            double Side(PointD p) => (l2.X - l1.X) * (p.Y - l1.Y) - (l2.Y - l1.Y) * (p.X - l1.X);
            double sk = Side(keep);
            if (Math.Abs(sk) < 1e-12) return this;
            double sign = Math.Sign(sk);

            var outp = new List<PointD>();
            int n = Vertices.Count;
            for (int i = 0; i < n; i++)
            {
                var cur = Vertices[i];
                var nxt = Vertices[(i + 1) % n];
                double sc = Side(cur) * sign, sn = Side(nxt) * sign;
                bool curIn = sc >= -1e-12, nxtIn = sn >= -1e-12;

                if (curIn) outp.Add(cur);
                if (curIn != nxtIn)
                {
                    double t = sc / (sc - sn);
                    outp.Add(new PointD { X = cur.X + (nxt.X - cur.X) * t, Y = cur.Y + (nxt.Y - cur.Y) * t });
                }
            }
            return outp.Count >= 3 ? new ConvexRegion(Dedupe(outp)) : this;
        }

        /// <summary>Nokta bölgenin içinde ya da kenarında mı (tolerans dahil)?</summary>
        public bool Contains(PointD p, double eps = 1e-9)
        {
            int n = Vertices.Count;
            double orient = 0;
            for (int i = 0; i < n; i++)
            {
                var a = Vertices[i]; var b = Vertices[(i + 1) % n];
                double s = (b.X - a.X) * (p.Y - a.Y) - (b.Y - a.Y) * (p.X - a.X);
                if (Math.Abs(s) <= eps) continue;
                if (orient == 0) orient = Math.Sign(s);
                else if (Math.Sign(s) != orient) return false;
            }
            return true;
        }

        /// <summary>Nokta içerideyse kendisi; değilse bölge sınırındaki en yakın nokta.</summary>
        public PointD Clamp(PointD p)
        {
            if (Contains(p)) return p;
            PointD best = p; double bestD = double.MaxValue;
            int n = Vertices.Count;
            for (int i = 0; i < n; i++)
            {
                var a = Vertices[i]; var b = Vertices[(i + 1) % n];
                double dx = b.X - a.X, dy = b.Y - a.Y;
                double len2 = dx * dx + dy * dy;
                double t = len2 < 1e-24 ? 0 : Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / len2, 0, 1);
                var q = new PointD { X = a.X + dx * t, Y = a.Y + dy * t };
                double d = (q.X - p.X) * (q.X - p.X) + (q.Y - p.Y) * (q.Y - p.Y);
                if (d < bestD) { bestD = d; best = q; }
            }
            return best;
        }
    }

    /// <summary>En büyük gerilim: 1 = klasik Catmull-Rom; üstü kontrol noktalarını daha da açar (daha yuvarlak).</summary>
    public const double MaxTension = 1.6;

    /// <summary>
    /// Açık Catmull-Rom eğrisini (centripetal, α = 0,5) kübik Bezier parçalarına çevirir.
    /// Noktalar ardışık çakışma içermemeli (bkz. <see cref="Dedupe"/>).
    /// <paramref name="tension"/> 1'in üstünde kontrol noktaları Catmull-Rom konumunun ötesine
    /// itilir; eğri daha yuvarlak olur ama sivri köşelerde kıvrım riski artar. Dışbükey bölge
    /// kısıtı (<see cref="Build"/>) bu durumda da eğriyi bölgede tutar.
    /// </summary>
    public static IReadOnlyList<CubicSegment> CatmullRomToBeziers(IReadOnlyList<PointD> pts, double tension, double alpha = 0.5)
    {
        tension = Math.Clamp(tension, 0, MaxTension);
        var segs = new List<CubicSegment>(Math.Max(0, pts.Count - 1));
        if (pts.Count < 2) return segs;

        for (int i = 0; i < pts.Count - 1; i++)
        {
            var p1 = pts[i];
            var p2 = pts[i + 1];
            var p0 = i > 0 ? pts[i - 1] : null;
            var p3 = i + 2 < pts.Count ? pts[i + 2] : null;

            double d2 = Math.Pow(Dist(p1, p2), alpha);

            // Uç parçalarda komşu yoksa doğal (düz) teğet: kontrol noktası kirişin 1/3'ünde.
            PointD c1, c2;
            if (p0 is null || d2 < eps0)
                c1 = Lerp(p1, p2, 1.0 / 3.0);
            else
            {
                double d1 = Math.Pow(Dist(p0, p1), alpha);
                if (d1 < eps0) c1 = Lerp(p1, p2, 1.0 / 3.0);
                else
                {
                    double d1s = d1 * d1, d2s = d2 * d2;
                    double denom = 3 * d1 * (d1 + d2);
                    c1 = new PointD
                    {
                        X = (d1s * p2.X - d2s * p0.X + (2 * d1s + 3 * d1 * d2 + d2s) * p1.X) / denom,
                        Y = (d1s * p2.Y - d2s * p0.Y + (2 * d1s + 3 * d1 * d2 + d2s) * p1.Y) / denom
                    };
                }
            }

            if (p3 is null || d2 < eps0)
                c2 = Lerp(p2, p1, 1.0 / 3.0);
            else
            {
                double d3 = Math.Pow(Dist(p2, p3), alpha);
                if (d3 < eps0) c2 = Lerp(p2, p1, 1.0 / 3.0);
                else
                {
                    double d3s = d3 * d3, d2s = d2 * d2;
                    double denom = 3 * d3 * (d3 + d2);
                    c2 = new PointD
                    {
                        X = (d3s * p1.X - d2s * p3.X + (2 * d3s + 3 * d3 * d2 + d2s) * p2.X) / denom,
                        Y = (d3s * p1.Y - d2s * p3.Y + (2 * d3s + 3 * d3 * d2 + d2s) * p2.Y) / denom
                    };
                }
            }

            // Gerilim: kontrol noktalarını uç noktalara doğru çek (0 → düz çizgi) ya da
            // 1'in üstünde onlardan uzağa it (daha yuvarlak)
            c1 = Lerp(p1, c1, tension);
            c2 = Lerp(p2, c2, tension);

            segs.Add(new CubicSegment(p1, c1, c2, p2));
        }
        return segs;
    }

    /// <summary>
    /// Yumuşatılmış çerçeveyi kapalı bir çokgen olarak örnekler (alan/ağırlık merkezi için).
    /// Kapatma noktaları düz kenar olarak eklenir; kapalı çokgenin son noktası ilkine bağlanır.
    /// </summary>
    public static List<PointD> SampleOutline(SmoothedOutline outline, int segmentsPerSpan = 8)
    {
        var pts = new List<PointD>();
        if (outline.Start is not null) pts.Add(outline.Start);

        if (outline.Curve.Count == 0)
            pts.AddRange(outline.DataPoints);
        else
        {
            pts.Add(outline.Curve[0].P0);
            foreach (var s in outline.Curve)
                for (int k = 1; k <= segmentsPerSpan; k++)
                    pts.Add(Evaluate(s, (double)k / segmentsPerSpan));
        }

        if (outline.End is not null) pts.Add(outline.End);
        return Dedupe(pts);
    }

    /// <summary>Kübik Bezier'i t ∈ [0,1]'de değerlendirir.</summary>
    public static PointD Evaluate(CubicSegment s, double t)
    {
        double u = 1 - t;
        double b0 = u * u * u, b1 = 3 * u * u * t, b2 = 3 * u * t * t, b3 = t * t * t;
        return new PointD
        {
            X = b0 * s.P0.X + b1 * s.C1.X + b2 * s.C2.X + b3 * s.P3.X,
            Y = b0 * s.P0.Y + b1 * s.C1.Y + b2 * s.C2.Y + b3 * s.P3.Y
        };
    }

    private const double eps0 = 1e-12;

    private static bool Same(PointD a, PointD b, double eps)
        => Math.Abs(a.X - b.X) <= eps && Math.Abs(a.Y - b.Y) <= eps;

    private static double Dist(PointD a, PointD b)
        => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    private static PointD Lerp(PointD a, PointD b, double t)
        => new() { X = a.X + (b.X - a.X) * t, Y = a.Y + (b.Y - a.Y) * t };
}
