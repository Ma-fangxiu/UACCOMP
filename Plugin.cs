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

/// <summary>
/// 插件入口 - 备用方案（完全模仿 StartUpAsAdmin 方式）
/// 如需使用此方案，请将此类重命名为 Plugin.cs 并删除原来的 Plugin.cs
/// </summary>
[PluginEntrance]
public class PluginAlternative : PluginBase
{
    public PluginAlternative()
    {
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
    /// 以管理员权限重启应用（完全模仿 StartUpAsAdmin 方式）
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

            // 移除原始的 -m 参数（如果存在）
            currentArgs.RemoveAll(arg => arg == "-m");

            // 添加重启标记
            currentArgs.Add(RestartFlag);

            // 使用 StartUpAsAdmin 的方式：在 ArgumentList 初始化时就添加 -m
            var appPath = Environment.ProcessPath?.Replace(".dll", ".exe");

            var processStartInfo = new ProcessStartInfo()
            {
                FileName = appPath,
                Verb = "runas",  // 请求提升权限
                UseShellExecute = true,
                ArgumentList = { "-m" }  // 初始化时就添加 -m 参数
            };

            // 添加其他参数
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