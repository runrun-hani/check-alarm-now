using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using CheckAlarmNow.ViewModels;

namespace CheckAlarmNow.Views;

public partial class PetRenderer : UserControl
{
    private readonly DispatcherTimer _animTimer;
    private readonly Random _rand = new();
    private ScaleTransform? _growTransform;
    private int _blinkTick;

    public PetRenderer()
    {
        InitializeComponent();
        _animTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _animTimer.Tick += AnimatePet;
        _animTimer.Start();

        Unloaded += (_, _) => _animTimer.Stop();
    }

    private void AnimatePet(object? sender, EventArgs e)
    {
        if (DataContext is not PetViewModel vm) return;

        // 흔들림 애니메이션
        ShakeTransform.X = vm.ShakeAmplitude > 0.1
            ? (_rand.NextDouble() * 2 - 1) * vm.ShakeAmplitude
            : 0;

        // Alert 시 크기 증가
        _growTransform ??= new ScaleTransform(1, 1);
        _growTransform.ScaleX = vm.AngryScale;
        _growTransform.ScaleY = vm.AngryScale;
        GrowWrapper.LayoutTransform = _growTransform;

        // 상태별 색조
        _blinkTick++;
        switch (vm.CurrentState)
        {
            case Core.PetState.Warn:
                // 주황 고정 0.2
                ColorOverlay.Fill = new SolidColorBrush(Color.FromRgb(255, 165, 0));
                ColorOverlay.Opacity = 0.2;
                break;

            case Core.PetState.Alert:
                // 빨강 깜빡 0.2~0.5
                ColorOverlay.Fill = new SolidColorBrush(System.Windows.Media.Colors.Red);
                var blink = 0.2 + 0.3 * (0.5 + 0.5 * Math.Sin(_blinkTick * 0.5));
                ColorOverlay.Opacity = blink;
                break;

            case Core.PetState.Idle:
                // 수면 색조
                ColorOverlay.Fill = new SolidColorBrush(Color.FromRgb(100, 100, 150));
                ColorOverlay.Opacity = 0.3;
                break;

            default:
                ColorOverlay.Opacity = 0;
                break;
        }
    }
}
