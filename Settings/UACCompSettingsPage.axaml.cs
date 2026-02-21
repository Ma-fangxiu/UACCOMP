using Avalonia.Interactivity;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Attributes;
using CommunityToolkit.Mvvm.ComponentModel;

namespace UACComp;

[HidePageTitle]
[SettingsPageInfo("uaccomp.settings.main", "UAC助手 设置", "\uEF53", "\uEF53")]
public partial class UACCompSettingsPage : SettingsPageBase
{
    public UACCompSettingsViewModel ViewModel { get; }

    public UACCompSettingsPage()
    {
        ViewModel = new UACCompSettingsViewModel();
        DataContext = this;
        InitializeComponent();

        ViewModel.Settings.RestartNeeded += OnRestartNeeded;
    }

    private void OnRestartNeeded()
    {
        RequestRestart();
    }
}

public partial class UACCompSettingsViewModel : ObservableObject
{
    [ObservableProperty]
    private Settings _settings = GlobalConstants.Config!.Data;

    [ObservableProperty]
    private string _pluginVersion = GlobalConstants.Information.PluginVersion;
}