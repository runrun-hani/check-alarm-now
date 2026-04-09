using System.Diagnostics;
using System.Windows.Threading;
using Windows.UI.Notifications;
using Windows.UI.Notifications.Management;

namespace CheckAlarmNow.Core;

public record NotificationInfo(uint Id, string AppName, DateTime FirstSeen);

public class NotificationMonitor
{
    private readonly AppSettings _settings;
    private readonly Dictionary<uint, NotificationInfo> _unread = new();
    private readonly HashSet<string> _allAppNames = new();
    private readonly DispatcherTimer _timer;
    private UserNotificationListener? _listener;
    private bool _initialized;

    public NotificationMonitor(AppSettings settings)
    {
        _settings = settings;
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(settings.CheckIntervalSeconds)
        };
        _timer.Tick += OnTick;
    }

    public void Start()
    {
        try
        {
            _listener = UserNotificationListener.Current;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[NotificationMonitor] UserNotificationListener 초기화 실패: {ex.Message}");
        }
        _timer.Start();
    }

    public void Stop()
    {
        _timer.Stop();
    }

    public List<NotificationInfo> GetUnread()
    {
        lock (_unread)
        {
            return _unread.Values.ToList();
        }
    }

    public void RemoveNotification(uint id)
    {
        lock (_unread)
        {
            _unread.Remove(id);
        }
    }

    public List<string> GetAllAppNames()
    {
        lock (_allAppNames)
        {
            return _allAppNames.ToList();
        }
    }

    private async void OnTick(object? sender, EventArgs e)
    {
        if (_listener == null) return;

        try
        {
            var accessStatus = _listener.GetAccessStatus();
            if (accessStatus != UserNotificationListenerAccessStatus.Allowed)
            {
                Debug.WriteLine($"[NotificationMonitor] 알림 액세스 미허용: {accessStatus}");
                return;
            }

            var notifications = await _listener.GetNotificationsAsync(
                Windows.UI.Notifications.NotificationKinds.Toast);

            var currentIds = new HashSet<uint>();

            foreach (var notif in notifications)
            {
                var id = notif.Id;
                currentIds.Add(id);

                // 앱 이름 추출
                var appName = "Unknown";
                try
                {
                    var appInfo = notif.AppInfo;
                    appName = appInfo?.DisplayInfo?.DisplayName ?? "Unknown";
                }
                catch
                {
                    // AppInfo 접근 실패 시 기본값 사용
                }

                lock (_allAppNames)
                {
                    _allAppNames.Add(appName);
                }

                // 첫 체크 시 기존 알림은 무시
                if (!_initialized)
                    continue;

                // 모니터링 앱 필터링
                if (_settings.MonitoredApps.Count > 0)
                {
                    bool isMonitored = _settings.MonitoredApps.Any(
                        app => appName.Contains(app, StringComparison.OrdinalIgnoreCase));
                    if (!isMonitored)
                        continue;
                }

                lock (_unread)
                {
                    if (!_unread.ContainsKey(id))
                    {
                        _unread[id] = new NotificationInfo(id, appName, DateTime.Now);
                    }
                }
            }

            // 사라진 알림 제거 (사용자가 직접 해제한 경우)
            lock (_unread)
            {
                var removed = _unread.Keys.Where(k => !currentIds.Contains(k)).ToList();
                foreach (var k in removed)
                    _unread.Remove(k);
            }

            if (!_initialized)
                _initialized = true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[NotificationMonitor] 알림 조회 실패: {ex.Message}");
        }
    }
}
