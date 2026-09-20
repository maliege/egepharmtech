namespace EgePharmTech.Core.Models;

public class Ttridata
{
    public int grup { get; set; }
    public int siraNo { get; set; }
    public double oil { get; set; }
    public double surCoSur { get; set; }
    public double water { get; set; }
    public PointD? p { get; set; }
}

public class TGroup
{
    public int GroupIndex { get; set; }
    public int N { get; set; }
    public int S { get; set; }
    public int E { get; set; }
    public int EdgeType { get; set; }
    public int PolygonType { get; set; }
    public PointD? PolygonCenterD { get; set; }
    public double A { get; set; }
    public double B { get; set; }
    public double C { get; set; }
    public double Area { get; set; }

    // UI için (hesaplama servisinden gelmeyebilir)
    public string? GrupAdi { get; set; }
}

public class PointD
{
    public double X { get; set; }
    public double Y { get; set; }
}

public class LineD
{
    public PointD? U1 { get; set; }
    public PointD? U2 { get; set; }
}

public class TWcoor
{
    public int xm { get; set; }
    public int ym { get; set; }
    public int xs { get; set; }
    public int ys { get; set; }
    public int w { get; set; }
    public int h { get; set; }
    public int s { get; set; }
    public double p { get; set; }
}
