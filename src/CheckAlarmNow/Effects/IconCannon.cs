using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace CheckAlarmNow.Effects;

public class IconCannon
{
    private const int IconSize = 48;
    private const int Margin = 80;
    private const int TaskbarHeight = 60;
    private const int FlightDurationFrames = 90; // ~1.5초 비행 시간

    private readonly List<IconProjectile> _projectiles = new();
    private readonly DispatcherTimer _animTimer;
    private readonly Random _rand = new();
    private readonly Dictionary<string, ImageSource?> _iconCache = new();

    public bool IsActive => _projectiles.Count > 0;

    public IconCannon()
    {
        _animTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _animTimer.Tick += OnAnimate;
    }

    /// <summary>아이콘 1개를 발사합니다. 반복 호출하면 계속 추가됩니다.</summary>
    public void FireOne(string appName, double petX, double petY, ImageSource? fallback)
    {
        if (!_iconCache.TryGetValue(appName, out var icon))
        {
            icon = GetAppIcon(appName) ?? fallback;
            _iconCache[appName] = icon;
        }
        if (icon == null) return;
        var _cachedIcon = icon;

        var area = SystemParameters.WorkArea;
        var targetX = _rand.Next(Margin, (int)(area.Width - Margin));
        var targetY = _rand.Next(Margin, (int)(area.Height - TaskbarHeight - Margin));

        var projectile = new IconProjectile(_cachedIcon, petX, petY, targetX, targetY, _rand);
        _projectiles.Add(projectile);

        if (!_animTimer.IsEnabled)
            _animTimer.Start();
    }

    public void Clear()
    {
        _animTimer.Stop();
        foreach (var p in _projectiles)
            p.Close();
        _projectiles.Clear();
        _iconCache.Clear();
    }

    private void OnAnimate(object? sender, EventArgs e)
    {
        var area = SystemParameters.WorkArea;

        foreach (var p in _projectiles)
        {
            if (p.IsStuck)
                continue;

            p.FrameCount++;

            // 보간 기반: t=0→시작, t=1→목표, 포물선 아치
            double t = Math.Min(1.0, (double)p.FrameCount / FlightDurationFrames);
            double eased = t * t * (3 - 2 * t); // smoothstep
            p.X = p.StartX + (p.TargetX - p.StartX) * eased;
            p.Y = p.StartY + (p.TargetY - p.StartY) * eased
                   - p.ArcHeight * Math.Sin(Math.PI * t); // 위로 볼록한 아치

            if (t >= 1.0)
            {
                p.X = p.TargetX;
                p.Y = p.TargetY;
                p.IsStuck = true;
            }

            p.UpdatePosition();
        }

        if (_projectiles.All(p => p.IsStuck))
            _animTimer.Stop();
    }

    private static BitmapSource? GetAppIcon(string appName)
    {
        try
        {
            var all = Process.GetProcesses();
            var match = all.FirstOrDefault(p =>
            {
                try
                {
                    var name = p.ProcessName;
                    return name.Contains(appName, StringComparison.OrdinalIgnoreCase) ||
                           appName.Contains(name, StringComparison.OrdinalIgnoreCase);
                }
                catch { return false; }
            });

            string? exePath = null;
            if (match != null)
            {
                try { exePath = match.MainModule?.FileName; } catch { }
            }
            foreach (var p in all) p.Dispose();

            if (string.IsNullOrEmpty(exePath)) return null;

            var shInfo = new SHFILEINFO();
            var result = SHGetFileInfo(exePath, 0, ref shInfo,
                (uint)Marshal.SizeOf<SHFILEINFO>(), SHGFI_ICON | SHGFI_LARGEICON);

            if (result == IntPtr.Zero || shInfo.hIcon == IntPtr.Zero)
                return null;

            var bmpSrc = Imaging.CreateBitmapSourceFromHIcon(
                shInfo.hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            bmpSrc.Freeze();
            DestroyIcon(shInfo.hIcon);
            return bmpSrc;
        }
        catch { return null; }
    }

    #region P/Invoke

    private const uint SHGFI_ICON = 0x000000100;
    private const uint SHGFI_LARGEICON = 0x000000000;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes,
        ref SHFILEINFO psfi, uint cbSizeFileInfo, uint uFlags);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr handle);

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WS_EX_TRANSPARENT = 0x00000020;

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

    #endregion

    private class IconProjectile
    {
        private readonly Window _window;

        public double X { get; set; }
        public double Y { get; set; }
        public double StartX { get; }
        public double StartY { get; }
        public double TargetX { get; }
        public double TargetY { get; }
        public double ArcHeight { get; }
        public bool IsStuck { get; set; }
        public int FrameCount { get; set; }

        public IconProjectile(ImageSource icon, double startX, double startY,
            double targetX, double targetY, Random rand)
        {
            X = startX;
            Y = startY;
            StartX = startX;
            StartY = startY;
            TargetX = targetX;
            TargetY = targetY;
            // 아치 높이: 거리에 비례 (100~250px)
            var dist = Math.Sqrt((targetX - startX) * (targetX - startX) + (targetY - startY) * (targetY - startY));
            ArcHeight = Math.Max(100, Math.Min(250, dist * 0.3)) + rand.NextDouble() * 50;

            _window = new Window
            {
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = System.Windows.Media.Brushes.Transparent,
                Topmost = true,
                ShowInTaskbar = false,
                Width = IconSize,
                Height = IconSize,
                Left = X,
                Top = Y,
                Content = new System.Windows.Controls.Image
                {
                    Source = icon,
                    Stretch = Stretch.Uniform
                }
            };

            _window.SourceInitialized += (_, _) =>
            {
                var hwnd = new WindowInteropHelper(_window).Handle;
                var exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
                SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_NOACTIVATE | WS_EX_TRANSPARENT);
            };

            _window.Show();
        }

        public void UpdatePosition()
        {
            _window.Left = X;
            _window.Top = Y;
        }

        public void Close() => _window.Close();
    }
}
