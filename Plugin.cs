using ClassIsland.Core;
using ClassIsland.Core.Abstractions;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Extensions.Registry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Diagnostics;
using System.Linq;
using System.Security.Principal;

namespace UACComp;

[PluginEntrance]
public class Plugin : PluginBase
{
    public override void Initialize(HostBuilderContext context, IServiceCollection services)
    {
        GlobalConstants.PluginConfigFolder = PluginConfigFolder;
        GlobalConstants.Information.PluginFolder = Info.PluginFolderPath;
        GlobalConstants.Information.PluginVersion = Info.Manifest.Version;
        GlobalConstants.Config = new ConfigHandler(PluginConfigFolder);

        services.AddSettingsPage<UACCompSettingsPage>();

        if (GlobalConstants.Config.Data.EnableAutoAdminRestart)
        {
            Console.WriteLine("[UACComp] 自动管理员重启已启用");

            if (!IsRunningInAdmin())
            {
                try
                {
                    var processStartInfo = new ProcessStartInfo()
                    {
                        FileName = Environment.ProcessPath?.Replace(".dll", ".exe"),
                        Verb = "runas",
                        UseShellExecute = true
                    };

                    processStartInfo.ArgumentList.Add("-m");

                    var args = Environment.GetCommandLineArgs().ToList();
                    args.RemoveAt(0);
                    foreach (var i in args)
                    {
                        processStartInfo.ArgumentList.Add(i);
                    }

                    Process.Start(processStartInfo);
                    AppBase.Current.Stop();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[UACComp] 管理员重启失败: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine("[UACComp] 当前已是管理员身份");
            }
        }
        else
        {
            Console.WriteLine("[UACComp] 自动管理员重启未启用");
        }
    }

    private static bool IsRunningInAdmin()
    {
        var id = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(id);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }
}