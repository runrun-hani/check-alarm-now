using System.Threading;
using System.Windows;
using CheckAlarmNow.Core;
using CheckAlarmNow.TrayIcon;
using CheckAlarmNow.ViewModels;
using CheckAlarmNow.Views;

namespace CheckAlarmNow;

public partial class App : Application
{
    private Mutex? _mutex;
    private NotificationMonitor? _notificationMonitor;
    private AlertManager? _alertManager;
    private TrayIconManager? _trayManager;
    private TaskbarFlashDetector? _flashDetector;

    private void OnStartup(object sender, StartupEventArgs e)
    {
        // 단일 인스턴스 보장
        _mutex = new Mutex(true, "CheckAlarmNow_SingleInstance", out bool isNew);
        if (!isNew)
        {
            MessageBox.Show("알리미가 이미 실행 중입니다!", "CheckAlarmNow", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        var settings = AppSettings.Load();
        _settings = settings;

        // NotificationMonitor: WinRT 알림 감시
        _notificationMonitor = new NotificationMonitor(settings);
        _notificationMonitor.Start();

        // PetViewModel
        var viewModel = new PetViewModel(settings);

        // PetWindow
        var petWindow = new PetWindow(viewModel, settings);

        // AlertManager: 인내심 기반 상태 머신
        _alertManager = new AlertManager(viewModel, _notificationMonitor, petWindow, settings);
        _alertManager.Start();

        // PetWindow에 참조 주입
        petWindow.AlertManager = _alertManager;
        petWindow.Monitor = _notificationMonitor;

        // TrayIcon
        _trayManager = new TrayIconManager(_alertManager, _notificationMonitor, petWindow, settings);

        // TaskbarFlashDetector: 태스크바 깜빡임 감지
        _flashDetector = new TaskbarFlashDetector();
        _flashDetector.AppFlashed += (displayName, processName) =>
        {
            _notificationMonitor?.AddFlashNotification(displayName, processName);
        };
        petWindow.Loaded += (_, _) => _flashDetector.Start(petWindow);

        petWindow.Show();
    }

    private AppSettings? _settings;

    private void OnExit(object sender, ExitEventArgs e)
    {
        _flashDetector?.Stop();
        _alertManager?.Stop();
        _notificationMonitor?.Stop();
        _trayManager?.Dispose();
        _settings?.Save();
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
    }
}
