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
    private const int CrackSize = 80; // 균열 이펙트 크기
    private const int Margin = 80;
    private const int TaskbarHeight = 60;
    private const double Speed = 45; // px/frame — 매우 빠름

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

    public void FireOne(string appName, double petX, double petY, ImageSource? fallback)
    {
        if (!_iconCache.TryGetValue(appName, out var icon))
        {
            icon = GetAppIcon(appName) ?? fallback;
            _iconCache[appName] = icon;
        }
        if (icon == null) return;

        var area = SystemParameters.WorkArea;
        var targetX = _rand.Next(Margin, (int)(area.Width - Margin));
        var targetY = _rand.Next(Margin, (int)(area.Height - TaskbarHeight - Margin));

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
        _iconCache.Clear();
    }

    private void OnAnimate(object? sender, EventArgs e)
    {
        foreach (var p in _projectiles)
        {
            if (p.IsStuck)
                continue;

            // 직선 이동: 목표를 향해 고속으로
            var dx = p.TargetX - p.X;
            var dy = p.TargetY - p.Y;
            var dist = Math.Sqrt(dx * dx + dy * dy);

            if (dist <= Speed)
            {
                // 도착 → 우뚝 멈춤 + 균열 이펙트
                p.X = p.TargetX;
                p.Y = p.TargetY;
                p.IsStuck = true;
                p.UpdatePosition();
                p.ShowCrack();
            }
            else
            {
                p.X += dx / dist * Speed;
                p.Y += dy / dist * Speed;
                p.UpdatePosition();
            }
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
        private readonly Window _iconWindow;
        private Window? _crackWindow;

        public double X { get; set; }
        public double Y { get; set; }
        public double TargetX { get; }
        public double TargetY { get; }
        public bool IsStuck { get; set; }

        public IconProjectile(ImageSource icon, double startX, double startY,
            double targetX, double targetY)
        {
            X = startX;
            Y = startY;
            TargetX = targetX;
            TargetY = targetY;

            _iconWindow = CreateWindow(IconSize, IconSize, startX, startY);
            _iconWindow.Content = new System.Windows.Controls.Image
            {
                Source = icon,
                Stretch = Stretch.Uniform
            };
            _iconWindow.Show();
        }

        public void ShowCrack()
        {
            // 균열 이펙트: Crack.png 이미지를 아이콘 앞에 표시
            _crackWindow = CreateWindow(CrackSize, CrackSize,
                X - (CrackSize - IconSize) / 2.0,
                Y - (CrackSize - IconSize) / 2.0);

            var crackImage = new System.Windows.Controls.Image
            {
                Source = new BitmapImage(new Uri("pack://application:,,,/Assets/Crack.png")),
                Width = CrackSize,
                Height = CrackSize,
                Stretch = Stretch.Uniform,
                RenderTransformOrigin = new System.Windows.Point(0.5, 0.5),
                // 랜덤 회전: 반복이 티 안 나게
                RenderTransform = new RotateTransform(new Random().NextDouble() * 360),
            };

            _crackWindow.Content = crackImage;
            _crackWindow.Show();
        }

        public void UpdatePosition()
        {
            _iconWindow.Left = X;
            _iconWindow.Top = Y;
        }

        public void Close()
        {
            _iconWindow.Close();
            _crackWindow?.Close();
        }

        private static Window CreateWindow(int w, int h, double left, double top)
        {
            var win = new Window
            {
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = System.Windows.Media.Brushes.Transparent,
                Topmost = true,
                ShowInTaskbar = false,
                Width = w,
                Height = h,
                Left = left,
                Top = top,
            };
            win.SourceInitialized += (_, _) =>
            {
                var hwnd = new WindowInteropHelper(win).Handle;
                var exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
                SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_NOACTIVATE | WS_EX_TRANSPARENT);
            };
            return win;
        }
    }
}
