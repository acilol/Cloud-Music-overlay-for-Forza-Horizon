using System.Collections.ObjectModel;
using System.Runtime.Versioning;
using CommunityToolkit.Mvvm.ComponentModel;
using HorizonRadioOverlay.Models;
using HorizonRadioOverlay.Services;

namespace HorizonRadioOverlay.ViewModels;

public sealed class TrackSourceOption
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public bool IsEnabled { get; set; } = true;
    public string? ToolTip { get; set; }

    public override string ToString() => DisplayName;
}

public sealed class MonitorOption
{
    public required string DeviceName { get; init; }
    public required string DisplayName { get; init; }

    public override string ToString() => DisplayName;
}

[SupportedOSPlatform("windows")]
public sealed partial class FloatingSettingsViewModel : ObservableObject
{
    public ObservableCollection<TrackSourceOption> TrackSources { get; } = [];
    public ObservableCollection<MonitorOption> Monitors { get; } = [];

    [ObservableProperty]
    private TrackSourceOption? _selectedTrackSource;

    [ObservableProperty]
    private MonitorOption? _selectedMonitor;

    [ObservableProperty]
    private double _horizontalPercent;

    [ObservableProperty]
    private double _bottomOffsetPercent;

    [ObservableProperty]
    private double _scalePercent = 100.0;

    [ObservableProperty]
    private bool _enableLyrics = true;

    [ObservableProperty]
    private bool _enableNeteaseMemoryTimeline = true;

    [ObservableProperty]
    private bool _alwaysShowOverlay;

    [ObservableProperty]
    private bool _hideOverlayWhenPaused;

    [ObservableProperty]
    private bool _enableCoverWingEffect;

    [ObservableProperty]
    private bool _minimizeToTray = true;

    [ObservableProperty]
    private bool _autoStartOnBoot;

    [ObservableProperty]
    private bool _diagnosticMode;

    [ObservableProperty]
    private bool _isPositionAdjusting;

    [ObservableProperty]
    private bool _isNeteaseTimelineEnabled = true;

    [ObservableProperty]
    private bool _isLyricsOptionEnabled = true;

    [ObservableProperty]
    private string? _lyricsOptionToolTip;

    [ObservableProperty]
    private string? _neteaseTimelineToolTip;

    public string HorizontalText => $"{HorizontalPercent:0}%";
    public string BottomOffsetText => $"{BottomOffsetPercent:0}%";
    public string ScaleText => $"{ScalePercent:0}%";
    public string AdjustPositionButtonText => IsPositionAdjusting ? UiText.FinishDragReposition : UiText.StartDragReposition;

    public event Action? SettingsChanged;
    public event Action? PlacementChanged;
    public event Action? TrackSourceChanged;
    public event Action? ToggleDragRepositionRequested;

    private bool _suppressNotification;

    partial void OnIsPositionAdjustingChanged(bool value)
    {
        OnPropertyChanged(nameof(AdjustPositionButtonText));
    }

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private void ToggleDragReposition()
    {
        ToggleDragRepositionRequested?.Invoke();
    }

    public void UpdatePositionFromDrag(double leftPercent, double topPercent, string monitorDeviceName)
    {
        _suppressNotification = true;
        try
        {
            HorizontalPercent = leftPercent * 100.0;
            BottomOffsetPercent = topPercent * 100.0;
            if (SelectedMonitor?.DeviceName != monitorDeviceName)
            {
                RefreshMonitors(monitorDeviceName);
            }
            OnPropertyChanged(nameof(HorizontalText));
            OnPropertyChanged(nameof(BottomOffsetText));
        }
        finally
        {
            _suppressNotification = false;
        }
    }

    partial void OnHorizontalPercentChanged(double value)
    {
        OnPropertyChanged(nameof(HorizontalText));
        NotifyPlacementChanged();
    }

    partial void OnBottomOffsetPercentChanged(double value)
    {
        OnPropertyChanged(nameof(BottomOffsetText));
        NotifyPlacementChanged();
    }

    partial void OnScalePercentChanged(double value)
    {
        OnPropertyChanged(nameof(ScaleText));
        NotifyPlacementChanged();
    }

    partial void OnSelectedMonitorChanged(MonitorOption? value) => NotifyPlacementChanged();

    partial void OnEnableLyricsChanged(bool value) => NotifySettingsChanged();
    partial void OnEnableNeteaseMemoryTimelineChanged(bool value) => NotifySettingsChanged();
    partial void OnAlwaysShowOverlayChanged(bool value) => NotifySettingsChanged();
    partial void OnHideOverlayWhenPausedChanged(bool value) => NotifySettingsChanged();
    partial void OnEnableCoverWingEffectChanged(bool value) => NotifySettingsChanged();
    partial void OnMinimizeToTrayChanged(bool value) => NotifySettingsChanged();
    partial void OnAutoStartOnBootChanged(bool value) => NotifySettingsChanged();
    partial void OnDiagnosticModeChanged(bool value) => NotifySettingsChanged();

    partial void OnSelectedTrackSourceChanged(TrackSourceOption? value)
    {
        if (_suppressNotification) return;
        TrackSourceChanged?.Invoke();
        NotifySettingsChanged();
    }

    private void NotifySettingsChanged()
    {
        if (_suppressNotification) return;
        SettingsChanged?.Invoke();
    }

    private void NotifyPlacementChanged()
    {
        if (_suppressNotification) return;
        PlacementChanged?.Invoke();
    }

    public void RefreshMonitors(string? selectedDeviceName = null)
    {
        bool wasSuppressed = _suppressNotification;
        _suppressNotification = true;
        try
        {
            string desiredName = selectedDeviceName ?? SelectedMonitor?.DeviceName ?? string.Empty;
            Monitors.Clear();
            Monitors.Add(new MonitorOption { DeviceName = string.Empty, DisplayName = "主显示器（自动）" });
            int index = 1;
            foreach (System.Windows.Forms.Screen screen in System.Windows.Forms.Screen.AllScreens)
            {
                Monitors.Add(new MonitorOption
                {
                    DeviceName = screen.DeviceName,
                    DisplayName = $"显示器 {index++}  ·  {screen.Bounds.Width} × {screen.Bounds.Height}{(screen.Primary ? "  ·  主屏" : string.Empty)}"
                });
            }

            MonitorOption? selected = Monitors.FirstOrDefault(m =>
                string.Equals(m.DeviceName, desiredName, StringComparison.OrdinalIgnoreCase));
            if (selected == null)
            {
                selected = new MonitorOption
                {
                    DeviceName = desiredName,
                    DisplayName = $"未连接的显示器（临时显示在主屏）"
                };
                Monitors.Add(selected);
            }
            SelectedMonitor = selected;
        }
        finally
        {
            _suppressNotification = wasSuppressed;
        }
    }

    public void LoadFrom(OverlaySettings settings, PlaybackCoordinator coordinator)
    {
        _suppressNotification = true;
        try
        {
            TrackSources.Clear();
            TrackSourceOption? toSelect = null;

            foreach (IPlaybackSource source in coordinator.Sources
                         .OrderBy(s => string.Equals(s.Id, PlaybackSourceIds.Netease, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                         .ThenBy(s => s.DisplayName, StringComparer.CurrentCulture))
            {
                var option = new TrackSourceOption
                {
                    Id = source.Id,
                    DisplayName = PlaybackCoordinator.IsSmtcSource(source.Id) ? "系统媒体（SMTC）" : "网易云音乐",
                    IsEnabled = source.IsAvailable,
                    ToolTip = source.IsAvailable ? source.DisplayName : "当前系统不支持此播放器来源"
                };
                TrackSources.Add(option);

                if (string.Equals(source.Id, settings.TrackSource, StringComparison.OrdinalIgnoreCase))
                {
                    toSelect = option;
                }
            }

            SelectedTrackSource = toSelect ?? TrackSources.FirstOrDefault();
            RefreshMonitors(settings.MonitorDeviceName);

            HorizontalPercent = settings.LeftPercent * 100.0;
            BottomOffsetPercent = settings.TopPercent * 100.0;
            ScalePercent = settings.Scale * 100.0;

            EnableLyrics = settings.EnableLyrics;
            EnableNeteaseMemoryTimeline = settings.EnableNeteaseMemoryTimeline;
            AlwaysShowOverlay = settings.AlwaysShowOverlay;
            HideOverlayWhenPaused = settings.HideOverlayWhenPaused;
            EnableCoverWingEffect = settings.EnableCoverWingEffect;
            MinimizeToTray = settings.MinimizeToTray;
            AutoStartOnBoot = settings.AutoStartOnBoot;
            DiagnosticMode = settings.DiagnosticMode;

            UpdateCapabilities(coordinator);
        }
        finally
        {
            _suppressNotification = false;
        }
    }

    public void UpdateCapabilities(PlaybackCoordinator coordinator)
    {
        string currentSourceId = SelectedTrackSource?.Id ?? PlaybackSourceIds.Netease;
        bool isSmtc = PlaybackCoordinator.IsSmtcSource(currentSourceId);

        IsLyricsOptionEnabled = coordinator.GetCapabilities(currentSourceId).HasFlag(PlaybackSourceCapabilities.Lyrics);
        LyricsOptionToolTip = TrackSourcePolicy.GetLyricsTooltip(isSmtc);

        IsNeteaseTimelineEnabled = !isSmtc;
        NeteaseTimelineToolTip = isSmtc
            ? "该选项仅用于网易云专用渠道，SMTC 渠道不会访问网易云进程"
            : "只读扫描网易云进程时间轴，不使用 SMTC；关闭后歌词使用本地计时降级";
    }

    public void ApplyTo(OverlaySettings settings)
    {
        if (SelectedTrackSource != null)
        {
            settings.TrackSource = SelectedTrackSource.Id;
        }

        settings.LeftPercent = HorizontalPercent / 100.0;
        settings.TopPercent = BottomOffsetPercent / 100.0;
        settings.Scale = ScalePercent / 100.0;
        settings.MonitorDeviceName = SelectedMonitor?.DeviceName ?? string.Empty;

        settings.EnableLyrics = EnableLyrics;
        settings.EnableNeteaseMemoryTimeline = EnableNeteaseMemoryTimeline;
        settings.AlwaysShowOverlay = AlwaysShowOverlay;
        settings.HideOverlayWhenPaused = HideOverlayWhenPaused;
        settings.EnableCoverWingEffect = EnableCoverWingEffect;
        settings.MinimizeToTray = MinimizeToTray;
        settings.AutoStartOnBoot = AutoStartOnBoot;
        settings.DiagnosticMode = DiagnosticMode;
    }
}
