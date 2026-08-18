using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PokeTokenBar.Services.Settings;
using PokeTokenBar.ViewModels;

namespace PokeTokenBar.Views;

public partial class FloatingCompanionWindow : Window
{
    private readonly Action _openMainWindow;
    private readonly Action _exitApplication;
    private System.Windows.Point _dragStart;
    private bool _dragging;

    public FloatingCompanionWindow(MainViewModel viewModel, Action openMainWindow, Action exitApplication)
    {
        InitializeComponent();
        DataContext = viewModel;
        _openMainWindow = openMainWindow;
        _exitApplication = exitApplication;
        Width = viewModel.DesktopCompanionSize;
        Height = viewModel.DesktopCompanionSize;
    }

    private MainViewModel ViewModel => (MainViewModel)DataContext;

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(this);
        _dragging = false;
        CaptureMouse();
    }

    private void OnMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!IsMouseCaptured || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var current = e.GetPosition(this);
        if (!_dragging && (Math.Abs(current.X - _dragStart.X) > 3 || Math.Abs(current.Y - _dragStart.Y) > 3))
        {
            _dragging = true;
            try
            {
                DragMove();
                ViewModel.UpdateDesktopCompanionPosition(Left, Top);
            }
            catch (InvalidOperationException)
            {
            }
        }
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        ReleaseMouseCapture();
        if (!_dragging)
        {
            _openMainWindow();
        }

        ViewModel.UpdateDesktopCompanionPosition(Left, Top);
    }

    private void OnOpenClicked(object sender, RoutedEventArgs e) => _openMainWindow();

    private void OnHideClicked(object sender, RoutedEventArgs e) => ViewModel.ShowDesktopCompanion = false;

    private void OnSizeClicked(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: string raw } && int.TryParse(raw, out var size))
        {
            ViewModel.DesktopCompanionSize = size;
            Width = ViewModel.DesktopCompanionSize;
            Height = ViewModel.DesktopCompanionSize;
        }
    }

    private void OnExitClicked(object sender, RoutedEventArgs e) => _exitApplication();
}
