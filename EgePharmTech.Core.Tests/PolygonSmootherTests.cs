using EgePharmTech.Core.Services;
using EgePharmTech.Models;

namespace EgePharmTech.Core.Tests;

/// <summary>
/// Faz bölgesi sınırının Bezier ile yumuşatılması. Kritik durum: sayfanın eklediği
/// kapatma noktaları bazen ilk/son ölçüm noktasıyla çakışır; bu sıfır uzunluklu segment
/// üretmemeli, NaN çıkmamalı.
/// </summary>
public class PolygonSmootherTests
{
    private static PointD P(double x, double y) => new() { X = x, Y = y };

    private static readonly PointD[] Data =
    {
        P(0.10, 0.05), P(0.20, 0.15), P(0.35, 0.22), P(0.50, 0.20), P(0.62, 0.10)
    };

    [Fact]
    public void Dedupe_removes_consecutive_and_wraparound_duplicates()
    {
        var pts = new[] { P(0, 0), P(0, 0), P(1, 0), P(1, 1e-12), P(0, 1), P(0, 0) };
        var d = PolygonSmoother.Dedupe(pts);
        Assert.Equal(3, d.Count);
        Assert.Equal((0.0, 0.0), (d[0].X, d[0].Y));
        Assert.Equal((0.0, 1.0), (d[2].X, d[2].Y));
    }

    [Fact]
    public void Curve_passes_through_every_data_point()
    {
        var segs = PolygonSmoother.CatmullRomToBeziers(Data, tension: 1.0);
        Assert.Equal(Data.Length - 1, segs.Count);
        for (int i = 0; i < segs.Count; i++)
        {
            Assert.Equal(Data[i].X, segs[i].P0.X, 12);
            Assert.Equal(Data[i].Y, segs[i].P0.Y, 12);
            Assert.Equal(Data[i + 1].X, segs[i].P3.X, 12);
            Assert.Equal(Data[i + 1].Y, segs[i].P3.Y, 12);
            // Bezier(0) = P0, Bezier(1) = P3
            var a = PolygonSmoother.Evaluate(segs[i], 0);
            var b = PolygonSmoother.Evaluate(segs[i], 1);
            Assert.Equal(Data[i].X, a.X, 12);
            Assert.Equal(Data[i + 1].Y, b.Y, 12);
        }
    }

    [Fact]
    public void Zero_tension_collapses_to_straight_segments()
    {
        var segs = PolygonSmoother.CatmullRomToBeziers(Data, tension: 0.0);
        foreach (var s in segs)
        {
            Assert.Equal(s.P0.X, s.C1.X, 12);
            Assert.Equal(s.P0.Y, s.C1.Y, 12);
            Assert.Equal(s.P3.X, s.C2.X, 12);
            Assert.Equal(s.P3.Y, s.C2.Y, 12);
            // Orta nokta kirişin ortasında olmalı
            var m = PolygonSmoother.Evaluate(s, 0.5);
            Assert.Equal((s.P0.X + s.P3.X) / 2, m.X, 12);
            Assert.Equal((s.P0.Y + s.P3.Y) / 2, m.Y, 12);
        }
    }

    [Fact]
    public void Control_points_are_finite_and_stay_near_the_chord()
    {
        var segs = PolygonSmoother.CatmullRomToBeziers(Data, tension: 1.0);
        foreach (var s in segs)
        {
            Assert.All(new[] { s.C1.X, s.C1.Y, s.C2.X, s.C2.Y }, v => Assert.True(double.IsFinite(v)));
            // Kontrol noktaları kirişten (aşırı) uzaklaşmamalı: kiriş uzunluğunun 1 katı içinde
            double chord = Math.Sqrt(Math.Pow(s.P3.X - s.P0.X, 2) + Math.Pow(s.P3.Y - s.P0.Y, 2));
            double d1 = Math.Sqrt(Math.Pow(s.C1.X - s.P0.X, 2) + Math.Pow(s.C1.Y - s.P0.Y, 2));
            double d2 = Math.Sqrt(Math.Pow(s.C2.X - s.P3.X, 2) + Math.Pow(s.C2.Y - s.P3.Y, 2));
            Assert.True(d1 <= chord && d2 <= chord, $"kontrol noktası kirişten uzak: {d1:G3}/{d2:G3} > {chord:G3}");
        }
    }

    /// <summary>Kapatma noktası ilk ölçüm noktasıyla aynı: düşürülmeli, NaN çıkmamalı.</summary>
    [Fact]
    public void Closing_point_coinciding_with_first_or_last_data_point_is_dropped()
    {
        var start = P(0.10, 0.05);            // == Data[0]
        var end = P(0.62, 0.10 + 5e-10);      // ≈ Data[^1] (eps içinde)

        var o = PolygonSmoother.Build(Data, start, end, tension: 1.0);

        Assert.Null(o.Start);
        Assert.Null(o.End);
        Assert.Equal(Data.Length, o.DataPoints.Count);
        Assert.Equal(Data.Length - 1, o.Curve.Count);
        Assert.All(o.Curve, s => Assert.All(
            new[] { s.C1.X, s.C1.Y, s.C2.X, s.C2.Y }, v => Assert.True(double.IsFinite(v))));
    }

    [Fact]
    public void Distinct_closing_points_are_kept_as_straight_ends()
    {
        var start = P(0.10, 0.0);
        var end = P(0.62, 0.0);
        var o = PolygonSmoother.Build(Data, start, end, tension: 1.0);

        Assert.NotNull(o.Start);
        Assert.NotNull(o.End);
        var outline = PolygonSmoother.SampleOutline(o, segmentsPerSpan: 4);
        Assert.Equal(start, outline[0]);
        Assert.Equal(end, outline[^1]);
        // 4 aralık × 4 örnek + ilk nokta + 2 kapatma
        Assert.Equal(1 + 4 * 4 + 2, outline.Count);
    }

    [Fact]
    public void Duplicate_data_points_do_not_break_the_curve()
    {
        var withDup = new[] { Data[0], Data[0], Data[1], Data[2], Data[2], Data[3], Data[4] };
        var o = PolygonSmoother.Build(withDup, null, null, tension: 1.0);
        Assert.Equal(Data.Length, o.DataPoints.Count);
        Assert.All(o.Curve, s => Assert.True(double.IsFinite(s.C1.X + s.C1.Y + s.C2.X + s.C2.Y)));
    }

    [Fact]
    public void Fewer_than_two_distinct_points_yield_no_curve()
    {
        var o = PolygonSmoother.Build(new[] { P(0.2, 0.2), P(0.2, 0.2) }, P(0.2, 0.0), null, 1.0);
        Assert.Empty(o.Curve);
        Assert.Single(o.DataPoints);
    }

    /// <summary>Yumuşatılmış çerçevenin alanı kırık çizgininkine yakın olmalı (aşırı şişme yok).</summary>
    [Fact]
    public void Sampled_outline_area_is_close_to_polygon_area()
    {
        var start = P(0.10, 0.0);
        var end = P(0.62, 0.0);
        var poly = new List<PointD> { start }; poly.AddRange(Data); poly.Add(end);

        var o = PolygonSmoother.Build(Data, start, end, tension: 1.0);
        var smooth = PolygonSmoother.SampleOutline(o, 16);

        double a1 = Area(poly), a2 = Area(smooth);
        Assert.InRange(a2 / a1, 0.9, 1.1);
    }

    // ---------------- Dışbükey bölge kısıtı ----------------

    private const double Sin60 = 0.86602540378443864676;
    private static PolygonSmoother.ConvexRegion Triangle()
        => PolygonSmoother.ConvexRegion.Triangle(P(0, 0), P(1, 0), P(0.5, Sin60));

    [Fact]
    public void Region_contains_and_clamp_behave_on_the_triangle()
    {
        var tri = Triangle();
        Assert.True(tri.Contains(P(0.5, 0.2)));
        Assert.True(tri.Contains(P(0.5, 0)));            // kenar üzerinde
        Assert.True(tri.Contains(P(0, 0)));              // köşe
        Assert.False(tri.Contains(P(0.5, -0.01)));
        Assert.False(tri.Contains(P(1.0, 0.1)));

        var q = tri.Clamp(P(0.5, -0.1));
        Assert.Equal(0.5, q.X, 12);
        Assert.Equal(0.0, q.Y, 12);
        var inside = P(0.4, 0.1);
        Assert.Same(inside, tri.Clamp(inside));
    }

    /// <summary>
    /// Kenara yapışık veri: taban kenarında (y = 0) noktalar ve arada bir çıkıntı. Kısıtsız
    /// Catmull-Rom kontrol noktalarını y &lt; 0'a iter; bölgeyle eğri üçgenin dışına çıkamaz.
    /// </summary>
    [Fact]
    public void Curve_hugging_an_edge_stays_inside_the_triangle_when_constrained()
    {
        var data = new[] { P(0.20, 0.00), P(0.30, 0.00), P(0.40, 0.08), P(0.50, 0.00), P(0.60, 0.00) };

        var free = PolygonSmoother.Build(data, null, null, 1.0);
        var constrained = PolygonSmoother.Build(data, null, null, 1.0, Triangle());

        // Kısıtsız hâl gerçekten taşıyor mu? (testin anlamlı olması için)
        Assert.Contains(PolygonSmoother.SampleOutline(free, 16), p => p.Y < -1e-9);

        var tri = Triangle();
        Assert.All(PolygonSmoother.SampleOutline(constrained, 16), p => Assert.True(tri.Contains(p, 1e-9),
            $"({p.X:G6}, {p.Y:G6}) üçgen dışında"));

        // Eğri yine ölçüm noktalarından geçiyor
        for (int i = 0; i < constrained.Curve.Count; i++)
        {
            Assert.Equal(data[i].X, constrained.Curve[i].P0.X, 12);
            Assert.Equal(data[i + 1].X, constrained.Curve[i].P3.X, 12);
        }
    }

    /// <summary>Min-değer kapatması: üçgen, kapatma çizgisiyle kesilir; eğri çizginin ötesine geçemez.</summary>
    [Fact]
    public void Clipping_the_triangle_by_the_closing_line_keeps_the_curve_on_the_data_side()
    {
        // Yatay kapatma çizgisi y = 0.10; veri onun üstünde ama ona yakın
        var l1 = P(0.0, 0.10); var l2 = P(1.0, 0.10);
        var data = new[] { P(0.25, 0.10), P(0.32, 0.11), P(0.40, 0.25), P(0.48, 0.11), P(0.55, 0.10) };

        var region = Triangle().ClipByLine(l1, l2, keep: P(0.4, 0.25));
        Assert.Equal(3, region.Vertices.Count);                 // taban kesilir: tepe + iki kesişim noktası kalır
        Assert.All(region.Vertices, v => Assert.True(v.Y >= 0.10 - 1e-9));

        var o = PolygonSmoother.Build(data, P(0.25, 0.10), P(0.55, 0.10), 1.0, region);
        Assert.All(PolygonSmoother.SampleOutline(o, 16), p => Assert.True(p.Y >= 0.10 - 1e-9,
            $"y={p.Y:G6} kapatma çizgisinin altında"));
    }

    /// <summary>Gerilim 1'in üstü: kontrol noktaları daha açık ama sonlu; bölge kısıtıyla eğri yine içeride.</summary>
    [Fact]
    public void Over_tension_is_rounder_but_stays_finite_and_inside_the_region()
    {
        var one = PolygonSmoother.CatmullRomToBeziers(Data, 1.0);
        var over = PolygonSmoother.CatmullRomToBeziers(Data, PolygonSmoother.MaxTension);

        double Off(PolygonSmoother.CubicSegment s) =>
            Math.Sqrt(Math.Pow(s.C1.X - s.P0.X, 2) + Math.Pow(s.C1.Y - s.P0.Y, 2));
        for (int i = 0; i < one.Count; i++)
        {
            Assert.True(Off(over[i]) >= Off(one[i]) - 1e-12, "gerilim artınca kontrol noktası uzaklaşmalı");
            Assert.All(new[] { over[i].C1.X, over[i].C1.Y, over[i].C2.X, over[i].C2.Y }, v => Assert.True(double.IsFinite(v)));
        }

        var data = new[] { P(0.20, 0.00), P(0.30, 0.00), P(0.40, 0.08), P(0.50, 0.00), P(0.60, 0.00) };
        var o = PolygonSmoother.Build(data, null, null, PolygonSmoother.MaxTension, Triangle());
        var tri = Triangle();
        Assert.All(PolygonSmoother.SampleOutline(o, 16), p => Assert.True(tri.Contains(p, 1e-9)));

        // MaxTension üstü istekler kırpılır
        var clamped = PolygonSmoother.CatmullRomToBeziers(Data, 99);
        Assert.Equal(over[1].C1.X, clamped[1].C1.X, 12);
    }

    [Fact]
    public void Clipping_with_keep_point_on_the_line_returns_the_region_unchanged()
    {
        var tri = Triangle();
        var same = tri.ClipByLine(P(0, 0), P(1, 0), keep: P(0.5, 0));   // keep doğru üzerinde
        Assert.Same(tri, same);
    }

    private static double Area(IReadOnlyList<PointD> p)
    {
        double s = 0;
        for (int i = 0; i < p.Count; i++)
        {
            var a = p[i]; var b = p[(i + 1) % p.Count];
            s += a.X * b.Y - b.X * a.Y;
        }
        return Math.Abs(s) / 2;
    }
}
