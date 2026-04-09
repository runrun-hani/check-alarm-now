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

        // TrayIcon
        _trayManager = new TrayIconManager(_alertManager, petWindow, settings);

        petWindow.Show();
    }

    private void OnExit(object sender, ExitEventArgs e)
    {
        _alertManager?.Stop();
        _notificationMonitor?.Stop();
        _trayManager?.Dispose();
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
    }
}
