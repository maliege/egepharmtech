public sealed class AnalysisStateF1F2
{
    // 16 rows, 16 columns: (Time + Rep1..Rep12 + Mean + StdDev + Extra)
    public object?[][] Reference { get; } = CreateMatrix(16, 16);
    public object?[][] Product { get; } = CreateMatrix(16, 16);


    private static object?[][] CreateMatrix(int rows, int cols)
    {
        var data = new object?[rows][];
        for (int r = 0; r < rows; r++)
        {
            data[r] = new object?[cols];
            // varsayılanlar null kalsın; kullanıcı doldursun
        }
        return data;
    }
}
