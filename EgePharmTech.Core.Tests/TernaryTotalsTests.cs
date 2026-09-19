using EgePharmTech.Models;
using EgePharmTech.Services;

namespace EgePharmTech.Core.Tests;

/// <summary>Üçgen diyagram satır toplamı denetimi (yağ + sürfaktan + su = 100).</summary>
public class TernaryTotalsTests
{
    private static Ttridata Row(int g, int s, double o, double sc, double w)
        => new() { grup = g, siraNo = s, oil = o, surCoSur = sc, water = w };

    [Fact]
    public void Rows_summing_to_100_within_tolerance_pass()
    {
        var rows = new[]
        {
            Row(1, 1, 8.14, 72.67, 19.19),   // tam 100
            Row(1, 2, 16.87, 65.43, 17.70),  // 100,00
            Row(1, 3, 25.0, 57.1, 18.2),     // 100,3 -> tolerans içi
            Row(1, 4, 40.0, 40.0, 19.6),     // 99,6 -> tolerans içi
        };
        Assert.Empty(TernaryTotals.Check(rows));
    }

    [Fact]
    public void Deviating_rows_are_reported_in_input_order_with_signed_delta()
    {
        var rows = new[]
        {
            Row(1, 1, 10, 60, 30),
            Row(1, 2, 10, 60, 25),   // 95 -> -5
            Row(2, 1, 20, 50, 30),
            Row(2, 2, 20, 50, 40),   // 110 -> +10
        };
        var dev = TernaryTotals.Check(rows);
        Assert.Equal(2, dev.Count);
        Assert.Equal((1, 2), (dev[0].Group, dev[0].Order));
        Assert.Equal(-5, dev[0].Delta, 9);
        Assert.Equal((2, 2), (dev[1].Group, dev[1].Order));
        Assert.Equal(10, dev[1].Delta, 9);
        Assert.Equal(110, dev[1].Total, 9);
    }

    [Fact]
    public void Tolerance_is_inclusive_and_configurable()
    {
        var rows = new[] { Row(1, 1, 10, 60, 30.5) };           // 100,5
        Assert.Empty(TernaryTotals.Check(rows));                 // 0,5 sınırda: uyarı yok
        Assert.Single(TernaryTotals.Check(rows, 0.1));
        Assert.Empty(TernaryTotals.Check(rows, 1.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => TernaryTotals.Check(rows, -1));
    }

    [Fact]
    public void NaN_totals_are_reported()
    {
        var rows = new[] { Row(1, 1, double.NaN, 60, 30) };
        Assert.Single(TernaryTotals.Check(rows));
    }
}
