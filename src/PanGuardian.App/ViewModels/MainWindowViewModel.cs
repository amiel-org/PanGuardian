using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PanGuardian.App.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private DashboardViewModel _dashboard = null!;
    private MigrationViewModel _migration = null!;
    private CleanupViewModel _cleanup = null!;

    public required DashboardViewModel Dashboard
    {
        get => _dashboard;
        set
        {
            if (ReferenceEquals(_dashboard, value))
            {
                return;
            }

            _dashboard = value;
            OnPropertyChanged();
        }
    }

    public required MigrationViewModel Migration
    {
        get => _migration;
        set
        {
            if (ReferenceEquals(_migration, value))
            {
                return;
            }

            _migration = value;
            OnPropertyChanged();
        }
    }

    public required CleanupViewModel Cleanup
    {
        get => _cleanup;
        set
        {
            if (ReferenceEquals(_cleanup, value))
            {
                return;
            }

            _cleanup = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
