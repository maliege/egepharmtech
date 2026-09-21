using PTCalc.Core.Dissolution;

namespace PTCalc.Core.Tests;

/// <summary>
/// f1/f2 ve yardımcıları. Değerler Moore &amp; Flanner (1996) tanımından elle hesaplandı;
/// f2'nin "her noktada 10 birim fark ≈ 50" çapası kılavuzlardaki benzerlik eşiğinin kaynağıdır.
/// </summary>
public class SimilarityFactorsTests
{
    [Fact]
    public void Identical_profiles_give_f1_zero_and_f2_hundred()
    {
        double[] r = { 20, 45, 70, 85 };
        var (f1, sumR, sumAbs) = SimilarityFactors.F1(r, r);

        Assert.Equal(0, f1, 12);
        Assert.Equal(220, sumR, 12);
        Assert.Equal(0, sumAbs, 12);
        Assert.Equal(100, SimilarityFactors.F2(r, r), 12);
    }

    /// <summary>Her noktada tam 10 birim fark: f2 = 50·log10(100/√101) = 49.894…</summary>
    [Fact]
    public void Constant_ten_point_difference_gives_f2_just_under_fifty()
    {
        double[] r = { 30, 50, 70, 85 };
        double[] p = { 20, 40, 60, 75 };

        double f2 = SimilarityFactors.F2(r, p);
        Assert.Equal(50 * Math.Log10(100 / Math.Sqrt(101)), f2, 10);
        Assert.InRange(f2, 49.8, 49.9);
    }

    [Fact]
    public void F1_is_sum_of_absolute_differences_over_sum_of_reference()
    {
        double[] r = { 40, 60, 80 };   // ΣR = 180
        double[] p = { 45, 54, 80 };   // |d| = 5, 6, 0 → 11

        var (f1, sumR, sumAbs) = SimilarityFactors.F1(r, p);
        Assert.Equal(180, sumR, 12);
        Assert.Equal(11, sumAbs, 12);
        Assert.Equal(100.0 * 11 / 180, f1, 12);
    }

    [Fact]
    public void F2_is_symmetric_but_f1_is_not()
    {
        double[] r = { 40, 60, 80 };
        double[] p = { 45, 54, 80 };

        Assert.Equal(SimilarityFactors.F2(r, p), SimilarityFactors.F2(p, r), 12);
        Assert.NotEqual(SimilarityFactors.F1(r, p).F1, SimilarityFactors.F1(p, r).F1);
    }

    [Fact]
    public void Mismatched_lengths_and_empty_series_are_rejected()
    {
        Assert.Throws<ArgumentException>(() => SimilarityFactors.F2([1, 2], [1]));
        Assert.Throws<ArgumentException>(() => SimilarityFactors.F1([], []));
        Assert.Throws<ArgumentException>(() => SimilarityFactors.F1([0, 0], [1, 2]));   // ΣR = 0
    }

    [Fact]
    public void Alignment_keeps_only_common_timepoints_in_time_order()
    {
        var reference = new List<(double t, double yMean)> { (4, 70), (1, 20), (2, 45), (8, 90) };
        var test = new List<(double t, double yMean)> { (2, 40), (1, 18), (4, 65), (6, 80) };

        var (t, r, p) = SimilarityFactors.AlignByExactTimepoints(reference, test);

        Assert.Equal([1.0, 2.0, 4.0], t);
        Assert.Equal([20.0, 45.0, 70.0], r);
        Assert.Equal([18.0, 40.0, 65.0], p);
    }

    [Fact]
    public void Alignment_uses_the_last_entry_for_a_repeated_time_and_returns_empty_when_disjoint()
    {
        var reference = new List<(double t, double yMean)> { (1, 20), (1, 25) };
        var test = new List<(double t, double yMean)> { (1, 18) };
        var (_, r, _) = SimilarityFactors.AlignByExactTimepoints(reference, test);
        Assert.Equal([25.0], r);

        var (t2, _, _) = SimilarityFactors.AlignByExactTimepoints(
            [(1, 20)], [(2, 20)]);
        Assert.Empty(t2);
    }

    /// <summary>Kılavuz: referans YA DA test %85'e ulaştığı ilk nokta dahil edilir.</summary>
    [Theory]
    [InlineData(new double[] { 30, 60, 84, 92 }, new double[] { 28, 55, 80, 90 }, 3)]  // ikisi de 4. noktada
    [InlineData(new double[] { 30, 60, 84, 92 }, new double[] { 40, 86, 95, 98 }, 1)]  // test 2. noktada
    [InlineData(new double[] { 30, 60, 70, 80 }, new double[] { 28, 55, 65, 75 }, 3)]  // hiçbiri → son indeks
    public void Cutoff_index_is_first_point_where_either_series_reaches_threshold(double[] r, double[] p, int expected)
        => Assert.Equal(expected, SimilarityFactors.FindCutoffIndex(r, p));

    [Fact]
    public void TakeThrough_includes_the_cutoff_point_and_clamps_the_index()
    {
        double[] t = { 1, 2, 4, 6 }, r = { 30, 60, 86, 92 }, p = { 28, 55, 80, 90 };

        var (t1, r1, p1) = SimilarityFactors.TakeThrough(t, r, p, 2);
        Assert.Equal([1.0, 2.0, 4.0], t1);
        Assert.Equal(3, r1.Length);
        Assert.Equal(3, p1.Length);

        var (t2, _, _) = SimilarityFactors.TakeThrough(t, r, p, 99);
        Assert.Equal(4, t2.Length);
    }

    [Fact]
    public void MeanStdDev_uses_sample_standard_deviation()
    {
        var (mean, std) = SimilarityFactors.MeanStdDev([2.0, 4.0, 4.0, 4.0, 5.0, 5.0, 7.0, 9.0]);
        Assert.Equal(5.0, mean, 12);
        Assert.Equal(Math.Sqrt(32.0 / 7.0), std, 12);

        Assert.Equal((0, 0), SimilarityFactors.MeanStdDev([]));
        Assert.Equal((3.5, 0), SimilarityFactors.MeanStdDev([3.5]));
    }
}
