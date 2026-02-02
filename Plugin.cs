using System;
using System.Diagnostics;
using System.IO;
using System.Linq
using System.Security.Principal;
using System.Windows.Forms;
using ClassIsland.Core;
using ClassIsland.Core.Abstractions;
using ClassIsland.Core.Attributes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace UACComp;

/// <summary>
/// UAC Comp 插件入口类
/// 检测管理员权限，非管理员时自动以管理员身份重启
/// </summary>
[PluginEntrance]
public class Plugin : PluginBase
{
    // 日志文件路径
    private static readonly string LogFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ClassIsland",
        "Logs",
        "UACHelper.log");

    /// <summary>
    /// 无参构造函数（ClassIsland 需要）
    /// </summary>
    public Plugin()
    {
        Log("========== UACComp 启动 ==========");
    }

    /// <summary>
    /// 插件初始化方法
    /// 在应用程序启动时调用
    /// </summary>
    public override void Initialize(HostBuilderContext context, IServiceCollection services)
    {
        try
        {
            Log("Initialize 开始");

            // 检测管理员权限
            bool isAdmin = IsRunningAsAdmin();
            Log($"管理员权限: {isAdmin}");

            if (!isAdmin)
            {
                Log("非管理员权限，准备重启");
                RestartAsAdmin();
            }
            else
            {
                Log("已是管理员权限");
            }

            Log("Initialize 完成");
        }
        catch (Exception ex)
        {
            Log($"Initialize 异常: {ex.Message}");
            Log($"堆栈: {ex.StackTrace}");
        }
    }

    /// <summary>
    /// 写入日志
    /// </summary>
    private static void Log(string message)
    {
        try
        {
            string logDir = Path.GetDirectoryName(LogFilePath);
            if (!string.IsNullOrEmpty(logDir) && !Directory.Exists(logDir))
            {
                Directory.CreateDirectory(logDir);
            }

            string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
            File.AppendAllText(LogFilePath, logEntry + Environment.NewLine);
        }
        catch { }
    }

    /// <summary>
    /// 检测当前是否以管理员权限运行
    /// </summary>
    private bool IsRunningAsAdmin()
    {
        try
        {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch (Exception ex)
        {
            Log($"检测权限异常: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 以管理员身份重启应用
    /// </summary>
    private void RestartAsAdmin()
    {
        try
        {
            Log("RestartAsAdmin 开始");

            // 获取 exe 路径
            string executablePath = Environment.ProcessPath?.Replace(".dll", ".exe") ?? 
                                  Application.ExecutablePath?.Replace(".dll", ".exe");

            Log($"目标路径: {executablePath}");

            if (string.IsNullOrEmpty(executablePath) || !File.Exists(executablePath))
            {
                Log("错误: 找不到可执行文件");
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

            Log("启动新进程");
            Process.Start(startInfo);
            Log("新进程已启动");

            // 延迟后停止当前应用
            Log("准备停止当前应用");
            System.Threading.Tasks.Task.Run(async () =>
            {
                await System.Threading.Tasks.Task.Delay(2000);
                Log("执行停止");
                AppBase.Current?.Stop();
            });
        }
        catch (Exception ex)
        {
            Log($"RestartAsAdmin 异常: {ex.Message}");
        }
    }
}
