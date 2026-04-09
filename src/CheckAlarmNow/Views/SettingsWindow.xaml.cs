using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using CheckAlarmNow.Core;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using Image = System.Windows.Controls.Image;

namespace CheckAlarmNow.Views;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;
    private string? _idlePath, _warnPath, _alertPath;
    private readonly List<string> _monitoredApps;

    private static readonly (string Name, int Minutes)[] PatiencePresets =
    {
        ("없음 (즉시)", 0),
        ("매우 낮음 (1분)", 1),
        ("낮음 (3분)", 3),
        ("보통 (5분)", 5),
        ("높음 (10분)", 10),
        ("매우 높음 (20분)", 20),
        ("사용자 지정", -1)
    };

    public SettingsWindow(AppSettings settings)
    {
        _settings = settings;
        InitializeComponent();

        // 인내심 프리셋
        int selectedIndex = 3; // 기본: 보통
        for (int i = 0; i < PatiencePresets.Length; i++)
        {
            PatienceCombo.Items.Add(new ComboBoxItem
            {
                Content = PatiencePresets[i].Name,
                Tag = PatiencePresets[i].Minutes
            });
            if (PatiencePresets[i].Minutes == settings.PatienceMinutes)
                selectedIndex = i;
        }

        // 사용자 지정이면 마지막 항목 선택
        bool isCustom = !PatiencePresets.Any(p => p.Minutes == settings.PatienceMinutes);
        if (isCustom)
            selectedIndex = PatiencePresets.Length - 1;

        PatienceCombo.SelectedIndex = selectedIndex;
        CustomMinutesBox.Text = settings.PatienceMinutes.ToString();

        SizeSlider.Value = settings.PetSize;
        UpdateSizeLabel(settings.PetSize);
        SoundCheck.IsChecked = settings.SoundEnabled;
        AutoStartCheck.IsChecked = AutoStartHelper.IsAutoStartEnabled();

        _idlePath = settings.IdleImagePath;
        _warnPath = settings.WarnImagePath;
        _alertPath = settings.AlertImagePath;

        _monitoredApps = new List<string>(settings.MonitoredApps);
        RefreshMonitoredList();
        RefreshPreviews();
    }

    private void RefreshPreviews()
    {
        SetPreview(PreviewIdle, _idlePath, "Sleeping.png");
        SetPreview(PreviewWarn, _warnPath, "Default.png");
        SetPreview(PreviewAlert, _alertPath, "WakeUp.png");
    }

    private static void SetPreview(Image img, string? path, string fallbackResource)
    {
        if (!string.IsNullOrEmpty(path) && File.Exists(path))
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource = new Uri(path, UriKind.Absolute);
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.EndInit();
            bmp.Freeze();
            img.Source = bmp;
        }
        else
        {
            img.Source = new BitmapImage(new Uri($"pack://application:,,,/Assets/{fallbackResource}"));
        }
    }

    private static string? PickImage()
    {
        var dlg = new OpenFileDialog
        {
            Title = "이미지 선택",
            Filter = "이미지 파일|*.png;*.jpg;*.jpeg;*.gif;*.bmp|모든 파일|*.*"
        };
        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }

    // 이미지 선택/초기화
    private void OnSelectIdleImage(object s, RoutedEventArgs e) { var p = PickImage(); if (p != null) { _idlePath = p; RefreshPreviews(); } }
    private void OnResetIdleImage(object s, RoutedEventArgs e) { _idlePath = null; RefreshPreviews(); }

    private void OnSelectWarnImage(object s, RoutedEventArgs e) { var p = PickImage(); if (p != null) { _warnPath = p; RefreshPreviews(); } }
    private void OnResetWarnImage(object s, RoutedEventArgs e) { _warnPath = null; RefreshPreviews(); }

    private void OnSelectAlertImage(object s, RoutedEventArgs e) { var p = PickImage(); if (p != null) { _alertPath = p; RefreshPreviews(); } }
    private void OnResetAlertImage(object s, RoutedEventArgs e) { _alertPath = null; RefreshPreviews(); }

    // 인내심 프리셋 변경
    private void OnPatienceChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PatienceCombo.SelectedItem is ComboBoxItem item && item.Tag is int minutes)
        {
            bool isCustom = minutes == -1;
            CustomMinutesBox.Visibility = isCustom ? Visibility.Visible : Visibility.Collapsed;
            MinuteLabel.Visibility = isCustom ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    // 감지된 앱 목록 새로고침 (SettingsWindow 열릴 때)
    public void RefreshDetectedApps(List<string> appNames)
    {
        DetectedAppsList.Items.Clear();
        foreach (var name in appNames.OrderBy(n => n))
        {
            if (!_monitoredApps.Contains(name))
                DetectedAppsList.Items.Add(name);
        }
    }

    private void RefreshMonitoredList()
    {
        MonitoredAppsList.Items.Clear();
        foreach (var app in _monitoredApps)
            MonitoredAppsList.Items.Add(app);
    }

    private void OnAddMonitoredApp(object sender, RoutedEventArgs e)
    {
        var selected = DetectedAppsList.SelectedItems.Cast<string>().ToList();
        foreach (var app in selected)
        {
            if (!_monitoredApps.Contains(app))
                _monitoredApps.Add(app);
        }
        RefreshMonitoredList();

        // 감지 목록에서 제거
        foreach (var app in selected)
            DetectedAppsList.Items.Remove(app);
    }

    private void OnRemoveMonitoredApp(object sender, RoutedEventArgs e)
    {
        var selected = MonitoredAppsList.SelectedItems.Cast<string>().ToList();
        foreach (var app in selected)
            _monitoredApps.Remove(app);
        RefreshMonitoredList();
    }

    private void OnAddManualApp(object sender, RoutedEventArgs e)
    {
        var name = ManualAppBox.Text.Trim();
        if (!string.IsNullOrEmpty(name) && !_monitoredApps.Contains(name))
        {
            _monitoredApps.Add(name);
            RefreshMonitoredList();
            ManualAppBox.Clear();
        }
    }

    private static readonly Dictionary<int, string> SizeLabels = new()
    {
        { 1, "매우 작게" }, { 2, "작게" }, { 3, "보통" }, { 4, "크게" }, { 5, "매우 크게" }
    };

    private void OnSizeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        UpdateSizeLabel(e.NewValue);
    }

    private void UpdateSizeLabel(double value)
    {
        if (SizeLabelText != null)
            SizeLabelText.Text = SizeLabels.GetValueOrDefault((int)Math.Round(value), "보통");
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        // 인내심
        if (PatienceCombo.SelectedItem is ComboBoxItem item && item.Tag is int minutes)
        {
            if (minutes == -1)
            {
                if (int.TryParse(CustomMinutesBox.Text, out int custom) && custom >= 0)
                    _settings.PatienceMinutes = custom;
            }
            else
            {
                _settings.PatienceMinutes = minutes;
            }
        }

        _settings.PetSize = SizeSlider.Value;
        _settings.IdleImagePath = _idlePath;
        _settings.WarnImagePath = _warnPath;
        _settings.AlertImagePath = _alertPath;
        _settings.SoundEnabled = SoundCheck.IsChecked == true;
        _settings.MonitoredApps = new List<string>(_monitoredApps);

        _settings.StartWithWindows = AutoStartCheck.IsChecked == true;
        AutoStartHelper.SetAutoStart(_settings.StartWithWindows);

        _settings.Save();
        DialogResult = true;
    }
}
