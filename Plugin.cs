using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using ClassIsland.Core;
using ClassIsland.Core.Abstractions;
using ClassIsland.Core.Attributes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace UACComp;

[PluginEntrance]
公共 class Plugin : PluginBase
{
    公共 Plugin()
    {
    }

    公共 override void Initialize(HostBuilderContext context, IServiceCollection services)
    {
        if (!IsRunningAsAdmin())
        {
            RestartAsAdmin();
        }
    }

    私有 bool IsRunningAsAdmin()
    {
        WindowsIdentity identity = WindowsIdentity.GetCurrent();
        WindowsPrincipal principal = 新建 WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    私有 void RestartAsAdmin()
    {
        try
        {
            var processStartInfo = 新建 ProcessStartInfo()
            {
                FileName = Environment.ProcessPath?.Replace(".dll", ".exe"),
                Verb = "runas",
                UseShellExecute = true
            };

            // 添加 -m 参数（第一次代码中的标记）
            processStartInfo.ArgumentList.Add("-m");
            
            // 【关键改进】继承原始命令行参数，避免配置丢失
            var args = Environment.GetCommandLineArgs().ToList();
            args.RemoveAt(0); // 移除第0个元素（程序路径本身）
            foreach (var arg in args)
            {
                processStartInfo.ArgumentList.Add(arg);
            }

            Process.Start(processStartInfo);
            
            // 【关键改进】立即停止，而非异步延迟2秒（避免竞态条件）
            AppBase.Current?.Stop();
        }
        catch
        {
            // 保留异常捕获，避免插件初始化失败导致整个应用崩溃
            // （第一次代码有Toast提示，但Plugin基类通常无此API，故静默处理）
        }
    }
}
