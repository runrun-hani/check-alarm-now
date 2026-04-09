using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CheckAlarmNow.Core;

namespace CheckAlarmNow.ViewModels;

public partial class PetViewModel : ObservableObject
{
    private readonly AppSettings _settings;
    private int _dialogueCooldown;

    // 상태별 이미지 캐시
    private ImageSource? _defaultImage;
    private ImageSource? _idleImage;
    private ImageSource? _warnImage;
    private ImageSource? _alertImage;

    [ObservableProperty] private PetState _currentState = PetState.Idle;
    [ObservableProperty] private double _annoyanceLevel;
    [ObservableProperty] private double _shakeAmplitude;
    [ObservableProperty] private string _statusText = "zzZ...";
    [ObservableProperty] private ImageSource? _petImageSource;
    [ObservableProperty] private double _angryScale = 1.0;

    public PetViewModel(AppSettings settings)
    {
        _settings = settings;
        LoadAllImages();
    }

    /// <summary>
    /// AlertManager에서 호출하는 상태 업데이트 메서드
    /// </summary>
    public void UpdateState(PetState state, double annoyance)
    {
        var prevState = CurrentState;
        CurrentState = state;
        AnnoyanceLevel = annoyance;

        // ALERT 진입 시 사운드 재생
        if (state == PetState.Alert && prevState != PetState.Alert && _settings.SoundEnabled)
        {
            System.Media.SystemSounds.Exclamation.Play();
        }

        // 상태 전환 시 이미지 교체
        if (state != prevState)
            PetImageSource = GetImageForState(state);

        // 대사
        _dialogueCooldown--;
        if (state != prevState || _dialogueCooldown <= 0)
        {
            StatusText = Dialogue.GetLine(state, annoyance);
            _dialogueCooldown = state == PetState.Idle ? 8 : 5;
        }

        switch (state)
        {
            case PetState.Idle:
            case PetState.Happy:
                ShakeAmplitude = 0;
                AngryScale = 1.0;
                break;
            case PetState.Warn:
                ShakeAmplitude = 3;
                AngryScale = 1.0;
                break;
            case PetState.Alert:
                ShakeAmplitude = 8;
                AngryScale = 1.2;
                break;
        }
    }

    public void LoadAllImages()
    {
        _defaultImage = LoadImage(null, "pack://application:,,,/Assets/Default.png");
        _idleImage = LoadImage(_settings.IdleImagePath, "pack://application:,,,/Assets/Sleeping.png");
        _warnImage = LoadImage(_settings.WarnImagePath, null);
        _alertImage = LoadImage(_settings.AlertImagePath, "pack://application:,,,/Assets/WakeUp.png");

        PetImageSource = GetImageForState(CurrentState);
    }

    public void LoadPetImage() => LoadAllImages();

    private static ImageSource? LoadImage(string? path, string? fallbackPack)
    {
        if (!string.IsNullOrEmpty(path) && File.Exists(path))
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource = new Uri(path, UriKind.Absolute);
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        if (fallbackPack != null)
            return new BitmapImage(new Uri(fallbackPack));
        return null;
    }

    private ImageSource? GetImageForState(PetState state)
    {
        var stateImage = state switch
        {
            PetState.Idle => _idleImage,       // Sleeping.png
            PetState.Warn => _warnImage,       // 사용자 지정 또는 Default.png
            PetState.Alert => _alertImage,     // WakeUp.png
            PetState.Happy => _idleImage,      // 다시 잠들기
            _ => null
        };
        return stateImage ?? _defaultImage;
    }
}
