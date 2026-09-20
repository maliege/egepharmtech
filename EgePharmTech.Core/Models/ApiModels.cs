namespace EgePharmTech.Core.Models;

public class DiagramCalculationRequest
{
    public List<Ttridata> Data { get; set; } = new();
    public ChartConfiguration Configuration { get; set; } = new();
}

public class DiagramCalculationResponse
{
    public string Message { get; set; } = "";
    public List<TGroup> Groups { get; set; } = new();
    public bool Success { get; set; } = true;
}

public class ChartConfiguration
{
    public string TitleChart { get; set; } = "Ternary Phase Diagram";
    public string TitleOil { get; set; } = "Oil";
    public string TitleSurfactant { get; set; } = "Surfactant";
    public string TitleWater { get; set; } = "Water";
    
    public int AxisPointCount { get; set; } = 10;
    public int FillAlpha { get; set; } = 100;
    public int BorderAlpha { get; set; } = 100;
    public int PolygonCloseType { get; set; } = 0;
    public int PolygonCloseEdge { get; set; } = 2;
    
    public List<Ttridata> Data { get; set; } = new();
}
