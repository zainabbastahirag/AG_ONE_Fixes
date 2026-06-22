namespace LNK.Configuration;

public class AppOptions
{
    public const string SectionName = "App";
    public string Name { get; set; } = "LNK";
    public string Tagline { get; set; } = "Never Run Out Of LinkedIn Content";
    public bool SeedDemoData { get; set; } = true;
}
