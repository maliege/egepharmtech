using System.Text.Json;
using EgePharmTech.Core.Dissolution;

namespace EgePharmTech.Core.Tests;

/// <summary>Geçici araç: rehberdeki işlenmiş örnek için motor çıktısını JSON'a döker (EGEPHARMTECH_WORKED_OUT verilirse).</summary>
public class WorkedExampleDump
{
    [Fact]
    public void Dump_when_requested()
    {
        var outPath = Environment.GetEnvironmentVariable("EGEPHARMTECH_WORKED_OUT");
        if (string.IsNullOrEmpty(outPath)) return;

        double[] t = [1, 2, 3, 4, 6, 8, 10, 12, 14, 16, 18, 20];
        double[] f = [8, 24, 38, 48, 58, 66, 73, 78, 84, 88, 92, 95];

        var r = KineticEngine.FitAll(t, f);
        object Map(ModelFit m) => new
        {
            m.ModelName, m.Equation, m.Converged,
            Parameters = m.Parameters.Select(p => new { p.Symbol, p.Value, p.Unit }),
            Secondary = m.Secondary.Select(s => new { s.Symbol, s.Value }),
            Gof = new { m.Gof.N, m.Gof.Dof, m.Gof.Rsqr, m.Gof.RsqrAdj, m.Gof.Mse, m.Gof.SS, m.Gof.Aic, m.Gof.AicC, m.Gof.Msc },
            Weight = r.AkaikeWeights.TryGetValue(m.ModelName, out var w) ? w : (double?)null,
            m.Flags,
            Residuals = t.Select((ti, i) => Math.Round(f[i] - m.Predict(ti), 2)).ToArray()
        };
        var dump = new
        {
            Fits = r.Fits.Select(Map),
            Mechanism = r.MechanismFits.Select(Map),
            Profile = r.Profile,
            KpAll = Map(KineticEngine.Fit("Korsmeyer-Peppas", t, f, VariantOptions.Default with { KpUseAllPoints = true })),
            WeibullFmax = Map(KineticEngine.Fit("Weibull", t, f, VariantOptions.Default with { UseFmax = true })),
            FirstOrderFmax = Map(KineticEngine.Fit("First-order", t, f, VariantOptions.Default with { UseFmax = true })),
        };
        File.WriteAllText(outPath, JsonSerializer.Serialize(dump, new JsonSerializerOptions { WriteIndented = true }));
    }
}
