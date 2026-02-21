using System.ComponentModel;
using System.Text.Json.Serialization;

namespace UACComp;

public class Settings : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action? RestartNeeded;

    private bool _enableAutoAdminRestart = false;

    [JsonPropertyName("enableAutoAdminRestart")]
    public bool EnableAutoAdminRestart
    {
        get => _enableAutoAdminRestart;
        set
        {
            if (_enableAutoAdminRestart == value) return;
            _enableAutoAdminRestart = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(EnableAutoAdminRestart)));
            RestartNeeded?.Invoke();
        }
    }
}