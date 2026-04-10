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
    private readonly Dictionary<uint, NotificationInfo> _flashUnread = new();
    private readonly HashSet<string> _allAppNames = new();
    private readonly HashSet<uint> _snoozedIds = new();  // 읽은 알림을 추적하여 제외
    private readonly DispatcherTimer _timer;
    private UserNotificationListener? _listener;
    private bool _accessGranted;
    private bool _initialized;
    private int _initTicks;  // 초기화 딜레이를 위한 tick 카운터

    public NotificationMonitor(AppSettings settings)
    {
        _settings = settings;
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(settings.CheckIntervalSeconds)
        };
        _timer.Tick += OnTick;
    }

    public async void Start()
    {
        try
        {
            _listener = UserNotificationListener.Current;
            var access = await _listener.RequestAccessAsync();
            if (access != UserNotificationListenerAccessStatus.Allowed)
            {
                Debug.WriteLine($"알림 접근 거부: {access}");
                return;
            }
            _accessGranted = true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"초기화 실패: {ex.Message}");
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
        lock (_flashUnread)
        {
            // _snoozedIds에 있는 알림 제외
            var unreadNotifs = _unread.Values.Where(n => !_snoozedIds.Contains(n.Id)).ToList();
            var flashNotifs = _flashUnread.Values.Where(n => !_snoozedIds.Contains(n.Id)).ToList();
            return unreadNotifs.Concat(flashNotifs).ToList();
        }
    }

    /// <summary>태스크바 깜빡임으로 감지된 앱을 알림으로 추가.</summary>
    public void AddFlashNotification(string displayName, string processName)
    {
        // 초기화 전(시작 직후) flash는 기존 깜빡임이므로 무시
        if (!_initialized)
        {
            lock (_allAppNames) { _allAppNames.Add(displayName); }
            return;
        }

        uint syntheticId = (uint)(processName.GetHashCode() & 0x7FFFFFFF) | 0x80000000;

        // 모니터링 앱 필터링
        if (_settings.MonitoredApps.Count > 0)
        {
            bool isMonitored = _settings.MonitoredApps.Any(app =>
                displayName.Contains(app, StringComparison.OrdinalIgnoreCase) ||
                processName.Contains(app, StringComparison.OrdinalIgnoreCase) ||
                app.Contains(displayName, StringComparison.OrdinalIgnoreCase) ||
                app.Contains(processName, StringComparison.OrdinalIgnoreCase));
            if (!isMonitored)
            {
                // 목록에는 표시하되 알림은 추가하지 않음
                lock (_allAppNames) { _allAppNames.Add(displayName); }
                return;
            }
        }

        lock (_flashUnread)
        {
            if (!_flashUnread.ContainsKey(syntheticId))
                _flashUnread[syntheticId] = new NotificationInfo(syntheticId, displayName, DateTime.Now);
        }
        lock (_allAppNames) { _allAppNames.Add(displayName); }
    }

    /// <summary>알림을 읽은 상태로 표시 (스누즈).</summary>
    public void MarkAsRead(uint id)
    {
        _snoozedIds.Add(id);
    }

    /// <summary>Flash 알림을 읽은 상태로 표시 (syntheticId 기반).</summary>
    public void MarkFlashAsRead(uint syntheticId)
    {
        _snoozedIds.Add(syntheticId);
        lock (_flashUnread)
        {
            _flashUnread.Remove(syntheticId);
        }
    }

    /// <summary>확인된 flash 알림을 제거.</summary>
    public void RemoveFlashNotification(string appName)
    {
        lock (_flashUnread)
        {
            var toRemove = _flashUnread.Where(kv =>
                kv.Value.AppName.Contains(appName, StringComparison.OrdinalIgnoreCase) ||
                appName.Contains(kv.Value.AppName, StringComparison.OrdinalIgnoreCase))
                .Select(kv => kv.Key).ToList();
            foreach (var k in toRemove)
                _flashUnread.Remove(k);
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
        if (_listener == null || !_accessGranted) return;

        try
        {
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

            // 초기화 딜레이: 2번의 OnTick 후 _initialized 설정
            // 이를 통해 앱 시작 시 기존 알림을 확실히 무시할 수 있음
            if (!_initialized)
            {
                _initTicks++;
                if (_initTicks >= 2)
                    _initialized = true;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[NotificationMonitor] 알림 조회 실패: {ex.Message}");
        }
    }
}
