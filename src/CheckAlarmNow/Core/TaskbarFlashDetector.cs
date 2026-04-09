using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;

namespace CheckAlarmNow.Core;

/// <summary>
/// 태스크바에서 깜빡이는(주황색 강조) 앱을 감지합니다.
/// RegisterShellHookWindow + HSHELL_FLASH 사용.
/// </summary>
public class TaskbarFlashDetector
{
    private const int HSHELL_FLASH = 0x8006; // HSHELL_REDRAW | 0x8000

    private IntPtr _hwnd;
    private HwndSource? _source;
    private uint _shellHookMsg;

    /// <summary>앱이 깜빡임을 시작했을 때 발생. (표시이름, 프로세스명)</summary>
    public event Action<string, string>? AppFlashed;

    public void Start(Window owner)
    {
        var helper = new WindowInteropHelper(owner);
        _hwnd = helper.Handle;
        if (_hwnd == IntPtr.Zero) return;

        _shellHookMsg = RegisterWindowMessage("SHELLHOOK");
        RegisterShellHookWindow(_hwnd);

        _source = HwndSource.FromHwnd(_hwnd);
        _source?.AddHook(WndProc);
    }

    public void Stop()
    {
        if (_hwnd != IntPtr.Zero)
        {
            DeregisterShellHookWindow(_hwnd);
            _source?.RemoveHook(WndProc);
            _hwnd = IntPtr.Zero;
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (_shellHookMsg != 0 && (uint)msg == _shellHookMsg)
        {
            int code = wParam.ToInt32();
            IntPtr flashHwnd = lParam;

            if (code == HSHELL_FLASH && flashHwnd != IntPtr.Zero)
            {
                OnFlashDetected(flashHwnd);
            }
        }
        return IntPtr.Zero;
    }

    private void OnFlashDetected(IntPtr flashHwnd)
    {
        try
        {
            GetWindowThreadProcessId(flashHwnd, out uint pid);
            if (pid == 0) return;

            var proc = Process.GetProcessById((int)pid);

            // 표시 이름: FileDescription (예: "카카오톡") > 윈도우 타이틀 > 프로세스명
            string displayName = proc.ProcessName;
            try
            {
                var desc = proc.MainModule?.FileVersionInfo.FileDescription;
                if (!string.IsNullOrWhiteSpace(desc))
                    displayName = desc;
            }
            catch { }

            var processName = proc.ProcessName;
            proc.Dispose();

            AppFlashed?.Invoke(displayName, processName);
        }
        catch { }
    }

    #region P/Invoke

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern uint RegisterWindowMessage(string lpString);

    [DllImport("user32.dll")]
    private static extern bool RegisterShellHookWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool DeregisterShellHookWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    #endregion
}
