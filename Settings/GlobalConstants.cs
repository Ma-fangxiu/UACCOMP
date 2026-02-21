namespace UACComp;

public static class GlobalConstants
{
    public static string? PluginConfigFolder { get; set; }
    public static ConfigHandler? Config { get; set; }

    public static class Information
    {
        public static string PluginFolder { get; set; } = string.Empty;
        public static string PluginVersion { get; set; } = "???";
    }
}