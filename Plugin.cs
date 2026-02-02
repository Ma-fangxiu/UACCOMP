using System;
using System.Diagnostics;
using System.Linq;
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
    /// <param name="context">主机构建上下文</param>
    /// <param name="services">服务集合</param>
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
    /// <returns>true: 是管理员, false: 不是管理员</returns>
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
        // 获取当前进程路径，将 .dll 替换为 .exe
        string executablePath = Environment.ProcessPath?.Replace(".dll", ".exe") ?? 
                              Application.ExecutablePath.Replace(".dll", ".exe");

        // 创建启动信息
        ProcessStartInfo processStartInfo = new ProcessStartInfo()
        {
            FileName = executablePath,
            ArgumentList = { "-m" },
            Verb = "runas",
            UseShellExecute = true
        };

        // 添加当前命令行参数（排除程序路径本身）
        var args = Environment.GetCommandLineArgs().ToList();
        args.RemoveAt(0);
        foreach (var arg in args)
        {
            processStartInfo.ArgumentList.Add(arg);
        }

        // 启动新进程
        Process.Start(processStartInfo);

        // 停止当前应用
        AppBase.Current?.Stop();
    }
}
