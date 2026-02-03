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
using Microsoft.Extensions.Logging;  // 添加日志命名空间

namespace UACComp;

[PluginEntrance]
public class Plugin : PluginBase
{
    private readonly ILogger<Plugin> _logger;  // 注入日志器
    
    public Plugin(ILogger<Plugin> logger)  // 通过构造函数注入
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override void Initialize(HostBuilderContext context, IServiceCollection services)
    {
        _logger.LogInformation("UACComp插件开始初始化，当前进程ID: {ProcessId}", Environment.ProcessId);
        
        try
        {
            bool isAdmin = IsRunningAsAdmin();
            
            if (!isAdmin)
            {
                _logger.LogWarning("当前进程未以管理员权限运行，准备请求权限提升...");
                RestartAsAdmin();
                _logger.LogInformation("已启动管理员权限进程，当前进程将退出");
            }
            else
            {
                _logger.LogInformation("当前进程已具有管理员权限，插件初始化完成");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UACComp插件初始化过程中发生严重错误");
            throw;  // 重新抛出，让上层知道初始化失败
        }
    }

    private bool IsRunningAsAdmin()
    {
        _logger.LogDebug("正在检查当前进程权限...");
        
        try
        {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);
            bool isAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator);
            
            _logger.LogDebug("权限检查完成 - 用户: {UserName}, 管理员权限: {IsAdmin}", 
                identity.Name, isAdmin);
            
            return isAdmin;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "检查管理员权限时发生异常");
            return false;  // 保守策略：无法确认时假设无权限，尝试提权
        }
    }

    private void RestartAsAdmin()
    {
        _logger.LogInformation("准备以管理员权限重启进程...");
        
        try
        {
            string? currentPath = Environment.ProcessPath;
            _logger.LogDebug("当前进程路径: {ProcessPath}", currentPath);
            
            if (string.IsNullOrWhiteSpace(currentPath))
            {
                _logger.LogError("无法获取当前进程路径 (Environment.ProcessPath 为空)");
                return;
            }

            // 转换 .dll 为 .exe（如果是 DLL 启动的情况）
            string targetPath = currentPath.Replace(".dll", ".exe");
            _logger.LogDebug("目标启动路径: {TargetPath}", targetPath);
            
            if (!File.Exists(targetPath))
            {
                _logger.LogError("目标可执行文件不存在: {TargetPath}", targetPath);
                return;
            }

            var processStartInfo = new ProcessStartInfo()
            {
                FileName = targetPath,
                Verb = "runas",
                UseShellExecute = true
            };

            // 添加标记参数避免重复提权
            processStartInfo.ArgumentList.Add("-m");
            _logger.LogDebug("已添加标记参数: -m");
            
            // 继承原始命令行参数
            var args = Environment.GetCommandLineArgs().ToList();
            if (args.Count > 0)
            {
                args.RemoveAt(0); // 移除程序路径本身
                _logger.LogDebug("继承原始参数数量: {ArgCount}", args.Count);
                
                foreach (var arg in args)
                {
                    processStartInfo.ArgumentList.Add(arg);
                    _logger.LogTrace("继承参数: {Argument}", arg);  // Trace级别避免敏感信息泄露
                }
            }
            
            _logger.LogInformation("正在启动管理员权限进程，文件: {FileName}", processStartInfo.FileName);
            
            var newProcess = Process.Start(processStartInfo);
            
            if (newProcess != null)
            {
                _logger.LogInformation("新进程已启动，PID: {NewProcessId}", newProcess.Id);
            }
            else
            {
                _logger.LogWarning("Process.Start 返回 null，可能进程启动失败");
            }
            
            // 停止当前进程
            _logger.LogInformation("正在停止当前非管理员进程...");
            AppBase.Current?.Stop();
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            // 1223 = ERROR_CANCELLED (用户取消了UAC提示)
            _logger.LogWarning("用户拒绝了UAC权限提升请求，插件将以普通权限继续运行");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "以管理员权限重启进程时发生错误");
            // 不抛出异常，让应用继续以当前权限运行
        }
    }
}
