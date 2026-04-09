using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using CheckAlarmNow.Core;
using CheckAlarmNow.Views;
using DrawColor = System.Drawing.Color;

namespace CheckAlarmNow.TrayIcon;

public class TrayIconManager : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly AlertManager _alertManager;
    private readonly PetWindow _petWindow;
    private readonly AppSettings _settings;
    private readonly ContextMenuStrip _contextMenu;
    private Icon? _currentIcon;

    public TrayIconManager(AlertManager alertManager, PetWindow petWindow, AppSettings settings)
    {
        _alertManager = alertManager;
        _petWindow = petWindow;
        _settings = settings;
        _contextMenu = CreateContextMenu();

        _currentIcon = CreateIcon(DrawColor.Gray);
        _notifyIcon = new NotifyIcon
        {
            Icon = _currentIcon,
            Text = "알리미 - zzZ...",
            Visible = true,
            ContextMenuStrip = _contextMenu
        };

        _notifyIcon.DoubleClick += (_, _) =>
        {
            _petWindow.Show();
            _petWindow.Activate();
        };

        _alertManager.StateUpdated += OnStateUpdated;
    }

    private void OnStateUpdated(PetState state, double annoyance)
    {
        var (color, text) = state switch
        {
            PetState.Warn => (DrawColor.Orange, "알리미 - 알림 확인해주세요~"),
            PetState.Alert => (DrawColor.Red, "알리미 - 지금 확인하세요!"),
            PetState.Happy => (DrawColor.LightGreen, "알리미 - 확인 완료!"),
            _ => (DrawColor.Gray, "알리미 - zzZ...")
        };

        var oldIcon = _currentIcon;
        _currentIcon = CreateIcon(color);
        _notifyIcon.Icon = _currentIcon;
        _notifyIcon.Text = text.Length > 63 ? text[..63] : text;

        if (oldIcon != null)
        {
            DestroyIcon(oldIcon.Handle);
            oldIcon.Dispose();
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr handle);

    private static Icon CreateIcon(DrawColor color)
    {
        using var bmp = new Bitmap(16, 16);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var brush = new SolidBrush(color);
        g.FillEllipse(brush, 1, 1, 14, 14);
        g.DrawEllipse(Pens.Black, 1, 1, 14, 14);
        return Icon.FromHandle(bmp.GetHicon());
    }

    private ContextMenuStrip CreateContextMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("설정", null, (_, _) =>
        {
            var sw = new SettingsWindow(_settings) { Owner = _petWindow };
            if (sw.ShowDialog() == true)
                _petWindow.ApplySettingsFromTray();
        });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("종료", null, (_, _) =>
        {
            _settings.Save();
            Application.Current.Shutdown();
        });
        return menu;
    }

    public void Dispose()
    {
        _alertManager.StateUpdated -= OnStateUpdated;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _contextMenu.Dispose();
        if (_currentIcon != null)
        {
            DestroyIcon(_currentIcon.Handle);
            _currentIcon.Dispose();
        }
    }
}
