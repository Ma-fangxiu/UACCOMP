using System.IO;
using System.Text.Json;
using ClassIsland.Shared.Helpers;

namespace UACComp;

public class ConfigHandler
{
    private readonly string _configPath;
    public Settings Data { get; set; }

    public ConfigHandler(string pluginConfigFolder)
    {
        _configPath = Path.Combine(pluginConfigFolder, "config.json");
        Data = new Settings();

        Load();
        Data.PropertyChanged += (_, _) => Save();
    }

    private void Load()
    {
        try
        {
            if (File.Exists(_configPath))
            {
                Data = ConfigureFileHelper.LoadConfig<Settings>(_configPath);
                // 重新订阅事件
                Data.PropertyChanged += (_, _) => Save();
            }
            else
            {
                Save();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[UACComp] 加载配置失败: {ex.Message}");
            Save();
        }
    }

    public void Save()
    {
        try
        {
            ConfigureFileHelper.SaveConfig(_configPath, Data);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[UACComp] 保存配置失败: {ex.Message}");
        }
    }
}