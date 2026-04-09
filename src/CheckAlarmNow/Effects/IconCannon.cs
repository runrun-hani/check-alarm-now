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
    private readonly List<IconProjectile> _projectiles = new();
    private readonly DispatcherTimer _animTimer;
    private readonly Random _rand = new();

    public bool IsActive => _projectiles.Count > 0;

    public IconCannon()
    {
        _animTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _animTimer.Tick += OnAnimate;
    }

    public void Fire(string appName, double petX, double petY, ImageSource? fallback)
    {
        var icon = GetAppIcon(appName) ?? fallback;
        if (icon == null) return;

        var area = SystemParameters.WorkArea;

        // 랜덤 목표 위치 (화면 내)
        var targetX = _rand.NextDouble() * (area.Width - 48);
        var targetY = _rand.NextDouble() * (area.Height - 100);

        var projectile = new IconProjectile(icon, petX, petY, targetX, targetY);
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
    }

    private void OnAnimate(object? sender, EventArgs e)
    {
        var toRemove = new List<IconProjectile>();

        foreach (var p in _projectiles)
        {
            if (p.IsStuck)
                continue;

            p.Vy += 0.3; // gravity
            p.X += p.Vx;
            p.Y += p.Vy;

            // 목표 근처 도달
            var dx = p.X - p.TargetX;
            var dy = p.Y - p.TargetY;
            if (Math.Sqrt(dx * dx + dy * dy) < 30 || p.Y > SystemParameters.WorkArea.Height)
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
            var procs = Process.GetProcessesByName(appName);
            if (procs.Length == 0)
            {
                // 부분 매칭 시도
                var all = Process.GetProcesses();
                var match = all.FirstOrDefault(p =>
                {
                    try { return p.ProcessName.Contains(appName, StringComparison.OrdinalIgnoreCase); }
                    catch { return false; }
                });
                if (match != null)
                {
                    procs = new[] { match };
                }
                foreach (var p in all.Where(p => p != match)) p.Dispose();
            }

            if (procs.Length == 0) return null;

            string? exePath = null;
            try { exePath = procs[0].MainModule?.FileName; } catch { }
            foreach (var p in procs) p.Dispose();

            if (string.IsNullOrEmpty(exePath)) return null;

            var shInfo = new SHFILEINFO();
            var result = SHGetFileInfo(exePath, 0, ref shInfo,
                (uint)Marshal.SizeOf<SHFILEINFO>(), SHGFI_ICON | SHGFI_SMALLICON);

            if (result == IntPtr.Zero || shInfo.hIcon == IntPtr.Zero)
                return null;

            var bmpSrc = Imaging.CreateBitmapSourceFromHIcon(
                shInfo.hIcon,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            bmpSrc.Freeze();

            DestroyIcon(shInfo.hIcon);
            return bmpSrc;
        }
        catch
        {
            return null;
        }
    }

    #region P/Invoke

    private const uint SHGFI_ICON = 0x000000100;
    private const uint SHGFI_SMALLICON = 0x000000001;

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
        public double TargetX { get; }
        public double TargetY { get; }
        public bool IsStuck { get; set; }

        public IconProjectile(ImageSource icon, double startX, double startY, double targetX, double targetY)
        {
            X = startX;
            Y = startY;
            TargetX = targetX;
            TargetY = targetY;

            // 초기 속도: 목표 방향으로 포물선
            var dx = targetX - startX;
            var dy = targetY - startY;
            var dist = Math.Sqrt(dx * dx + dy * dy);
            var speed = Math.Min(dist * 0.05, 15);
            var angle = Math.Atan2(dy, dx);
            Vx = Math.Cos(angle) * speed;
            Vy = Math.Sin(angle) * speed - 8; // 위로 쏘기

            _window = new Window
            {
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = System.Windows.Media.Brushes.Transparent,
                Topmost = true,
                ShowInTaskbar = false,
                Width = 32,
                Height = 32,
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

        public void Close()
        {
            _window.Close();
        }
    }
}
