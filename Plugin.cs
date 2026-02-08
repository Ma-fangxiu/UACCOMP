using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using ClassIsland.Core;
using ClassIsland.Core.Abstractions;
using ClassIsland.Core.Attributes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace UACComp;

[PluginEntrance]
public class Plugin : PluginBase
{
    private ILogger<Plugin>? _logger;
    private const string UacMarkerArg = "-m";

    public Plugin()
    {
    }

    public override void Initialize(HostBuilderContext context, IServiceCollection services)
    {
        var args = Environment.GetCommandLineArgs();
        bool isRestarted = args.Contains(UacMarkerArg);
        
        try
        {
            _logger = LoggerFactory.Create(builder => 
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Information);
            }).CreateLogger<Plugin>();
        }
        catch { }

        if (isRestarted)
        {
            _logger?.LogInformation("UACComp: 检测到提权标记，当前已是管理员权限进程");
            return;
        }

        _logger?.LogInformation("UACComp: 开始检查管理员权限...");

        try
        {
            if (!IsRunningAsAdmin())
            {
                _logger?.LogWarning("UACComp: 当前非管理员权限，准备请求提升...");
                RestartAsAdmin(args);
                _logger?.LogInformation("UACComp: 正在终止当前非管理员进程...");
                Environment.Exit(0);
            }
            else
            {
                _logger?.LogInformation("UACComp: 当前已是管理员权限，无需操作");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "UACComp: 权限提升过程发生错误");
        }
    }

    private bool IsRunningAsAdmin()
    {
        _logger?.LogDebug("UACComp: 正在检查权限...");
        
        try
        {
            using WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);
            bool isAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator);
            
            _logger?.LogDebug("UACComp: 权限检查 - 用户: {User}, 管理员: {IsAdmin}", 
                identity.Name, isAdmin);
            
            return isAdmin;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "UACComp: 检查权限时发生异常");
            return false;
        }
    }

    private void RestartAsAdmin(string[] currentArgs)
    {
        string? currentPath = Environment.ProcessPath;
        
        if (string.IsNullOrWhiteSpace(currentPath))
        {
            _logger?.LogError("UACComp: 无法获取当前进程路径");
            return;
        }

        string targetPath = currentPath.Replace(".dll", ".exe");
        
        if (!File.Exists(targetPath))
        {
            _logger?.LogError("UACComp: 目标文件不存在: {Path}", targetPath);
            return;
        }

        var startInfo = new ProcessStartInfo()
        {
            FileName = targetPath,
            Verb = "runas",
            UseShellExecute = true
        };

        startInfo.ArgumentList.Add(UacMarkerArg);

        for (int i = 1; i < currentArgs.Length; i++)
        {
            if (currentArgs[i] != UacMarkerArg)
            {
                startInfo.ArgumentList.Add(currentArgs[i]);
            }
        }

        try
        {
            _logger?.LogInformation("UACComp: 正在启动管理员权限进程: {Path}", targetPath);
            var process = Process.Start(startInfo);
            
            if (process != null)
            {
                _logger?.LogInformation("UACComp: 新进程已启动，PID: {Pid}", process.Id);
            }
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            _logger?.LogWarning("UACComp: 用户拒绝了UAC权限提升");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "UACComp: 启动管理员进程失败");
            throw;
        }
    }
}
