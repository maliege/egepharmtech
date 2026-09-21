using System.Text.Json;
using PTCalc.Core.Statistics;

namespace PTCalc.Core.Tests;

/// <summary>
/// ANOVA, Tukey HSD, çoklu regresyon ve kalibrasyon eğrisi sonuçlarının SciPy 1.17 / statsmodels 0.14
/// çıktılarıyla karşılaştırması (stats_reference.json; üretim betiği referansın kendisidir:
/// f_oneway, levene(center='mean'), pairwise_tukeyhsd, studentized_range, OLS + get_prediction, linregress).
/// </summary>
public class StatisticsReferenceTests
{
    private static readonly JsonElement Root =
        JsonDocument.Parse(File.ReadAllText("stats_reference.json")).RootElement;

    private static double D(JsonElement e, string name) => e.GetProperty(name).GetDouble();
    private static double[] Arr(JsonElement e, string name) => e.GetProperty(name).EnumerateArray().Select(x => x.GetDouble()).ToArray();

    private static void Close(double expected, double actual, double rtol, double atol = 0, string? what = null)
        => Assert.True(Math.Abs(expected - actual) <= atol + rtol * Math.Abs(expected),
            $"{what}: beklenen {expected:G10}, bulunan {actual:G10}");

    // ---------- Studentized range ----------

    [Fact]
    public void StudentizedRange_cdf_matches_scipy()
    {
        foreach (var row in Root.GetProperty("q").GetProperty("cdf").EnumerateArray())
        {
            var v = row.EnumerateArray().Select(x => x.GetDouble()).ToArray();
            Close(v[3], StudentizedRange.Cdf(v[0], (int)v[1], v[2]), 0, 2e-6, $"cdf(q={v[0]}, k={v[1]}, ν={v[2]})");
        }
    }

    [Fact]
    public void StudentizedRange_critical_values_match_scipy()
    {
        foreach (var row in Root.GetProperty("q").GetProperty("ppf").EnumerateArray())
        {
            var v = row.EnumerateArray().Select(x => x.GetDouble()).ToArray();
            Close(v[2], StudentizedRange.InvCdf(0.95, (int)v[0], v[1]), 1e-5, 0, $"q0.05(k={v[0]}, ν={v[1]})");
        }
    }

    // ---------- ANOVA ----------

    private static (OneWayAnovaResult Result, JsonElement Ref) Anova()
    {
        var a = Root.GetProperty("anova");
        var groups = a.GetProperty("groups").EnumerateArray()
            .Select(g => (IReadOnlyList<double>)g.EnumerateArray().Select(x => x.GetDouble()).ToArray()).ToList();
        return (OneWayAnova.Analyze(groups, new[] { "F1", "F2", "F3" }), a);
    }

    [Fact]
    public void Anova_table_matches_scipy()
    {
        var (r, a) = Anova();
        Close(D(a, "ssb"), r.SsBetween, 1e-10, what: "SSb");
        Close(D(a, "ssw"), r.SsWithin, 1e-10, what: "SSw");
        Assert.Equal((int)D(a, "dfb"), r.DfBetween);
        Assert.Equal((int)D(a, "dfw"), r.DfWithin);
        Close(D(a, "F"), r.F, 1e-10, what: "F");
        Close(D(a, "p"), r.P, 1e-6, 1e-12, "p");
        Close(D(a, "eta2"), r.Eta2, 1e-10, what: "eta²");
        Close(D(a, "omega2"), r.Omega2, 1e-10, what: "omega²");
        Assert.True(r.Significant);
    }

    [Fact]
    public void Levene_mean_centered_matches_scipy()
    {
        var (r, a) = Anova();
        var lev = a.GetProperty("levene_mean");
        Close(D(lev, "F"), r.LeveneF, 1e-9, what: "Levene F");
        Close(D(lev, "p"), r.LeveneP, 1e-7, what: "Levene p");
    }

    [Fact]
    public void Tukey_pairs_match_statsmodels()
    {
        var (r, a) = Anova();
        Close(D(a, "q_crit_05"), r.QCritical, 1e-5, what: "q_crit");
        var refPairs = a.GetProperty("tukey").EnumerateArray().ToList();
        Assert.Equal(refPairs.Count, r.Tukey.Count);
        for (int i = 0; i < refPairs.Count; i++)
        {
            var e = refPairs[i]; var t = r.Tukey[i];
            // statsmodels farkı (B − A) olarak verir, biz (A − B); işaret ve aralık ters çevrilir
            Close(-D(e, "diff"), t.Diff, 0, 1e-4, $"diff {t.A}-{t.B}");
            Close(-D(e, "hi"), t.Lower, 0, 1e-4, $"lower {t.A}-{t.B}");
            Close(-D(e, "lo"), t.Upper, 0, 1e-4, $"upper {t.A}-{t.B}");
            Close(D(e, "p"), t.P, 0, 1e-4, $"p {t.A}-{t.B}");   // statsmodels p'yi 4 basamağa yuvarlar
            Assert.Equal(e.GetProperty("reject").GetBoolean(), t.Significant);
        }
    }

    [Fact]
    public void Anova_rejects_fewer_than_two_usable_groups()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            OneWayAnova.Analyze(new IReadOnlyList<double>[] { new[] { 1.0, 2.0 }, new[] { 5.0 } }));
        Assert.Contains("en az iki gruba", ex.Message, StringComparison.Ordinal);
    }

    // ---------- Çoklu regresyon ----------

    private static (MultipleRegressionResult Result, JsonElement Ref) Regression()
    {
        var g = Root.GetProperty("regression");
        var x1 = Arr(g, "X1"); var x2 = Arr(g, "X2"); var y = Arr(g, "Y");
        var x = x1.Select((v, i) => new[] { v, x2[i] }).ToList();
        return (MultipleRegression.Fit(x, y, new[] { "Basınç", "Bağlayıcı" }), g);
    }

    [Fact]
    public void Regression_coefficients_and_inference_match_statsmodels()
    {
        var (r, g) = Regression();
        var coef = Arr(g, "coef"); var se = Arr(g, "se"); var t = Arr(g, "t"); var p = Arr(g, "p");
        var ci = g.GetProperty("ci").EnumerateArray().Select(row => row.EnumerateArray().Select(x => x.GetDouble()).ToArray()).ToArray();
        Assert.Equal(3, r.Coefficients.Count);
        for (int j = 0; j < 3; j++)
        {
            Close(coef[j], r.Coefficients[j].Estimate, 1e-8, 1e-12, $"b{j}");
            Close(se[j], r.Coefficients[j].Se, 1e-8, what: $"se{j}");
            Close(t[j], r.Coefficients[j].T, 1e-8, what: $"t{j}");
            Close(p[j], r.Coefficients[j].P, 1e-6, 1e-12, $"p{j}");
            Close(ci[j][0], r.Coefficients[j].Lower, 1e-8, 1e-12, $"ci lo {j}");
            Close(ci[j][1], r.Coefficients[j].Upper, 1e-8, 1e-12, $"ci hi {j}");
        }
        Assert.Equal("(Sabit)", r.Coefficients[0].Name);
        Assert.Equal("Basınç", r.Coefficients[1].Name);
    }

    [Fact]
    public void Regression_anova_and_fit_statistics_match_statsmodels()
    {
        var (r, g) = Regression();
        Close(D(g, "r2"), r.R2, 1e-10, what: "R²");
        Close(D(g, "r2adj"), r.R2Adj, 1e-10, what: "R²adj");
        Close(D(g, "F"), r.F, 1e-8, what: "F");
        Close(D(g, "Fp"), r.FP, 1e-6, 1e-15, "F p");
        Close(D(g, "ss_reg"), r.SsRegression, 1e-8, what: "SSreg");
        Close(D(g, "ss_res"), r.SsResidual, 1e-8, what: "SSres");
        Close(D(g, "ss_tot"), r.SsTotal, 1e-10, what: "SStot");
        Assert.Equal((int)D(g, "df_reg"), r.DfRegression);
        Assert.Equal((int)D(g, "df_res"), r.DfResidual);
        Close(D(g, "rmse"), r.Rmse, 1e-8, what: "RMSE");
        Close(D(g, "dw"), r.DurbinWatson, 1e-8, what: "DW");
        var resid = Arr(g, "resid");
        for (int i = 0; i < resid.Length; i++) Close(resid[i], r.Residuals[i], 1e-8, 1e-12, $"resid {i}");
    }

    [Fact]
    public void Regression_prediction_intervals_match_statsmodels()
    {
        var (r, g) = Regression();
        var pr = g.GetProperty("pred");
        var pred = r.Predict(Arr(pr, "x"));
        Close(D(pr, "mean"), pred.Fit, 1e-8, what: "fit");
        var ci = Arr(pr, "ci"); var pi = Arr(pr, "pi");
        Close(ci[0], pred.CiLower, 1e-8, what: "ci lo");
        Close(ci[1], pred.CiUpper, 1e-8, what: "ci hi");
        Close(pi[0], pred.PiLower, 1e-8, what: "pi lo");
        Close(pi[1], pred.PiUpper, 1e-8, what: "pi hi");
    }

    [Fact]
    public void Regression_detects_singular_design()
    {
        var x = Enumerable.Range(0, 8).Select(i => new[] { (double)i, 2.0 * i }).ToList();
        var y = x.Select(r => r[0] + 1).ToList();
        var ex = Assert.Throws<ArgumentException>(() => MultipleRegression.Fit(x, y));
        Assert.Contains("tekil", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Regression_vif_is_one_for_orthogonal_predictors()
    {
        var (r, _) = Regression();   // X1 ve X2 tasarımı dik: VIF = 1
        Assert.All(r.Vif, v => Close(1.0, v, 1e-9, what: "VIF"));
    }

    // ---------- Kalibrasyon ----------

    private static (CalibrationResult Result, JsonElement Ref) Calibration()
    {
        var c = Root.GetProperty("calibration");
        return (CalibrationCurve.Fit(Arr(c, "conc"), Arr(c, "resp")), c);
    }

    [Fact]
    public void Calibration_line_matches_scipy_linregress()
    {
        var (r, c) = Calibration();
        Close(D(c, "slope"), r.Slope, 1e-10, what: "slope");
        Close(D(c, "intercept"), r.Intercept, 1e-8, 1e-15, "intercept");
        Close(D(c, "se_slope"), r.SeSlope, 1e-8, what: "se slope");
        Close(D(c, "se_intercept"), r.SeIntercept, 1e-8, what: "se intercept");
        Close(D(c, "r"), r.R, 1e-10, what: "r");
        Close(D(c, "r2"), r.R2, 1e-10, what: "r²");
        Close(D(c, "syx"), r.Syx, 1e-8, what: "s_y/x");
        Close(D(c, "t_crit"), r.TCrit, 1e-8, what: "t crit");
        var cs = Arr(c, "ci_slope"); var ci = Arr(c, "ci_intercept");
        Close(cs[0], r.SlopeLower, 1e-8, what: "slope lo"); Close(cs[1], r.SlopeUpper, 1e-8, what: "slope hi");
        Close(ci[0], r.InterceptLower, 1e-8, 1e-15, "int lo"); Close(ci[1], r.InterceptUpper, 1e-8, 1e-15, "int hi");
    }

    [Fact]
    public void Calibration_lod_loq_and_back_calculation()
    {
        var (r, c) = Calibration();
        Close(D(c, "lod"), r.Lod, 1e-8, what: "LOD");
        Close(D(c, "loq"), r.Loq, 1e-8, what: "LOQ");
        var back = Arr(c, "back_calc"); var acc = Arr(c, "accuracy_pct"); var rp = Arr(c, "resid_pct");
        for (int i = 0; i < back.Length; i++)
        {
            Close(back[i], r.Points[i].BackCalc, 1e-8, what: $"back {i}");
            Close(acc[i], r.Points[i].AccuracyPct, 1e-8, what: $"acc {i}");
            Close(rp[i], r.Points[i].ResidualPct, 1e-8, what: $"resid% {i}");
        }
        Assert.Equal(5, r.Levels);
        Assert.Equal(15, r.N);
        Assert.All(r.LevelSummary, l => Assert.Equal(3, l.N));
    }

    [Fact]
    public void Calibration_inverse_prediction_matches_Miller_formula()
    {
        var (r, c) = Calibration();
        var u = c.GetProperty("unknown");
        var est = r.Estimate(D(u, "y0"));
        Close(D(u, "x0"), est.Conc, 1e-10, what: "x0");
        Close(D(u, "sx0"), est.Se, 1e-8, what: "s_x0");
        Assert.False(est.OutsideRange);
        var u3 = c.GetProperty("unknown_m3");
        Close(D(u3, "sx0"), r.Estimate(D(u3, "y0"), 3).Se, 1e-8, what: "s_x0 (m=3)");
    }

    [Fact]
    public void Calibration_flags_intercept_and_level_count()
    {
        // Üç düzey, belirgin kesim: iki uyarı da beklenir
        var conc = new double[] { 1, 1, 5, 5, 10, 10 };
        var resp = new double[] { 0.30, 0.31, 0.70, 0.69, 1.20, 1.21 };
        var r = CalibrationCurve.Fit(conc, resp);
        Assert.Contains(r.Flags, f => f.Contains("Kesim noktası"));
        Assert.Contains(r.Flags, f => f.Contains("beş konsantrasyon"));
        Assert.Throws<ArgumentException>(() => CalibrationCurve.Fit(new double[] { 1, 1, 2, 2 }, new double[] { 1, 1, 2, 2 }));
    }

    // ---------- Tablo okuyucu ----------

    [Fact]
    public void ReadCompleteRows_skips_partial_rows_and_reports_them()
    {
        object?[]?[] table =
        [
            [1.0, 2.0, 3.0],
            [null, null, null],
            ["4,5", "6", ""],          // kısmi → atlanır, 3. satır
            ["7", "8", "9"],
            ["x", "1", "2"],           // metin → atlanır, 5. satır
        ];
        var res = StatisticsInputReader.ReadCompleteRows(table, [0, 1, 2]);
        Assert.Equal(2, res.Rows.Count);
        Assert.Equal([7, 8, 9], res.Rows[1]);
        Assert.Equal([3, 5], res.SkippedRowNumbers);
    }

    [Fact]
    public void ReadColumns_compresses_each_column_independently()
    {
        object?[]?[] table = [[1.0, null], [2.0, "3"], [null, "4"], ["", null]];
        var cols = StatisticsInputReader.ReadColumns(table, 3);
        Assert.Equal([1.0, 2.0], cols[0]);
        Assert.Equal([3.0, 4.0], cols[1]);
        Assert.Empty(cols[2]);
    }
}
