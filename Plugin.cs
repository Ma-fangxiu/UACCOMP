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

    // 必须保留无参构造函数
    public Plugin()
    {
    }

    public override void Initialize(HostBuilderContext context, IServiceCollection services)
    {
        // 【修复】直接使用 LoggerFactory 创建 Logger，不依赖 ApplicationServices
        try
        {
            _logger = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
                builder.AddDebug();
            }).CreateLogger<Plugin>();
        }
        catch
        {
            // 如果日志创建失败，使用空操作，避免崩溃
            _logger = null;
        }

        _logger?.LogInformation("UACComp插件初始化开始，进程ID: {ProcessId}", Environment.ProcessId);

        try
        {
            bool isAdmin = IsRunningAsAdmin();
            
            if (!isAdmin)
            {
                _logger?.LogWarning("当前未以管理员权限运行，正在请求权限提升...");
                RestartAsAdmin();
                _logger?.LogInformation("已启动管理员进程，当前进程即将退出");
            }
            else
            {
                _logger?.LogInformation("当前已具有管理员权限，插件初始化完成");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "UACComp插件初始化失败");
            throw;
        }
    }

    private bool IsRunningAsAdmin()
    {
        _logger?.LogDebug("正在检查权限...");
        
        try
        {
            using WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);
            bool isAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator);
            
            _logger?.LogDebug("权限检查 - 用户: {User}, 管理员: {IsAdmin}", 
                identity.Name, isAdmin);
            
            return isAdmin;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "权限检查异常");
            return false;
        }
    }

    private void RestartAsAdmin()
    {
        string? currentPath = Environment.ProcessPath;
        
        if (string.IsNullOrWhiteSpace(currentPath))
        {
            _logger?.LogError("无法获取进程路径");
            return;
        }

        string targetPath = currentPath.Replace(".dll", ".exe");
        
        if (!File.Exists(targetPath))
        {
            _logger?.LogError("目标文件不存在: {Path}", targetPath);
            return;
        }

        var startInfo = new ProcessStartInfo()
        {
            FileName = targetPath,
            Verb = "runas",
            UseShellExecute = true
        };

        // 添加标记参数避免循环
        startInfo.ArgumentList.Add("-m");
        
        // 继承原参数
        var args = Environment.GetCommandLineArgs().ToList();
        if (args.Count > 0) args.RemoveAt(0);
        
        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        try
        {
            _logger?.LogInformation("启动管理员进程: {Path}", targetPath);
            var process = Process.Start(startInfo);
            
            if (process != null)
            {
                _logger?.LogInformation("新进程已启动，PID: {Pid}", process.Id);
            }
            
            _logger?.LogInformation("停止当前进程...");
            AppBase.Current?.Stop();
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            _logger?.LogWarning("用户拒绝了UAC权限提升");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "重启进程失败");
        }
    }
}
