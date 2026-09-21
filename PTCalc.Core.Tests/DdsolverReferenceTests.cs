using System.Text.Json;
using PTCalc.Core.Dissolution;
using PTCalc.Core.Dissolution.Models;

namespace PTCalc.Core.Tests;

/// <summary>
/// Gerçek DDSolver 1.0 çıktılarıyla (ddsolver_reference_cases.json; 32 sayfa, 37 formülasyon) doğrulama.
///
/// DDSolver'ın çözücüsü Nelder-Mead'dir (AMOEBA) ve sık sık yerel/erken bir noktada durur; bu yüzden
/// parametrelerin birebir eşitliği beklenmez. Bunun yerine üç şey kanıtlanır:
///  1. DDSolver'ın bulduğu parametreler bizim denklemimize konduğunda DDSolver'ın SS'i ve tüm GoF
///     ölçütleri (R, R², R²adj, MSE, AIC, MSC, DF) birebir çıkar → denklem, nokta kuralları ve GoF
///     tanımları özdeş. (Tek istisna DDSolver'ın MSC'de F=0 noktalarını düşürmesi; testte açıklanır.)
///  2. İkincil parametreler (T25–T90, "Non Calc" dahil) DDSolver parametrelerinde birebir çıkar.
///  3. Motorumuzun bulduğu SS DDSolver'ınkinden asla yüksek değildir (37/37; 9'unda eşit, 28'inde düşük).
/// </summary>
public class DdsolverReferenceTests
{
    private static readonly JsonElement Root =
        JsonDocument.Parse(File.ReadAllText("ddsolver_reference_cases.json")).RootElement.GetProperty("cases");

    public static IEnumerable<object[]> Formulations()
    {
        int ci = 0;
        foreach (var c in Root.EnumerateArray())
        {
            int nform = c.GetProperty("F").GetArrayLength();
            for (int k = 0; k < nform; k++) yield return new object[] { ci, k };
            ci++;
        }
    }

    private sealed record Case(string Label, string BaseName, VariantOptions Opt, double[] T, double[] F,
        Dictionary<string, double?> Params, Dictionary<string, double?> Secondary, Dictionary<string, double?> Gof);

    private static double? Num(JsonElement e) => e.ValueKind == JsonValueKind.Number ? e.GetDouble() : null;

    private static Case Load(int ci, int k)
    {
        var c = Root[ci];
        string ddModel = c.GetProperty("model").GetString()!;
        var parts = ddModel.Split(" with ");
        string variant = parts.Length > 1 ? parts[1] : "";
        var opt = new VariantOptions
        {
            UseF0 = variant.Contains("F0"), UseTlag = variant.Contains("Tlag"), UseFmax = variant.Contains("Fmax"),
            KpUseAllPoints = true   // DDSolver, Korsmeyer-Peppas'ı tüm noktalara fit eder (F≤60 filtresi yok)
        };
        var t = c.GetProperty("t").EnumerateArray().Select(x => x.GetDouble()).ToArray();
        var fRaw = c.GetProperty("F")[k].EnumerateArray().Select(Num).ToArray();
        var idx = Enumerable.Range(0, t.Length).Where(i => fRaw[i] is not null).ToArray();
        Dictionary<string, double?> Dict(string prop) => c.GetProperty(prop).EnumerateArray()
            .ToDictionary(p => p.GetProperty("name").GetString()!, p => Num(p.GetProperty("values")[k]));
        var gof = c.GetProperty("gof").EnumerateObject().ToDictionary(p => p.Name, p => Num(p.Value[k]));
        return new Case($"{c.GetProperty("source").GetString()}#{k + 1} {ddModel}", parts[0].Trim(), opt,
            idx.Select(i => t[i]).ToArray(), idx.Select(i => fRaw[i]!.Value).ToArray(), Dict("params"), Dict("secondary"), gof);
    }

    private static (DissolutionModelBase Model, double[] P) ModelAtDdParams(Case c)
    {
        var model = ModelCatalog.Build(ModelCatalog.Find(c.BaseName)!, c.Opt);
        var p = model.ParamNames.Select(n => c.Params[n]!.Value).ToArray();
        return (model, p);
    }

    private static void AssertRel(double expected, double actual, double rtol, string what)
    {
        double tol = rtol * Math.Max(1e-12, Math.Abs(expected));
        Assert.True(Math.Abs(expected - actual) <= tol, $"{what}: beklenen {expected:G10}, bulunan {actual:G10}");
    }

    [Theory]
    [MemberData(nameof(Formulations))]
    public void DDSolver_parameters_reproduce_DDSolver_SS_and_GoF_exactly(int ci, int k)
    {
        var c = Load(ci, k);
        var (model, p) = ModelAtDdParams(c);
        var yHat = c.T.Select(ti => model.Evaluate(ti, p, c.Opt)).ToArray();
        var gof = GoodnessOfFitCalculator.Compute(c.F, yHat, p.Length);

        AssertRel(c.Gof["SS"]!.Value, gof.SS, 1e-7, c.Label + " SS");
        Assert.Equal((int)c.Gof["N_observed"]!.Value, gof.N);
        Assert.Equal((int)c.Gof["DF"]!.Value, gof.Dof);
        AssertRel(c.Gof["Rsqr"]!.Value, gof.Rsqr, 1e-7, c.Label + " Rsqr");
        AssertRel(c.Gof["Rsqr_adj"]!.Value, gof.RsqrAdj, 1e-7, c.Label + " Rsqr_adj");
        AssertRel(c.Gof["MSE"]!.Value, gof.Mse, 1e-7, c.Label + " MSE");
        AssertRel(c.Gof["AIC"]!.Value, gof.Aic, 1e-7, c.Label + " AIC");
        // DDSolver'ın MSC döngüsü (VBA: `If Cells(...) <> "" And Cells(...) <> 0`) F = 0 olan noktaları
        // SStot'tan düşürür; Rsqr döngüsü düşürmez. Bu DDSolver'ın kendi tutarsızlığıdır; biz her iki
        // ölçütte de tüm noktaları kullanırız. F = 0 içeren tek referans sette (Ornekler DDResult(9),
        // t=0 → F=0) fark tam olarak bu kadar çıkar; aşağıda DDSolver'ın hesabı yeniden üretilir.
        double expectedMsc = c.Gof["MSC"]!.Value;
        if (c.F.Any(v => v == 0))
        {
            double mean = c.F.Average();
            double ssTotNonZero = c.F.Where(v => v != 0).Sum(v => (v - mean) * (v - mean));
            double mscDdStyle = Math.Log(ssTotNonZero / gof.SS) - 2.0 * p.Length / c.F.Length;
            AssertRel(expectedMsc, mscDdStyle, 1e-7, c.Label + " MSC (DDSolver'ın F=0 düşürme kuralıyla)");
        }
        else
            AssertRel(expectedMsc, gof.Msc, 1e-7, c.Label + " MSC");
        AssertRel(c.Gof["R_obs-pre"]!.Value, gof.R, 1e-7, c.Label + " R");
    }

    [Theory]
    [MemberData(nameof(Formulations))]
    public void Secondary_parameters_match_DDSolver_at_its_parameters(int ci, int k)
    {
        var c = Load(ci, k);
        var (model, p) = ModelAtDdParams(c);
        foreach (var s in model.Secondary(p, c.Opt))
        {
            var dd = c.Secondary[s.Symbol];   // null ⇔ "Non Calc"
            if (dd is null) Assert.False(s.IsCalculable, $"{c.Label} {s.Symbol}: DDSolver 'Non Calc', biz {s.Value}");
            else
            {
                Assert.True(s.IsCalculable, $"{c.Label} {s.Symbol}: DDSolver {dd}, biz 'Non Calc'");
                AssertRel(dd.Value, s.Value!.Value, 1e-6, c.Label + " " + s.Symbol);
            }
        }
    }

    [Theory]
    [MemberData(nameof(Formulations))]
    public void Our_fit_is_never_worse_than_DDSolver(int ci, int k)
    {
        var c = Load(ci, k);
        var fit = KineticEngine.Fit(c.BaseName, c.T, c.F, c.Opt);
        double ddSS = c.Gof["SS"]!.Value;
        Assert.True(fit.Gof.SS <= ddSS * (1 + 1e-6) + 1e-9,
            $"{c.Label}: bizim SS {fit.Gof.SS:G8} > DDSolver SS {ddSS:G8}");
    }

    /// <summary>Katsayılarda doğrusal modellerde OLS tohumu global optimumdur; DDSolver da oraya varır → parametreler eşit.</summary>
    [Theory]
    [MemberData(nameof(Formulations))]
    public void Linear_in_coefficients_models_agree_on_parameters(int ci, int k)
    {
        var c = Load(ci, k);
        if (c.BaseName is not ("Zero-order" or "Higuchi")) return;
        var fit = KineticEngine.Fit(c.BaseName, c.T, c.F, c.Opt);
        foreach (var coef in fit.Parameters)
            AssertRel(c.Params[coef.Symbol]!.Value, coef.Value, 1e-4, c.Label + " " + coef.Symbol);
    }
}
