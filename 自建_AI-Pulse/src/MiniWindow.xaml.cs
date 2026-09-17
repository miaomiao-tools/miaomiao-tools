using System.Windows;
using System.Windows.Input;

namespace AIPulse;

public partial class MiniWindow : Window
{
    public MiniPresenter Presenter { get; }
    public Dashboard Model => Presenter.Model;
    public event Action? ExpandRequested;
    public event Action? PinChanged;
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == TopmostProperty) PinChanged?.Invoke();
    }
    public MiniWindow(Dashboard model)
    {
        InitializeComponent(); Presenter = new(model); DataContext = Presenter;
        PreviewKeyDown += async (_, e) =>
        {
            if (e.Key == Key.F5) { e.Handled = true; await Model.ToggleRunAsync(); }
            else if (e.Key == Key.M && Keyboard.Modifiers == ModifierKeys.Control) { e.Handled = true; ExpandRequested?.Invoke(); }
            else if (e.Key == Key.T && Keyboard.Modifiers == ModifierKeys.Control) { e.Handled = true; Topmost = !Topmost; }
        };
        Closed += (_, _) => Presenter.Dispose();
    }
    private async void RunClick(object sender, RoutedEventArgs e) => await Model.ToggleRunAsync();
    private void ExpandClick(object sender, RoutedEventArgs e) => ExpandRequested?.Invoke();
    private void CloseClick(object sender, RoutedEventArgs e) => Close();
    private void ServiceDoubleClick(object sender, MouseButtonEventArgs e) { if (Model.Selected != null) ExpandRequested?.Invoke(); }
}
