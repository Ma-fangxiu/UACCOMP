using System;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Windows.Forms;
using ClassIsland.Core;
using ClassIsland.Core.Abstractions;
using ClassIsland.Core.Attributes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace UACComp;

/// <summary>
/// UAC Helper 插件入口类
/// 检测管理员权限，非管理员时自动以管理员身份重启
/// </summary>
[PluginEntrance]
public class Plugin : PluginBase
{
    /// <summary>
    /// 无参构造函数（ClassIsland 需要）
    /// </summary>
    public Plugin()
    {
    }

    /// <summary>
    /// 插件初始化方法
    /// 在应用程序启动时调用
    /// </summary>
    public override void Initialize(HostBuilderContext context, IServiceCollection services)
    {
        // 检测管理员权限
        if (!IsRunningAsAdmin())
        {
            RestartAsAdmin();
        }
    }

    /// <summary>
    /// 检测当前是否以管理员权限运行
    /// </summary>
    private bool IsRunningAsAdmin()
    {
        WindowsIdentity identity = WindowsIdentity.GetCurrent();
        WindowsPrincipal principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    /// <summary>
    /// 以管理员身份重启应用
    /// </summary>
    private void RestartAsAdmin()
    {
        // 获取 exe 路径
        string executablePath = Environment.ProcessPath?.Replace(".dll", ".exe") ?? 
                              Application.ExecutablePath?.Replace(".dll", ".exe");

        if (string.IsNullOrEmpty(executablePath) || !File.Exists(executablePath))
        {
            return;
        }

        // 创建启动信息
        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            Arguments = "-m",
            Verb = "runas",
            UseShellExecute = true
        };

        // 启动新进程
        Process.Start(startInfo);

        // 延迟后停止当前应用
        System.Threading.Tasks.Task.Run(async () =>
        {
            await System.Threading.Tasks.Task.Delay(2000);
            AppBase.Current?.Stop();
        });
    }
}
