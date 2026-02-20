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
/// UAC 助手
/// 功能：检测应用权限，如果不是管理员身份则自动以管理员身份重启
/// </summary>
[PluginEntrance]
public class Plugin : PluginBase
{
    /// <summary>
    /// 无参构造函数（ClassIsland 插件要求）
    /// </summary>
    public Plugin()
    {
    }
    
    /// <summary>
    /// 重启标记常量
    /// 用于防止无限循环：如果检测到此参数，说明已经重启过一次，不再重复重启
    /// </summary>
    private const string RestartFlag = "--uac-restarted";

    /// <summary>
    /// 插件初始化方法（ClassIsland 加载插件时自动调用）
    /// </summary>
    /// <param name="context">主机构建上下文</param>
    /// <param name="services">服务集合（用于依赖注入）</param>
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
            // 如果不是管理员，以管理员身份重启
            RestartAsAdmin();
        }
    }

    /// <summary>
    /// 检查当前进程是否已经重启过
    /// </summary>
    /// <returns>如果命令行参数中包含重启标记，返回 true</returns>
    private bool IsRestarted()
    {
        return Environment.GetCommandLineArgs().Contains(RestartFlag);
    }

    /// <summary>
    /// 检查当前进程是否以管理员权限运行
    /// </summary>
    /// <returns>如果是管理员权限返回 true，否则返回 false</returns>
    private bool IsRunningAsAdmin()
    {
        try
        {
            // 获取当前 Windows 用户标识
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            // 创建 Windows 主体对象用于角色检查
            WindowsPrincipal principal = new WindowsPrincipal(identity);
            // 检查用户是否属于管理员角色
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            // 如果获取权限信息失败，默认返回 false（不是管理员）
            return false;
        }
    }

    /// <summary>
    /// 以管理员权限重启应用
    /// </summary>
    private void RestartAsAdmin()
    {
        try
        {
            // 获取当前命令行参数列表
            var currentArgs = Environment.GetCommandLineArgs().ToList();
            
            // 移除第一个参数（程序路径本身）
            // 例如：["ClassIsland.exe", "-m", "--config"] -> ["-m", "--config"]
            if (currentArgs.Count > 0)
            {
                currentArgs.RemoveAt(0);
            }

            // 移除旧的重启标记（防止重复添加）
            currentArgs.RemoveAll(arg => arg == RestartFlag);
            
            // 添加新的重启标记
            currentArgs.Add(RestartFlag);

            // 确保 -m 参数存在（用于多开实例）
            // ClassIsland 需要 -m 参数才能启动多个实例
            if (!currentArgs.Contains("-m"))
            {
                // 将 -m 插入到最前面，确保优先处理
                currentArgs.Insert(0, "-m");
            }

            // 获取可执行文件路径
            // Environment.ProcessPath 可能返回 .dll 路径（.NET 单文件发布）
            // 需要替换为 .exe 才能正确启动
            var appPath = Environment.ProcessPath?.Replace(".dll", ".exe");

            // 创建新进程启动信息
            var processStartInfo = new ProcessStartInfo()
            {
                FileName = appPath,              // 可执行文件路径
                Verb = "runas",                  // 请求以管理员身份运行（触发 UAC 提示）
                UseShellExecute = true           // 必须为 true 才能使用 Verb
            };

            // 添加所有命令行参数
            foreach (var arg in currentArgs)
            {
                processStartInfo.ArgumentList.Add(arg);
            }

            // 启动新的管理员权限进程
            var process = Process.Start(processStartInfo);
            
            // 如果启动成功，停止当前进程
            // 检查 process != null 是为了处理用户拒绝 UAC 提示的情况
            if (process != null)
            {
                AppBase.Current?.Stop();
            }
        }
        catch (Exception ex)
        {
            // 记录错误到调试输出
            // 不抛出异常，避免影响应用正常启动
            Debug.WriteLine($"UAC 提权失败: {ex.Message}");
        }
    }
}