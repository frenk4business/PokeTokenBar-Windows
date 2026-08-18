using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using PokeTokenBar.ViewModels;

namespace PokeTokenBar.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private bool _allowClose;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        UpdateWindowIcon();
    }

    public void AllowClose() => _allowClose = true;

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_allowClose)
        {
            return;
        }

        e.Cancel = true;
        Hide();
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        base.OnClosed(e);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.CompanionSpritePath))
        {
            UpdateWindowIcon();
        }
    }

    private void UpdateWindowIcon()
    {
        Icon = LoadImage(_viewModel.CompanionSpritePath) ?? LoadImage(Path.Combine(AppContext.BaseDirectory, "Resources", "PokeTokenBar.ico"));
    }

    private static BitmapSource? LoadImage(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(path, UriKind.Absolute);
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }
}
