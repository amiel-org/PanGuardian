using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PanGuardian.App.ViewModels;

public sealed class CleanupCandidateFileViewModel : INotifyPropertyChanged
{
    private bool _isSelected = true;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
            {
                return;
            }

            _isSelected = value;
            OnPropertyChanged();
        }
    }

    public string Category { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public string FullPath { get; init; } = string.Empty;
    public long SizeBytes { get; init; }
    public string SizeText { get; init; } = string.Empty;
    public string AgeText { get; init; } = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
