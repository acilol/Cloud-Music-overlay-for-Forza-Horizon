using System.Runtime.Versioning;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HorizonRadioOverlay.Models;

namespace HorizonRadioOverlay.ViewModels;

[SupportedOSPlatform("windows")]
public sealed partial class NowPlayingViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title = UiText.NoTrackDetected;

    [ObservableProperty]
    private string _artist = UiText.PleasePlayMedia;

    [ObservableProperty]
    private string _sourceText = UiText.SourceUnknown;

    [ObservableProperty]
    private BitmapImage? _coverImage;

    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private string _sourceLabel = "等待连接";

    [ObservableProperty]
    private double _positionSeconds;

    [ObservableProperty]
    private double _durationSeconds;

    [ObservableProperty]
    private string _previousLyric = string.Empty;

    public double ProgressPercent => DurationSeconds > 0 ? Math.Clamp(PositionSeconds / DurationSeconds * 100, 0, 100) : 0;
    public string ElapsedText => FormatTime(PositionSeconds);
    public string DurationText => DurationSeconds > 0 ? FormatTime(DurationSeconds) : "--:--";

    private static string FormatTime(double seconds)
    {
        var time = TimeSpan.FromSeconds(double.IsFinite(seconds) ? Math.Clamp(seconds, 0, 359999) : 0);
        return time.ToString(time.TotalHours >= 1 ? @"h\:mm\:ss" : @"m\:ss");
    }

    partial void OnPositionSecondsChanged(double value)
    {
        OnPropertyChanged(nameof(ProgressPercent));
        OnPropertyChanged(nameof(ElapsedText));
    }

    partial void OnDurationSecondsChanged(double value)
    {
        OnPropertyChanged(nameof(ProgressPercent));
        OnPropertyChanged(nameof(DurationText));
    }

    partial void OnLyricsPreviewChanging(string value)
    {
        if (value == UiText.LyricsPreviewPlaceholder) PreviousLyric = string.Empty;
        else if (LyricsPreview != UiText.LyricsPreviewPlaceholder && value != LyricsPreview)
            PreviousLyric = LyricsPreview;
    }

    public string PlaybackGlyph => IsPlaying ? "\uE769" : "\uE768";

    partial void OnIsPlayingChanged(bool value) => OnPropertyChanged(nameof(PlaybackGlyph));

    [ObservableProperty]
    private string _lyricsPreview = UiText.LyricsPreviewPlaceholder;

    [ObservableProperty]
    private string _connectionStatus = "正在连接播放器";

    [ObservableProperty]
    private string _connectionStatusDetail = "等待首个播放器数据快照。";

    public IAsyncRelayCommand? PrevCommand { get; set; }
    public IAsyncRelayCommand? PlayPauseCommand { get; set; }
    public IAsyncRelayCommand? NextCommand { get; set; }
    public IAsyncRelayCommand? RefreshCommand { get; set; }
    public IAsyncRelayCommand? ToggleOverlayCommand { get; set; }
    public IRelayCommand<string>? NavigateCommand { get; set; }

    public void ResetTrack()
    {
        Title = UiText.NoTrackDetected;
        Artist = UiText.PleasePlayMedia;
        SourceText = UiText.SourceUnknown;
        CoverImage = null;
        IsPlaying = false;
        SourceLabel = "等待连接";
        LyricsPreview = UiText.LyricsPreviewPlaceholder;
    }
}
