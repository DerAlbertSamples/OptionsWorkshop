namespace OptionsWorkshop;

public class TestOptions
{
    public string Region { get; set; } = "DefaultRegion";
    public string Url { get; set; } = string.Empty;
    public decimal Vat { get; set; }
    public bool Enabled { get; set; }

    public List<string> Callstack { get; } = new();
}

public class RegionOptions
{
    public string Region { get; set; } = string.Empty;
}