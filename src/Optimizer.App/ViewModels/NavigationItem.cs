namespace Optimizer.App.ViewModels;

public sealed class NavigationItem(string key, string label, string symbol, bool isAvailable, string? badge, bool isSelected = false) : ObservableObject
{
    private bool _isSelected = isSelected;

    public string Key { get; } = key;
    public string Label { get; } = label;
    public string Symbol { get; } = symbol;
    public bool IsAvailable { get; } = isAvailable;
    public string? Badge { get; } = badge;
    public bool IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value); }
}
