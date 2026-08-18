using System.ComponentModel;
using System.Windows;
using PokeTokenBar.Core.Interfaces;
using PokeTokenBar.Services.Settings;
using PokeTokenBar.ViewModels;
using PokeTokenBar.Views;

namespace PokeTokenBar.Services.Floating;

public sealed class FloatingCompanionService : IDisposable
{
    private readonly MainViewModel _viewModel;
    private readonly Action _openMainWindow;
    private readonly IAppLogger _logger;
    private FloatingCompanionWindow? _window;
    private bool _disposed;

    public FloatingCompanionService(MainViewModel viewModel, Action openMainWindow, IAppLogger logger)
    {
        _viewModel = viewModel;
        _openMainWindow = openMainWindow;
        _logger = logger;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    public bool IsVisible => _window?.IsVisible == true;

    public void Sync()
    {
        if (_viewModel.ShowDesktopCompanion)
        {
            Show();
        }
        else
        {
            Hide();
        }
    }

    public void Show()
    {
        if (_disposed)
        {
            return;
        }

        if (_window is null)
        {
            _window = new FloatingCompanionWindow(_viewModel, _openMainWindow, () => global::System.Windows.Application.Current.Shutdown());
            _window.Closed += (_, _) => _window = null;
        }

        ApplyPlacement();
        _window.Show();
    }

    public void Hide() => _window?.Hide();

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        if (_window is not null)
        {
            _window.Close();
            _window = null;
        }

        _disposed = true;
    }

    private void ApplyPlacement()
    {
        if (_window is null)
        {
            return;
        }

        var placement = FloatingCompanionBounds.EnsureVisible(
            new FloatingCompanionPlacement(_viewModel.DesktopCompanionLeft, _viewModel.DesktopCompanionTop, _viewModel.DesktopCompanionSize),
            CurrentDesktopBounds());
        _window.Left = placement.Left;
        _window.Top = placement.Top;
        _window.Width = placement.Size;
        _window.Height = placement.Size;
        _window.Topmost = false;
        if (placement.Left != _viewModel.DesktopCompanionLeft || placement.Top != _viewModel.DesktopCompanionTop || placement.Size != _viewModel.DesktopCompanionSize)
        {
            _viewModel.UpdateDesktopCompanionPlacement(placement.Left, placement.Top, placement.Size);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!global::System.Windows.Application.Current.Dispatcher.CheckAccess())
        {
            global::System.Windows.Application.Current.Dispatcher.Invoke(() => OnViewModelPropertyChanged(sender, e));
            return;
        }

        if (e.PropertyName is nameof(MainViewModel.ShowDesktopCompanion))
        {
            Sync();
            return;
        }

        if (_window is null)
        {
            return;
        }

        if (e.PropertyName is nameof(MainViewModel.DesktopCompanionSize))
        {
            ApplyPlacement();
        }
    }

    private static DesktopBounds CurrentDesktopBounds()
    {
        return new DesktopBounds(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop, SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
    }
}
