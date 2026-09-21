#nullable enable
using MathNet.Numerics.Distributions;
using PTCalc.Core.Localization;

namespace PTCalc.Core.Dissolution.Models;

/// <summary>
/// Weibull: F = 100·(1 − e^(−(t^β)/α)) (şartname §5.6). Costa Eq. 13/14 (Langenbucher, 1972).
///
/// Şartname §6.2: <b>Langenbucher, Modified Langenbucher ve RRSBW bu dağılımın farklı
/// parametrizasyonlarıdır — aynı modeldir.</b> Eski motordaki 4 ayrı enum girdisi burada
/// tek modelde birleşir; gecikmeli hâli (Modified Langenbucher) Ti varyantıdır.
///
/// <b>Parametrizasyon uyarısı:</b> Burada α ölçek parametresi <c>t^β/α</c> biçimindedir
/// (şartname/DDSolver). Eski <c>Kinetik.razor</c> kodu <c>(t/α)^β</c> kullanıyordu —
/// bunlar farklı α tanımlarıdır (α_razor^β = α_buradaki).
/// </summary>
public sealed class WeibullModel : DissolutionModelBase
{
    public override string Name => "Weibull";
    public override string Equation => "F = 100*(1-Exp(-(t^b)/a))";
    public override IReadOnlyList<string> ParamNames => new[] { "a", "b" };
    public override bool SupportsF0 => false;   // doyuma gider → Fmax kullan
    public override bool SupportsFmax => true;

    public override double[] LowerBounds => new[] { 1e-9, 1e-9 };
    public override double[] UpperBounds => new[] { double.PositiveInfinity, double.PositiveInfinity };

    public override IReadOnlyList<Coefficient> Describe(double[] p)
    {
        var list = new List<Coefficient>
        {
            new("a", p[0], CoreText.T("zaman^b"), CoreText.T("Ölçek parametresi (α)")),
            new("b", p[1], "-", CoreText.T("Şekil parametresi (β): <1 parabolik, =1 üstel, >1 sigmoid"))
        };
        // Costa: "α, daha bilgilendirici olan Td ile değiştirilebilir; a = Td^b"
        double td = Td(p);
        if (IsFinite(td))
            list.Add(new Coefficient("Td", td, CoreText.T("zaman"), CoreText.T("%63.2 salım süresi (a = Td^b)")));
        return list;
    }

    /// <summary>Costa: a = (Td)^b → Td = a^(1/b). %63.2 salıma karşılık gelen süre.</summary>
    public static double Td(double[] p)
        => p[0] > 0 && p[1] > 0 ? Math.Pow(p[0], 1.0 / p[1]) : double.NaN;

    /// <summary>Tohum: y=log10(−ln(1−F/tavan)), x=log10 t, kesişimli → β=eğim, α=10^(−kesişim).</summary>
    public override double[] InitialGuess(IReadOnlyList<double> t, IReadOnlyList<double> f, VariantOptions opt)
    {
        double ceil = opt.UseFmax ? SeedFmax(f) : DefaultCeiling;
        var (xs, ys) = SeedPoints(t, f,
            ti => Math.Log10(ti),
            fi => Math.Log10(-Math.Log(1 - fi / ceil)),
            (ti, fi) => ti > 0 && fi > 0 && fi < ceil);

        if (xs.Count < 2) return new[] { 5.0, 1.0 };

        var (intercept, slope) = SeedRegression.WithIntercept(xs, ys);
        double b = slope;
        double a = Math.Pow(10, -intercept);
        return new[]
        {
            IsFinite(a) && a > 0 ? a : 5.0,
            IsFinite(b) && b > 0 ? b : 1.0
        };
    }

    public override double EvaluateCore(double t, double[] p, double ceiling, VariantOptions opt)
    {
        if (t <= 0) return 0;
        double a = p[0], b = p[1];
        if (a <= 0) return double.NaN;
        return ceiling * (1 - Math.Exp(-Math.Pow(t, b) / a));
    }

    /// <summary>Tx = (α · (−ln(1 − x/tavan)))^(1/β).</summary>
    public override IReadOnlyList<SecondaryValue> SecondaryCore(double[] p, double ceiling, VariantOptions opt)
        => SolveTargets(ceiling, x =>
        {
            double a = p[0], b = p[1];
            if (a <= 0 || b <= 0) return double.NaN;
            return Math.Pow(a * -Math.Log(1 - x / ceiling), 1.0 / b);
        });

    public override IReadOnlyList<string> Flags(double[] p, VariantOptions opt)
    {
        double b = p[1];
        string shape = b < 0.75 ? CoreText.T("Parabolik (β<1): yüksek başlangıç eğimi, difüzyon baskın")
            : b > 1.0 ? CoreText.T("Sigmoid (β>1): S-şekilli, kompleks/gecikmeli salım")
            : CoreText.T("Üstel (β≈1): birinci derece benzeri");
        return new[] { shape };
    }
}

/// <summary>
/// Logistic: F = 100·e^(a+b·log t)/(1+e^(a+b·log t)) (şartname §5.13). log = 10 tabanı.
/// Not: Fmax'lı hâli farklı bir parametrizasyondur → ayrı model
/// (<see cref="LogisticFmaxModel"/>), dekoratör değil.
/// </summary>
public sealed class LogisticModel : DissolutionModelBase
{
    public override string Name => "Logistic";
    public override string Equation => "F = 100*Exp(a+b*log t)/(1+Exp(a+b*log t))";
    public override IReadOnlyList<string> ParamNames => new[] { "a", "b" };
    public override bool SupportsF0 => false;
    public override bool SupportsFmax => false;  // Fmax'lı hâli ayrı model

    public override double[] LowerBounds => new[] { double.NegativeInfinity, double.NegativeInfinity };
    public override double[] UpperBounds => new[] { double.PositiveInfinity, double.PositiveInfinity };

    public override IReadOnlyList<Coefficient> Describe(double[] p) => new[]
    {
        new Coefficient("a", p[0], "-", CoreText.T("Konum parametresi (kesişim)")),
        new Coefficient("b", p[1], "-", CoreText.T("Şekil parametresi (eğim)"))
    };

    /// <summary>Tohum: y = ln(F/(100−F)), x = log10 t, kesişimli → a=kesişim, b=eğim.</summary>
    public override double[] InitialGuess(IReadOnlyList<double> t, IReadOnlyList<double> f, VariantOptions opt)
    {
        var (xs, ys) = SeedPoints(t, f,
            Math.Log10,
            fi => Math.Log(fi / (DefaultCeiling - fi)),
            (ti, fi) => ti > 0 && fi > 0 && fi < DefaultCeiling);

        if (xs.Count < 2) return new[] { -2.0, 3.0 };
        var (a, b) = SeedRegression.WithIntercept(xs, ys);
        return new[] { IsFinite(a) ? a : -2.0, IsFinite(b) ? b : 3.0 };
    }

    public override double EvaluateCore(double t, double[] p, double ceiling, VariantOptions opt)
    {
        if (t <= 0) return 0;
        double z = p[0] + p[1] * Math.Log10(t);
        // Sayısal taşmayı önle: e^z/(1+e^z) = 1/(1+e^-z)
        return ceiling / (1 + Math.Exp(-z));
    }

    /// <summary>Tx = 10^((ln(x/(100−x)) − a) / b).</summary>
    public override IReadOnlyList<SecondaryValue> SecondaryCore(double[] p, double ceiling, VariantOptions opt)
        => SolveTargets(ceiling, x =>
        {
            if (Math.Abs(p[1]) < 1e-12) return double.NaN;
            double y = Math.Log(x / (ceiling - x));
            return Math.Pow(10, (y - p[0]) / p[1]);
        });
}

/// <summary>Gompertz: F = 100·e^(−a·e^(−b·log t)) (şartname §5.14). log = 10 tabanı.</summary>
public sealed class GompertzModel : DissolutionModelBase
{
    public override string Name => "Gompertz";
    public override string Equation => "F = 100*Exp(-a*Exp(-b*log t))";
    public override IReadOnlyList<string> ParamNames => new[] { "a", "b" };
    public override bool SupportsF0 => false;
    public override bool SupportsFmax => false;  // Fmax'lı hâli ayrı model

    public override double[] LowerBounds => new[] { 1e-12, double.NegativeInfinity };
    public override double[] UpperBounds => new[] { double.PositiveInfinity, double.PositiveInfinity };

    public override IReadOnlyList<Coefficient> Describe(double[] p) => new[]
    {
        new Coefficient("a", p[0], "-", CoreText.T("Ölçek parametresi")),
        new Coefficient("b", p[1], "-", CoreText.T("Şekil parametresi"))
    };

    /// <summary>Tohum: y = ln(−ln(F/100)), x = log10 t → b = −eğim, a = e^(kesişim).</summary>
    public override double[] InitialGuess(IReadOnlyList<double> t, IReadOnlyList<double> f, VariantOptions opt)
    {
        var (xs, ys) = SeedPoints(t, f,
            Math.Log10,
            fi => Math.Log(-Math.Log(fi / DefaultCeiling)),
            (ti, fi) => ti > 0 && fi > 0 && fi < DefaultCeiling);

        if (xs.Count < 2) return new[] { 3.0, 2.5 };
        var (intercept, slope) = SeedRegression.WithIntercept(xs, ys);
        double a = Math.Exp(intercept);
        double b = -slope;
        return new[] { IsFinite(a) && a > 0 ? a : 3.0, IsFinite(b) ? b : 2.5 };
    }

    public override double EvaluateCore(double t, double[] p, double ceiling, VariantOptions opt)
    {
        if (t <= 0) return 0;
        double z = -p[1] * Math.Log10(t);
        return ceiling * Math.Exp(-p[0] * Math.Exp(z));
    }

    /// <summary>Tx = 10^((ln(−ln(x/100)) − ln a) / (−b)).</summary>
    public override IReadOnlyList<SecondaryValue> SecondaryCore(double[] p, double ceiling, VariantOptions opt)
        => SolveTargets(ceiling, x =>
        {
            double a = p[0], b = p[1];
            if (a <= 0 || Math.Abs(b) < 1e-12) return double.NaN;
            double y = Math.Log(-Math.Log(x / ceiling));
            return Math.Pow(10, (y - Math.Log(a)) / -b);
        });
}

/// <summary>Probit: F = 100·Φ(a + b·log t) (şartname §5.15). Φ = standart normal CDF.</summary>
public sealed class ProbitModel : DissolutionModelBase
{
    public override string Name => "Probit";
    public override string Equation => "F = 100*NORMSDIST(a+b*log t)";
    public override IReadOnlyList<string> ParamNames => new[] { "a", "b" };
    public override bool SupportsF0 => false;
    public override bool SupportsFmax => false;

    public override double[] LowerBounds => new[] { double.NegativeInfinity, double.NegativeInfinity };
    public override double[] UpperBounds => new[] { double.PositiveInfinity, double.PositiveInfinity };

    public override IReadOnlyList<Coefficient> Describe(double[] p) => new[]
    {
        new Coefficient("a", p[0], "-", CoreText.T("Konum parametresi (kesişim)")),
        new Coefficient("b", p[1], "-", CoreText.T("Şekil parametresi (eğim)"))
    };

    /// <summary>Tohum: y = Φ⁻¹(F/100), x = log10 t, kesişimli → a=kesişim, b=eğim.</summary>
    public override double[] InitialGuess(IReadOnlyList<double> t, IReadOnlyList<double> f, VariantOptions opt)
    {
        var (xs, ys) = SeedPoints(t, f,
            Math.Log10,
            fi => Normal.InvCDF(0, 1, fi / DefaultCeiling),
            (ti, fi) => ti > 0 && fi > 0 && fi < DefaultCeiling);

        if (xs.Count < 2) return new[] { -1.4, 2.0 };
        var (a, b) = SeedRegression.WithIntercept(xs, ys);
        return new[] { IsFinite(a) ? a : -1.4, IsFinite(b) ? b : 2.0 };
    }

    public override double EvaluateCore(double t, double[] p, double ceiling, VariantOptions opt)
    {
        if (t <= 0) return 0;
        return ceiling * Normal.CDF(0, 1, p[0] + p[1] * Math.Log10(t));
    }

    /// <summary>Tx = 10^((Φ⁻¹(x/100) − a) / b).</summary>
    public override IReadOnlyList<SecondaryValue> SecondaryCore(double[] p, double ceiling, VariantOptions opt)
        => SolveTargets(ceiling, x =>
        {
            if (Math.Abs(p[1]) < 1e-12) return double.NaN;
            double y = Normal.InvCDF(0, 1, x / ceiling);
            return Math.Pow(10, (y - p[0]) / p[1]);
        });
}
