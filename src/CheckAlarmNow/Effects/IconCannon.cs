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
    private const int MaxFlightFrames = 150; // ~2.5초 후 강제 착지

    private readonly List<IconProjectile> _projectiles = new();
    private readonly DispatcherTimer _animTimer;
    private readonly Random _rand = new();
    private ImageSource? _cachedIcon;

    public bool IsActive => _projectiles.Count > 0;

    public IconCannon()
    {
        _animTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _animTimer.Tick += OnAnimate;
    }

    /// <summary>아이콘 1개를 발사합니다. 반복 호출하면 계속 추가됩니다.</summary>
    public void FireOne(string appName, double petX, double petY, ImageSource? fallback)
    {
        _cachedIcon ??= GetAppIcon(appName) ?? fallback;
        if (_cachedIcon == null) return;

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
        _cachedIcon = null;
    }

    private void OnAnimate(object? sender, EventArgs e)
    {
        var area = SystemParameters.WorkArea;

        foreach (var p in _projectiles)
        {
            if (p.IsStuck)
                continue;

            p.FrameCount++;
            p.Vy += p.Gravity;
            p.X += p.Vx;
            p.Y += p.Vy;

            // 착지 조건: 목표 근처 도달 OR 시간 초과 OR 화면 밖
            var dx = p.X - p.TargetX;
            var dy = p.Y - p.TargetY;
            var dist = Math.Sqrt(dx * dx + dy * dy);

            bool shouldStick = dist < 30
                || p.FrameCount > MaxFlightFrames
                || p.Y > area.Height
                || p.X < -50 || p.X > area.Width + 50;

            if (shouldStick)
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
        public double Vx { get; set; }
        public double Vy { get; set; }
        public double Gravity { get; }
        public double TargetX { get; }
        public double TargetY { get; }
        public bool IsStuck { get; set; }
        public int FrameCount { get; set; }

        public IconProjectile(ImageSource icon, double startX, double startY,
            double targetX, double targetY, Random rand)
        {
            X = startX;
            Y = startY;
            TargetX = targetX;
            TargetY = targetY;

            var dx = targetX - startX;
            var dy = targetY - startY;
            var dist = Math.Sqrt(dx * dx + dy * dy);
            if (dist < 1) dist = 1;
            var speed = 8 + rand.NextDouble() * 6;
            Vx = dx / dist * speed;
            Vy = dy / dist * speed - (12 + rand.NextDouble() * 10);
            Gravity = 0.8 + rand.NextDouble() * 0.6;

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
