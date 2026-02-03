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

    // 保留无参构造函数，否则 ClassIsland 无法实例化插件
    public Plugin()
    {
    }

    public override void Initialize(HostBuilderContext context, IServiceCollection services)
    {
        // 在 Initialize 中从 DI 容器获取 Logger（此时服务已可用）
        try
        {
            _logger = context.HostingEnvironment.ApplicationServices?.GetService<ILogger<Plugin>>();
        }
        catch
        {
            // 如果获取失败，使用简单控制台日志作为后备
            _logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<Plugin>();
        }

        _logger?.LogInformation("UACComp插件初始化开始，当前进程ID: {ProcessId}", Environment.ProcessId);

        try
        {
            bool isAdmin = IsRunningAsAdmin();
            
            if (!isAdmin)
            {
                _logger?.LogWarning("当前未以管理员权限运行，正在请求权限提升...");
                RestartAsAdmin();
                // 注意：如果成功启动新进程，这里会在Stop后返回，不会继续执行
                _logger?.LogInformation("已启动管理员进程，当前进程即将退出");
            }
            else
            {
                _logger?.LogInformation("当前已具有管理员权限，插件初始化完成");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "UACComp插件初始化过程中发生严重错误");
            throw;  // 重新抛出让 ClassIsland 知道初始化失败
        }
    }

    private bool IsRunningAsAdmin()
    {
        _logger?.LogDebug("正在检查当前进程权限...");
        
        try
        {
            using WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);
            bool isAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator);
            
            _logger?.LogDebug("权限检查结果 - 用户: {UserName}, 管理员: {IsAdmin}", 
                identity.Name, isAdmin);
            
            return isAdmin;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "检查管理员权限时发生异常");
            return false;  // 保守策略：无法确认时尝试提权
        }
    }

    private void RestartAsAdmin()
    {
        string? currentPath = Environment.ProcessPath;
        _logger?.LogDebug("当前进程路径: {ProcessPath}", currentPath);

        if (string.IsNullOrWhiteSpace(currentPath))
        {
            _logger?.LogError("无法获取当前进程路径 (Environment.ProcessPath 为空)");
            return;
        }

        // 确保启动的是 .exe 而不是 .dll
        string targetPath = currentPath.Replace(".dll", ".exe");
        
        if (!File.Exists(targetPath))
        {
            _logger?.LogError("目标可执行文件不存在: {TargetPath}", targetPath);
            return;
        }

        var processStartInfo = new ProcessStartInfo()
        {
            FileName = targetPath,
            Verb = "runas",  // 触发UAC提权
            UseShellExecute = true
        };

        // 添加标记参数避免无限循环
        processStartInfo.ArgumentList.Add("-m");
        _logger?.LogDebug("已添加标记参数: -m");
        
        // 继承原始命令行参数（保持配置）
        var args = Environment.GetCommandLineArgs().ToList();
        if (args.Count > 0) args.RemoveAt(0);  // 移除程序路径本身
        
        foreach (var arg in args)
        {
            processStartInfo.ArgumentList.Add(arg);
            _logger?.LogTrace("继承参数: {Argument}", arg);
        }

        try
        {
            _logger?.LogInformation("正在启动管理员权限进程: {FileName}", processStartInfo.FileName);
            
            var process = Process.Start(processStartInfo);
            if (process != null)
            {
                _logger?.LogInformation("新进程已启动，PID: {NewProcessId}", process.Id);
            }
            else
            {
                _logger?.LogWarning("Process.Start 返回 null，进程可能未成功启动");
            }
            
            // 立即停止当前进程
            _logger?.LogInformation("正在停止当前非管理员进程...");
            AppBase.Current?.Stop();
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            // 1223 = ERROR_CANCELLED (用户点击了UAC的"否")
            _logger?.LogWarning("用户拒绝了UAC权限提升请求，将以当前权限继续运行");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "以管理员权限重启进程时发生错误");
            // 不抛出异常，让应用继续以当前权限运行
        }
    }
}
