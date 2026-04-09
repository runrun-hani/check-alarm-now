using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using CheckAlarmNow.Effects;
using CheckAlarmNow.Interop;
using CheckAlarmNow.ViewModels;

namespace CheckAlarmNow.Core;

public class AlertManager
{
    private readonly PetViewModel _viewModel;
    private readonly NotificationMonitor _monitor;
    private readonly Window _petWindow;
    private readonly AppSettings _settings;
    private readonly DispatcherTimer _timer;
    private readonly HashSet<uint> _snoozedIds = new();
    private readonly IconCannon _cannon;
    private PetState _lastState = PetState.Idle;
    private DateTime? _happyUntil;

    public event Action<PetState, double>? StateUpdated;

    public AlertManager(PetViewModel viewModel, NotificationMonitor monitor, Window petWindow, AppSettings settings)
    {
        _viewModel = viewModel;
        _monitor = monitor;
        _petWindow = petWindow;
        _settings = settings;
        _cannon = new IconCannon();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _timer.Tick += OnCheck;
    }

    public void Start() => _timer.Start();
    public void Stop()
    {
        _timer.Stop();
        _cannon.Clear();
    }

    public void Reset()
    {
        // 현재 알림을 모두 스누즈 + 대포 정리 + HAPPY 상태
        var unread = _monitor.GetUnread();
        foreach (var n in unread)
            _snoozedIds.Add(n.Id);
        _cannon.Clear();
        _happyUntil = DateTime.Now.AddSeconds(2);
        UpdateState(PetState.Happy, 0);
    }

    private void OnCheck(object? sender, EventArgs e)
    {
        // Happy 상태 유지 중이면 타임아웃까지 유지
        if (_happyUntil.HasValue)
        {
            if (DateTime.Now < _happyUntil.Value)
            {
                UpdateState(PetState.Happy, 0);
                return;
            }
            _happyUntil = null;
        }

        var unread = _monitor.GetUnread();

        // 센터에서 사라진 알림의 스누즈 ID 정리
        if (unread.Count > 0)
        {
            var currentIds = new HashSet<uint>(unread.Select(n => n.Id));
            _snoozedIds.RemoveWhere(id => !currentIds.Contains(id));
        }
        else
        {
            _snoozedIds.Clear();
        }

        // snoozed 제외 → active
        var active = unread.Where(n => !_snoozedIds.Contains(n.Id)).ToList();

        if (active.Count == 0)
        {
            _cannon.Clear();
            UpdateState(PetState.Idle, 0);
            return;
        }

        // 포커스 감지: 현재 포커스된 앱이 알림을 보낸 앱인지 확인
        var fgPid = NativeMethods.GetForegroundProcessId();
        if (fgPid != 0)
        {
            try
            {
                var fgProc = Process.GetProcessById((int)fgPid);
                var fgName = fgProc.ProcessName;
                fgProc.Dispose();

                var matched = active.Where(n =>
                    fgName.Contains(n.AppName, StringComparison.OrdinalIgnoreCase) ||
                    n.AppName.Contains(fgName, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (matched.Count > 0)
                {
                    foreach (var m in matched)
                        _snoozedIds.Add(m.Id);

                    _happyUntil = DateTime.Now.AddSeconds(2);
                    _cannon.Clear();
                    UpdateState(PetState.Happy, 0);
                    return;
                }
            }
            catch { }
        }

        // 가장 오래된 미확인 알림의 경과 시간
        var oldest = active.Min(n => n.FirstSeen);
        var elapsed = DateTime.Now - oldest;
        var patienceSeconds = _settings.PatienceMinutes * 60;

        if (patienceSeconds == 0 || elapsed.TotalSeconds >= patienceSeconds)
        {
            var divisor = patienceSeconds > 0 ? patienceSeconds * 2.0 : 60.0;
            var annoyance = Math.Min(1.0, (elapsed.TotalSeconds - patienceSeconds) / divisor);

            // Alert 진입 시 대포 발사 (1회만)
            if (!_cannon.IsActive)
            {
                var petX = _petWindow.Left + _petWindow.ActualWidth / 2;
                var petY = _petWindow.Top + _petWindow.ActualHeight / 2;
                var appName = active.First().AppName;
                _cannon.Fire(appName, petX, petY, _viewModel.PetImageSource);
            }

            UpdateState(PetState.Alert, annoyance);
        }
        else
        {
            _cannon.Clear();
            UpdateState(PetState.Warn, 0);
        }
    }

    private void UpdateState(PetState state, double annoyance)
    {
        _lastState = state;
        _viewModel.UpdateState(state, annoyance);
        StateUpdated?.Invoke(state, annoyance);
    }
}
