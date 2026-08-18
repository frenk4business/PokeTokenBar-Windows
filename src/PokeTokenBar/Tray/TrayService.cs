using System.Drawing;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using PokeTokenBar.Core.Interfaces;
using PokeTokenBar.Services.Notifications;
using PokeTokenBar.ViewModels;
using PokeTokenBar.Views;
using Forms = System.Windows.Forms;

namespace PokeTokenBar.Tray;

public sealed class TrayService : IDisposable
{
    private readonly MainWindow _window;
    private readonly MainViewModel _viewModel;
    private readonly IAppLogger _logger;
    private readonly INotificationService _notificationService;
    private readonly Forms.NotifyIcon _notifyIcon;
    private Forms.ToolStripMenuItem? _refreshItem;
    private Forms.ToolStripMenuItem? _desktopCompanionItem;
    private Forms.ToolStripMenuItem? _launchWithWindowsItem;
    private Icon? _currentIcon;
    private bool _disposed;

    public TrayService(MainWindow window, MainViewModel viewModel, IAppLogger logger, INotificationService notificationService)
    {
        _window = window;
        _viewModel = viewModel;
        _logger = logger;
        _notificationService = notificationService;
        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = LoadAppIcon(),
            Text = "PokeTokenBar - Today: --",
            Visible = true,
            ContextMenuStrip = BuildContextMenu()
        };
        _currentIcon = _notifyIcon.Icon;

        _notifyIcon.MouseClick += OnMouseClick;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _notificationService.NotificationRequested += OnNotificationRequested;
        UpdateTooltip();
    }

    public void ShowWindow()
    {
        if (_window.WindowState == WindowState.Minimized)
        {
            _window.WindowState = WindowState.Normal;
        }

        _window.Show();
        _window.Activate();
    }

    public void HideWindow() => _window.Hide();

    public void ToggleWindow()
    {
        if (_window.IsVisible)
        {
            HideWindow();
        }
        else
        {
            ShowWindow();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _notifyIcon.MouseClick -= OnMouseClick;
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _notificationService.NotificationRequested -= OnNotificationRequested;
        _notifyIcon.Visible = false;
        if (_currentIcon is not null && !ReferenceEquals(_currentIcon, SystemIcons.Application))
        {
            _currentIcon.Dispose();
        }
        _notifyIcon.Dispose();
        _disposed = true;
    }

    private Forms.ContextMenuStrip BuildContextMenu()
    {
        var menu = new Forms.ContextMenuStrip();

        menu.Items.Add("Open", null, (_, _) => ShowWindow());
        _refreshItem = new Forms.ToolStripMenuItem("Refresh", null, (_, _) => _viewModel.RefreshCommand.Execute(null));
        menu.Items.Add(_refreshItem);
        menu.Items.Add("Settings", null, (_, _) =>
        {
            _viewModel.SelectSection("Settings");
            ShowWindow();
        });
        menu.Items.Add(new Forms.ToolStripSeparator());
        _desktopCompanionItem = new Forms.ToolStripMenuItem("Show Desktop Pokemon", null, (_, _) => _viewModel.ShowDesktopCompanion = !_viewModel.ShowDesktopCompanion);
        menu.Items.Add(_desktopCompanionItem);
        _launchWithWindowsItem = new Forms.ToolStripMenuItem("Launch with Windows", null, (_, _) => _viewModel.LaunchWithWindows = !_viewModel.LaunchWithWindows);
        menu.Items.Add(_launchWithWindowsItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, async (_, _) =>
        {
            await _logger.LogAsync(AppLogLevel.Information, "Application exit requested from tray.");
            System.Windows.Application.Current.Shutdown();
        });

        return menu;
    }

    private void OnMouseClick(object? sender, Forms.MouseEventArgs e)
    {
        if (e.Button == Forms.MouseButtons.Left)
        {
            ToggleWindow();
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.TodayTokens) or nameof(MainViewModel.StatusText) or nameof(MainViewModel.CompanionName) or nameof(MainViewModel.TrayTooltipText) or nameof(MainViewModel.IsRefreshing) or nameof(MainViewModel.LaunchWithWindows) or nameof(MainViewModel.ShowDesktopCompanion) or nameof(MainViewModel.CompanionSpritePath))
        {
            UpdateTooltip();
            UpdateMenuState();
            UpdateTrayIcon();
        }
    }

    private void UpdateTooltip()
    {
        _notifyIcon.Text = TruncateTooltip(_viewModel.TrayTooltipText.Replace('\n', ' '));
    }

    private void UpdateMenuState()
    {
        if (_refreshItem is not null)
        {
            _refreshItem.Enabled = !_viewModel.IsRefreshing;
        }

        if (_desktopCompanionItem is not null)
        {
            _desktopCompanionItem.Checked = _viewModel.ShowDesktopCompanion;
            _desktopCompanionItem.Text = _viewModel.ShowDesktopCompanion ? "Hide Desktop Pokemon" : "Show Desktop Pokemon";
        }

        if (_launchWithWindowsItem is not null)
        {
            _launchWithWindowsItem.Checked = _viewModel.LaunchWithWindows;
        }
    }

    private void OnNotificationRequested(object? sender, AppNotification notification)
    {
        var icon = notification.Kind == AppNotificationKind.Warning ? Forms.ToolTipIcon.Warning : Forms.ToolTipIcon.Info;
        _notifyIcon.ShowBalloonTip(5_000, notification.Title, notification.Message, icon);
    }

    private static string TruncateTooltip(string text) => text.Length <= 63 ? text : text[..63];

    private void UpdateTrayIcon()
    {
        var next = LoadCompanionIcon(_viewModel.CompanionSpritePath) ?? LoadAppIcon();
        var previous = _currentIcon;
        _notifyIcon.Icon = next;
        _currentIcon = next;
        if (previous is not null && !ReferenceEquals(previous, SystemIcons.Application))
        {
            previous.Dispose();
        }
    }

    private static Icon LoadAppIcon()
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Resources", "PokeTokenBar.ico");
        return File.Exists(iconPath) ? new Icon(iconPath) : SystemIcons.Application;
    }

    private static Icon? LoadCompanionIcon(string? spritePath)
    {
        if (string.IsNullOrWhiteSpace(spritePath) || !File.Exists(spritePath))
        {
            return null;
        }

        try
        {
            using var stream = new FileStream(spritePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var source = new Bitmap(stream);
            using var canvas = new Bitmap(32, 32);
            using (var graphics = Graphics.FromImage(canvas))
            {
                graphics.Clear(Color.Transparent);
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                var scale = Math.Min(30f / source.Width, 30f / source.Height);
                var width = Math.Max(1, source.Width * scale);
                var height = Math.Max(1, source.Height * scale);
                var x = (32 - width) / 2f;
                var y = (32 - height) / 2f;
                graphics.DrawImage(source, x, y, width, height);
            }

            var handle = canvas.GetHicon();
            try
            {
                return (Icon)Icon.FromHandle(handle).Clone();
            }
            finally
            {
                DestroyIcon(handle);
            }
        }
        catch
        {
            return null;
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
