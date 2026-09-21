#nullable enable
using PTCalc.Core.Localization;

namespace PTCalc.Core.Dissolution.Models;

/// <summary>
/// Logistic (Fmax): F = Fmax/(1 + e^(−k·(t−g))).
///
/// <b>Neden ayrı model?</b> Şartname §7'nin Fmax varyantı "100 → Fmax" ikamesidir; ama
/// DDSolver'ın Logistic (Fmax)'ı taban Logistic'ten farklı bir <i>parametrizasyondur</i>:
/// taban <c>log t</c> tabanlıyken (a + b·log t) bu form doğrudan <c>t</c> tabanlıdır
/// (k·(t−g)). Aynı eğri ailesi değildir → dekoratörle üretilemez, ayrı tanımlanır.
/// </summary>
public sealed class LogisticFmaxModel : DissolutionModelBase
{
    /// <summary>Fmax'ı denkleminde taşıyan sigmoidler için ortak %100 üstü uyarısı.</summary>
    internal static IReadOnlyList<string> FmaxFlags(double fmax)
        => fmax > 100.0 + 1e-9
            ? new[] { CoreText.T("UYARI: Fmax = %{0:0.#} > %100. Plato fiziksel sınırı aşıyor; parametreler birbirini telafi ediyor olabilir.", fmax) }
            : Array.Empty<string>();

    public override string Name => "Logistic (Fmax)";
    public override string Equation => "F = Fmax/(1+Exp(-k*(t-g)))";
    public override IReadOnlyList<string> ParamNames => new[] { "k", "g", "Fmax" };
    public override bool SupportsF0 => false;
    public override bool SupportsFmax => false;  // Fmax zaten modelin içinde

    public override double[] LowerBounds => new[] { 0.0, double.NegativeInfinity, 0.0 };
    public override double[] UpperBounds => new[] { double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity };

    public override IReadOnlyList<Coefficient> Describe(double[] p) => new[]
    {
        new Coefficient("k", p[0], CoreText.T("1/zaman"), CoreText.T("Sigmoid eğim (hız) parametresi")),
        new Coefficient("g", p[1], CoreText.T("zaman"), CoreText.T("Dönüm noktası (Fmax/2'ye ulaşılan süre)")),
        new Coefficient("Fmax", p[2], "%", CoreText.T("Ulaşılan maksimum salım (plato)"))
    };

    /// <summary>Tohum: Fmax = 1.05·max(F); y = ln(F/(Fmax−F)), x = t → k = eğim, g = −kesişim/k.</summary>
    public override double[] InitialGuess(IReadOnlyList<double> t, IReadOnlyList<double> f, VariantOptions opt)
    {
        double fmax = SeedFmax(f);
        var (xs, ys) = SeedPoints(t, f,
            ti => ti,
            fi => Math.Log(fi / (fmax - fi)),
            (_, fi) => fi > 0 && fi < fmax);

        if (xs.Count < 2) return new[] { 0.1, t.Count > 0 ? t.Average() : 1.0, fmax };

        var (intercept, slope) = SeedRegression.WithIntercept(xs, ys);
        double k = slope;
        double g = Math.Abs(k) > 1e-12 ? -intercept / k : (t.Count > 0 ? t.Average() : 1.0);
        return new[]
        {
            IsFinite(k) && k > 0 ? k : 0.1,
            IsFinite(g) ? g : 1.0,
            fmax
        };
    }

    public override double EvaluateCore(double t, double[] p, double ceiling, VariantOptions opt)
    {
        if (t < 0) return 0;
        return p[2] / (1 + Math.Exp(-p[0] * (t - p[1])));
    }

    public override IReadOnlyList<string> Flags(double[] p, VariantOptions opt) => FmaxFlags(p[2]);

    /// <summary>Tx = g − ln(Fmax/x − 1)/k. x ≥ Fmax → "Non Calc".</summary>
    public override IReadOnlyList<SecondaryValue> SecondaryCore(double[] p, double ceiling, VariantOptions opt)
        => SolveTargets(p[2], x =>
        {
            double k = p[0], g = p[1], fmax = p[2];
            if (Math.Abs(k) < 1e-12 || x <= 0 || x >= fmax) return double.NaN;
            return g - Math.Log(fmax / x - 1) / k;
        });
}

/// <summary>
/// Gompertz (Fmax): F = Fmax·e^(−e^(−k·(t−g))).
/// <see cref="LogisticFmaxModel"/> ile aynı gerekçe: taban Gompertz <c>log t</c> tabanlı,
/// bu form <c>t</c> tabanlı → ayrı model.
/// </summary>
public sealed class GompertzFmaxModel : DissolutionModelBase
{
    public override string Name => "Gompertz (Fmax)";
    public override string Equation => "F = Fmax*Exp(-Exp(-k*(t-g)))";
    public override IReadOnlyList<string> ParamNames => new[] { "k", "g", "Fmax" };
    public override bool SupportsF0 => false;
    public override bool SupportsFmax => false;

    public override double[] LowerBounds => new[] { 0.0, double.NegativeInfinity, 0.0 };
    public override double[] UpperBounds => new[] { double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity };

    public override IReadOnlyList<Coefficient> Describe(double[] p) => new[]
    {
        new Coefficient("k", p[0], CoreText.T("1/zaman"), CoreText.T("Gompertz hız parametresi")),
        new Coefficient("g", p[1], CoreText.T("zaman"), CoreText.T("Dönüm noktası")),
        new Coefficient("Fmax", p[2], "%", CoreText.T("Ulaşılan maksimum salım (plato)"))
    };

    /// <summary>Tohum: y = ln(−ln(F/Fmax)), x = t → k = −eğim, g = kesişim/k.</summary>
    public override double[] InitialGuess(IReadOnlyList<double> t, IReadOnlyList<double> f, VariantOptions opt)
    {
        double fmax = SeedFmax(f);
        var (xs, ys) = SeedPoints(t, f,
            ti => ti,
            fi => Math.Log(-Math.Log(fi / fmax)),
            (_, fi) => fi > 0 && fi < fmax);

        if (xs.Count < 2) return new[] { 0.1, t.Count > 0 ? t.Average() : 1.0, fmax };

        var (intercept, slope) = SeedRegression.WithIntercept(xs, ys);
        double k = -slope;
        double g = Math.Abs(k) > 1e-12 ? intercept / k : (t.Count > 0 ? t.Average() : 1.0);
        return new[]
        {
            IsFinite(k) && k > 0 ? k : 0.1,
            IsFinite(g) ? g : 1.0,
            fmax
        };
    }

    public override double EvaluateCore(double t, double[] p, double ceiling, VariantOptions opt)
    {
        if (t < 0) return 0;
        return p[2] * Math.Exp(-Math.Exp(-p[0] * (t - p[1])));
    }

    public override IReadOnlyList<string> Flags(double[] p, VariantOptions opt) => LogisticFmaxModel.FmaxFlags(p[2]);

    /// <summary>Tx = g − ln(−ln(x/Fmax))/k. x ≥ Fmax → "Non Calc".</summary>
    public override IReadOnlyList<SecondaryValue> SecondaryCore(double[] p, double ceiling, VariantOptions opt)
        => SolveTargets(p[2], x =>
        {
            double k = p[0], g = p[1], fmax = p[2];
            if (Math.Abs(k) < 1e-12 || x <= 0 || x >= fmax) return double.NaN;
            return g - Math.Log(-Math.Log(x / fmax)) / k;
        });
}
