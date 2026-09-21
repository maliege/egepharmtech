using PTCalc.Core.Dissolution;

namespace PTCalc.Core.Tests;

/// <summary>
/// Kinetik veri giriş tablosunun (t, F) profiline çevrilmesi. Eski sayfa kodunun sessizce
/// yaptığı iki şey — yalnız Ortalama sütunu dolu satırları yok sayma ve %100'e ulaşan
/// noktaları atma — burada açıkça sınanır.
/// </summary>
public class KineticInputReaderTests
{
    /// <summary>9 sütunlu satır: t, r1..r6, mean, std.</summary>
    private static object?[] Row(object? t, object? r1 = null, object? r2 = null, object? r3 = null,
                                  object? mean = null)
        => [t, r1, r2, r3, null, null, null, mean, null];

    [Fact]
    public void Replicates_are_averaged()
    {
        var p = KineticInputReader.Read([Row(1.0, 10.0, 12.0, 14.0), Row(2.0, 20.0, 22.0), Row(4.0, 40.0)]);

        Assert.Equal([1.0, 2.0, 4.0], p.T);
        Assert.Equal([12.0, 21.0, 40.0], p.F);
        Assert.Empty(p.Warnings);
    }

    /// <summary>Kullanıcının elinde yalnız ortalamalar varsa Ortalama sütunu yeterli olmalı.</summary>
    [Fact]
    public void Mean_column_is_used_when_no_replicates_are_given()
    {
        var p = KineticInputReader.Read([Row(1.0, mean: 12.0), Row(2.0, mean: 21.0), Row(4.0, mean: 40.0)]);

        Assert.Equal([12.0, 21.0, 40.0], p.F);
        Assert.Contains(p.Warnings, w => w.Contains("Ortalama"));
    }

    [Fact]
    public void Replicates_win_over_a_stale_mean_column()
    {
        // Kullanıcı tekrarları değiştirmiş ama eski ortalama hücrede kalmış olabilir
        var p = KineticInputReader.Read([Row(1.0, 10.0, 20.0, mean: 99.0), Row(2.0, 30.0, 40.0, mean: 99.0)]);
        Assert.Equal([15.0, 35.0], p.F);
    }

    /// <summary>Eski kod %100 ve üstünü atıyordu; profilin sonu kayboluyordu.</summary>
    [Fact]
    public void Points_at_or_above_100_percent_are_kept_with_a_warning()
    {
        var p = KineticInputReader.Read(
        [
            Row(1.0, 30.0), Row(2.0, 55.0), Row(4.0, 85.0), Row(6.0, 97.0),
            Row(8.0, 100.0), Row(12.0, 100.4), Row(16.0, 99.8)
        ]);

        Assert.Equal(7, p.T.Length);
        Assert.Equal(100.4, p.F[5]);
        Assert.Contains(p.Warnings, w => w.Contains("%100'ün üzerinde"));
    }

    [Fact]
    public void Exactly_100_does_not_trigger_the_above_100_warning()
    {
        var p = KineticInputReader.Read([Row(1.0, 50.0), Row(2.0, 80.0), Row(4.0, 100.0)]);
        Assert.Equal(3, p.T.Length);
        Assert.DoesNotContain(p.Warnings, w => w.Contains("%100'ün üzerinde"));
    }

    [Fact]
    public void Blank_rows_are_skipped_silently()
    {
        var p = KineticInputReader.Read([Row(1.0, 10.0), Row(null), Row("", ""), null!, Row(2.0, 20.0), Row(4.0, 40.0)]);
        Assert.Equal(3, p.T.Length);
        Assert.Empty(p.Warnings);
    }

    [Fact]
    public void Rows_with_non_positive_time_are_skipped_with_a_warning()
    {
        var p = KineticInputReader.Read([Row(0.0, 0.0), Row(-1.0, 5.0), Row(1.0, 10.0), Row(2.0, 20.0)]);
        Assert.Equal([1.0, 2.0], p.T);
        Assert.Contains(p.Warnings, w => w.Contains("zaman ≤ 0"));
    }

    [Fact]
    public void Rows_with_time_but_no_value_are_reported()
    {
        var p = KineticInputReader.Read([Row(1.0, 10.0), Row(2.0), Row(3.0, 30.0)]);
        Assert.Equal([1.0, 3.0], p.T);
        Assert.Contains(p.Warnings, w => w.Contains("ne tekrar ne ortalama"));
    }

    [Fact]
    public void Empty_table_returns_empty_profile_with_an_explanation()
    {
        var p = KineticInputReader.Read([Row(null), Row(null)]);
        Assert.Empty(p.T);
        Assert.Contains(p.Warnings, w => w.Contains("Geçerli veri noktası bulunamadı"));
    }

    [Fact]
    public void Output_is_sorted_by_time()
    {
        var p = KineticInputReader.Read([Row(4.0, 40.0), Row(1.0, 10.0), Row(2.0, 20.0)]);
        Assert.Equal([1.0, 2.0, 4.0], p.T);
        Assert.Equal([10.0, 20.0, 40.0], p.F);
    }

    /// <summary>Kalan formatı zamana göre ilk/son noktayla tanınmalı, girdi sırasıyla değil.</summary>
    [Fact]
    public void Retained_format_is_detected_by_time_order_and_inverted()
    {
        // Karışık girilmiş "kalan" verisi: t=1 → %90 kaldı, t=8 → %20 kaldı
        var p = KineticInputReader.Read([Row(8.0, 20.0), Row(1.0, 90.0), Row(4.0, 50.0)]);

        Assert.True(p.ConvertedFromRetained);
        Assert.Equal([1.0, 4.0, 8.0], p.T);
        Assert.Equal([10.0, 50.0, 80.0], p.F);
        Assert.Contains(p.Warnings, w => w.Contains("kalan"));
    }

    [Fact]
    public void Increasing_profile_is_not_inverted_even_if_entered_backwards()
    {
        var p = KineticInputReader.Read([Row(8.0, 80.0), Row(4.0, 50.0), Row(1.0, 10.0)]);
        Assert.False(p.ConvertedFromRetained);
        Assert.Equal([10.0, 50.0, 80.0], p.F);
    }

    [Fact]
    public void Fraction_scale_data_is_flagged()
    {
        var p = KineticInputReader.Read([Row(1.0, 0.08), Row(2.0, 0.24), Row(4.0, 0.48), Row(8.0, 0.66)]);
        Assert.Equal(4, p.T.Length);
        Assert.Contains(p.Warnings, w => w.Contains("kesir"));
    }

    [Fact]
    public void Too_few_points_are_flagged()
    {
        var p = KineticInputReader.Read([Row(1.0, 10.0), Row(2.0, 20.0)]);
        Assert.Equal(2, p.T.Length);
        Assert.Contains(p.Warnings, w => w.Contains("Yalnız 2 nokta"));
    }

    [Fact]
    public void Text_cells_are_parsed_culture_independently()
    {
        var p = KineticInputReader.Read([Row("0,5", "12,5", "13.5"), Row("1", "1.234,5")]);
        Assert.Equal([0.5, 1.0], p.T);
        Assert.Equal(13.0, p.F[0]);
        Assert.Equal(1234.5, p.F[1]);
    }

    [Fact]
    public void ReplicateStats_skips_rows_without_replicates_so_manual_means_survive()
    {
        var stats = KineticInputReader.ReplicateStats(
        [
            Row(1.0, 10.0, 12.0, 14.0),
            Row(2.0, mean: 21.0),          // yalnız ortalama → geri yazılmamalı
            Row(3.0, 30.0)                 // tek tekrar → std 0
        ]);

        Assert.Equal(2, stats.Count);
        Assert.Equal((0, 12.0, 2.0), stats[0]);
        Assert.Equal((2, 30.0, 0.0), stats[1]);
    }
}
