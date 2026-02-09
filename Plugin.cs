using System;
using System.Diagnostics;
using System.Linq;
using System.Security.Principal;
using ClassIsland.Core;
using ClassIsland.Core.Abstractions;
using ClassIsland.Core.Attributes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace UACComp;

[PluginEntrance]
public class Plugin : PluginBase
{
    public Plugin()
    {
        // 无参构造函数
    }
    
    private const string RestartFlag = "--uac-restarted";

    public override void Initialize(HostBuilderContext context, IServiceCollection services)
    {
        // 检查是否已经重启过（防止无限循环）
        if (IsRestarted())
        {
            return;
        }

        // 检查是否以管理员权限运行
        if (!IsRunningAsAdmin())
        {
            RestartAsAdmin();
        }
    }

    /// <summary>
    /// 检查是否已经重启过
    /// </summary>
    private bool IsRestarted()
    {
        return Environment.GetCommandLineArgs().Contains(RestartFlag);
    }

    /// <summary>
    /// 检查当前进程是否以管理员权限运行
    /// </summary>
    private bool IsRunningAsAdmin()
    {
        try
        {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 获取可执行文件路径
    /// </summary>
    private string GetExecutablePath()
    {
        // 参考 StartUpAsAdmin 的实现，使用 Environment.ProcessPath 并替换 .dll 为 .exe
        var processPath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(processPath))
        {
            // 如果无法获取进程路径，尝试使用 AppBase.ExecutingEntrance
            return AppBase.ExecutingEntrance;
        }
        
        // 简单直接地替换 .dll 为 .exe，与 StartUpAsAdmin 保持一致
        if (processPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            return processPath.Replace(".dll", ".exe");
        }
        
        // 如果不是 .dll 文件，直接返回原路径
        return processPath;
    }

    /// <summary>
    /// 以管理员权限重启应用
    /// </summary>
    private void RestartAsAdmin()
    {
        try
        {
            // 获取当前命令行参数
            var currentArgs = Environment.GetCommandLineArgs().ToList();
            
            // 移除程序路径（第一个参数）
            if (currentArgs.Count > 0)
            {
                currentArgs.RemoveAt(0);
            }

            // 移除旧的重启标记（防止重复）
            currentArgs.RemoveAll(arg => arg == RestartFlag);

            // 添加重启标记
            currentArgs.Add(RestartFlag);

            // 使用更可靠的路径获取方式，参考 StartUpAsAdmin
            var appPath = GetExecutablePath();

            var processStartInfo = new ProcessStartInfo()
            {
                FileName = appPath,
                Verb = "runas",  // 请求提升权限
                UseShellExecute = true
            };

            // 添加参数
            foreach (var arg in currentArgs)
            {
                processStartInfo.ArgumentList.Add(arg);
            }

            // 启动新进程
            var process = Process.Start(processStartInfo);
            
            // 如果启动成功，停止当前应用
            if (process != null)
            {
                AppBase.Current?.Stop();
            }
        }
        catch (Exception ex)
        {
            // 记录错误，但不影响应用启动
            Debug.WriteLine($"UAC 提权失败: {ex.Message}");
        }
    }
}