using System.IO;
using System.Reflection;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows.Media.Imaging;
using System.Windows.Media.Animation;
using System.Windows.Navigation;
using HorizonRadioOverlay.Models;
using HorizonRadioOverlay.Services;
using HorizonRadioOverlay.ViewModels;
using HorizonRadioOverlay.Views.Pages;

namespace HorizonRadioOverlay;

public partial class MainWindow
{
    private void MinimizeWindow_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void MaximizeWindow_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void CloseWindow_Click(object sender, RoutedEventArgs e) => Close();

    private void WireShellControls()
    {
        NavigationListBox.SelectedIndex = 0;
        ShowPage(_shellViewModel.SelectedItem?.Key ?? "NowPlaying");
    }

    private void WirePageControls()
    {
        if (_nowPlayingPageView != null && !_nowPlayingPageWired)
        {
            _nowPlayingPageView.DataContext = _nowPlayingViewModel;
            _nowPlayingViewModel.PrevCommand = new CommunityToolkit.Mvvm.Input.AsyncRelayCommand(PrevAsync);
            _nowPlayingViewModel.PlayPauseCommand = new CommunityToolkit.Mvvm.Input.AsyncRelayCommand(TogglePlayPauseAsync);
            _nowPlayingViewModel.NextCommand = new CommunityToolkit.Mvvm.Input.AsyncRelayCommand(NextAsync);
            _nowPlayingViewModel.ToggleOverlayCommand = new CommunityToolkit.Mvvm.Input.AsyncRelayCommand(ToggleOverlayVisibilityAsync);
            _nowPlayingViewModel.RefreshCommand = new CommunityToolkit.Mvvm.Input.AsyncRelayCommand(async () => await RefreshCurrentTrackAsync(showOverlay: false, allowOverlayOnTrackChange: false));
            _nowPlayingViewModel.NavigateCommand = new CommunityToolkit.Mvvm.Input.RelayCommand<string>(key =>
            {
                if (!string.IsNullOrEmpty(key)) NavigateTo(key);
            });
            _nowPlayingPageWired = true;
        }

        if (_floatingSettingsPageView != null && !_floatingSettingsPageWired)
        {
            _floatingSettingsPageView.DataContext = _floatingSettingsViewModel;
            _floatingSettingsViewModel.SettingsChanged += () =>
            {
                if (_isInitializingOverlayControls) return;
                _placementSaveTimer.Stop();
                ApplyOverlaySettingsFromControls();
                _diagnostic.Enabled = _activeSettings.DiagnosticMode;
                _overlaySettingsService.Save(_activeSettings);
                _remoteControlService.ApplySettings(_activeSettings);
                UpdateRemoteControlPage();
                _ = ApplyPauseOverlayVisibilityRuleAsync();
            };
            _floatingSettingsViewModel.PlacementChanged += () =>
            {
                if (_isInitializingOverlayControls) return;
                ApplyOverlayPlacementFromControls();
            };
            _floatingSettingsViewModel.TrackSourceChanged += async () =>
            {
                if (_isInitializingOverlayControls) return;
                ApplyOverlaySettingsFromControls();
                _floatingSettingsViewModel.UpdateCapabilities(_playbackCoordinator);
                _smtcCoverRefreshCts?.Cancel();
                _smtcCoverRefreshCts = null;
                lock (_lyricGate)
                {
                    _lyricsService.Reset();
                    _lastSmtcPlaybackPositionSeconds = null;
                    _smtcLyricTimingController.Reset();
                    _neteaseLyricTimingController.Reset();
                }
                _lastTrackKey = string.Empty;
                _lastDisplayTrackKey = string.Empty;
                _lastPreviewCoverBytes = null;
                SetCover(null);
                _overlayWindow.SetLyrics(null);
                await RefreshCurrentTrackAsync(showOverlay: false, allowOverlayOnTrackChange: false);
                SetStatus("状态：数据来源已切换（点击“保存”可持久化）。", false);
            };
            _floatingSettingsViewModel.ToggleDragRepositionRequested += ToggleOverlayDragReposition;
            _overlayWindow.PositionDragged += (leftPercent, topPercent, monitorDeviceName) =>
            {
                Dispatcher.Invoke(() =>
                {
                    _floatingSettingsViewModel.UpdatePositionFromDrag(leftPercent, topPercent, monitorDeviceName);
                    _activeSettings.LeftPercent = leftPercent;
                    _activeSettings.TopPercent = topPercent;
                    _activeSettings.MonitorDeviceName = monitorDeviceName;
                    UpdateOverlayControlLabels();
                });
            };
            _overlayWindow.DragRepositionCompleted += () =>
            {
                Dispatcher.Invoke(() =>
                {
                    if (_floatingSettingsViewModel.IsPositionAdjusting)
                    {
                        ToggleOverlayDragReposition();
                    }
                });
            };
            _floatingSettingsPageWired = true;
        }

        if (_hotkeySettingsPageView != null && !_hotkeySettingsPageWired)
        {
            _hotkeySettingsPageView.DataContext = _hotkeySettingsViewModel;
            _hotkeySettingsViewModel.SettingsChanged += () =>
            {
                if (_isInitializingOverlayControls) return;
                ApplyHotkeySettings();
            };
            _hotkeySettingsPageWired = true;
            SetupHotkeyCaptureInputs();
        }

        if (_remoteControlPageView != null && !_remoteControlPageWired)
        {
            _remoteControlPageView.DataContext = _remoteControlViewModel;
            _remoteControlViewModel.SettingsChanged += () =>
            {
                if (_isInitializingOverlayControls) return;
                ApplyOverlaySettingsFromControls();
                _overlaySettingsService.Save(_activeSettings);
                SetStatus(_remoteControlService.IsRunning ? "状态：手机遥控已应用。" : $"状态：{_remoteControlService.StatusMessage}", !_remoteControlService.IsRunning && _activeSettings.EnableRemoteControl);
            };
            _remoteControlViewModel.RequestResetToken += () =>
            {
                _activeSettings.RemoteControlToken = RemoteControlService.GenerateToken();
                _overlaySettingsService.Save(_activeSettings);
                _remoteControlService.ApplySettings(_activeSettings);
                UpdateRemoteControlPage();
                SetStatus("状态：手机遥控连接令牌已重置。", false);
            };
            _remoteControlViewModel.StatusNotification += (msg, isErr) => SetStatus(msg, isErr);
            _remoteControlPageWired = true;
            UpdateRemoteControlPage();
        }

        if (_themeSettingsPageView != null && !_themeSettingsPageWired)
        {
            _themeSettingsPageView.DataContext = _themeSettingsViewModel;
            _themeSettingsViewModel.SettingsChanged += () =>
            {
                if (_isInitializingOverlayControls) return;
                ApplyOverlaySettingsFromControls();
            };
            _themeSettingsPageView.ThemeAccentIndigoButton.Click += (_, _) => ApplyThemeAccentColor("#635E89");
            _themeSettingsPageView.ThemeAccentBlueButton.Click += (_, _) => ApplyThemeAccentColor("#326BEE");
            _themeSettingsPageView.ThemeAccentGreenButton.Click += (_, _) => ApplyThemeAccentColor("#D7FF3F");
            _themeSettingsPageView.ThemeAccentAmberButton.Click += (_, _) => ApplyThemeAccentColor("#53625B");
            _themeSettingsPageView.ThemeAccentRoseButton.Click += (_, _) => ApplyThemeAccentColor("#AD526A");
            _themeSettingsPageView.MainColorPicker.ColorChanged += hex =>
            {
                _themeSettingsViewModel.ApplyColorFromPicker(hex);
            };
            _themeSettingsPageWired = true;
            ApplyDisplayColors(_activeSettings);
        }

        if (_logsPageView != null && !_logsPageWired)
        {
            _logsPageView.DataContext = _logsViewModel;
            _logsViewModel.StatusNotification += (msg, isErr) => SetStatus(msg, isErr);
            _logsPageWired = true;
            InitializeLogWatcher();
        }

        if (_aboutPageView != null && !_aboutPageWired)
        {
            _aboutPageView.DataContext = _aboutViewModel;
            _aboutViewModel.RequestCheckUpdate += () => CheckUpdate_Click(this, new RoutedEventArgs());
            _aboutViewModel.StatusNotification += (msg, isErr) => SetStatus(msg, isErr);
            _aboutPageWired = true;
        }
    }

    private void NavigateTo(string key)
    {
        NavigationItem? item = _shellViewModel.NavigationItems.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.Ordinal));
        if (item == null)
        {
            return;
        }

        _shellViewModel.SelectedItem = item;
        NavigationListBox.SelectedItem = item;
        ShowPage(item.Key);
    }

    public void SetPreviewPage(string key)
    {
        _suppressPageAnimation = true;
        try
        {
            NavigateTo(key);
            UpdateLayout();
        }
        finally
        {
            _suppressPageAnimation = false;
        }
    }

    private void ShowPage(string key)
    {
        FrameworkElement activePage = key switch
        {
            "FloatingSettings" => FloatingSettingsPageView,
            "Hotkeys" => HotkeySettingsPageView,
            "RemoteControl" => RemoteControlPageView,
            "Theme" => ThemeSettingsPageView,
            "Logs" => LogsPageView,
            "About" => AboutPageView,
            _ => NowPlayingPageView
        };

        WirePageControls();
        PageHost.Content = activePage;
        if (_suppressPageAnimation)
        {
            activePage.BeginAnimation(OpacityProperty, null);
            activePage.Opacity = 1;
        }
        else
        {
            RunPageFadeIn(activePage);
        }

        if (key == "Logs")
        {
            RefreshLogView();
        }
    }

    private static void RunPageFadeIn(UIElement element)
    {
        element.Opacity = 0;
        DoubleAnimation animation = new()
        {
            From = 0,
            To = 1,
            Duration = TimeSpan.FromMilliseconds(180)
        };
        element.BeginAnimation(OpacityProperty, animation, HandoffBehavior.SnapshotAndReplace);
    }

    private void NavigationListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_shellViewModel.SelectedItem != null)
        {
            ShowPage(_shellViewModel.SelectedItem.Key);
        }
    }

    private void ApplyAutoWindowSize()
    {
        var screen = SystemParameters.WorkArea;
        Width = Math.Max(MinWidth, Math.Min(1280, screen.Width * 0.85));
        Height = Math.Max(MinHeight, Math.Min(900, screen.Height * 0.85));
    }

    private void SetPreviewEffect(int level)
    {
        _previewEffectLevel = Math.Clamp(level, 0, 2);
        ApplyPreviewEffect();
    }

    private void ApplyPreviewEffect()
    {
        if (ThemeSettingsPageView?.ThemePreviewCard == null)
        {
            return;
        }

        double opacity = _previewEffectLevel switch
        {
            0 => 0.10,
            2 => 0.28,
            _ => 0.18
        };
        double blurRadius = _previewEffectLevel switch
        {
            0 => 10,
            2 => 24,
            _ => 16
        };
        double shadowDepth = _previewEffectLevel switch
        {
            0 => 2,
            2 => 8,
            _ => 4
        };

        ThemeSettingsPageView.ThemePreviewCard.Effect = new System.Windows.Media.Effects.DropShadowEffect
        {
            Color = Colors.Black,
            BlurRadius = blurRadius,
            ShadowDepth = shadowDepth,
            Opacity = opacity
        };
    }

    private void ApplyThemeAccentColor(string colorHex)
    {
        _themeSettingsViewModel.AccentColor = colorHex;
    }

    private void ApplyAccentPalette(string colorHex)
    {
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(colorHex);
            if (_themeSettingsPageView != null)
            {
                foreach (var swatch in new[] { _themeSettingsPageView.ThemeAccentGreenButton,
                    _themeSettingsPageView.ThemeAccentBlueButton, _themeSettingsPageView.ThemeAccentRoseButton,
                    _themeSettingsPageView.ThemeAccentIndigoButton, _themeSettingsPageView.ThemeAccentAmberButton })
                    swatch.IsChecked = swatch.Background is SolidColorBrush swatchBrush && swatchBrush.Color == color;
            }
            if (Application.Current.Resources["PrimaryBrush"] is SolidColorBrush existing && existing.Color == color) return;
            Application.Current.Resources["PrimaryBrush"] = new SolidColorBrush(color);
            bool isLight = color.R * 0.299 + color.G * 0.587 + color.B * 0.114 > 175;
            Application.Current.Resources["AccentTextBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(32, 35, 44) : Colors.White);
            Application.Current.Resources["AccentInkBrush"] = new SolidColorBrush(isLight
                ? Color.FromRgb((byte)(color.R * 0.4), (byte)(color.G * 0.4), (byte)(color.B * 0.4)) : color);
            Application.Current.Resources["AccentSoftBrush"] = new SolidColorBrush(Color.FromRgb(
                (byte)(color.R * 0.1 + 250 * 0.9), (byte)(color.G * 0.1 + 251 * 0.9), (byte)(color.B * 0.1 + 250 * 0.9)));
        }
        catch
        {
        }
    }

    private void UpdatePageMetaTexts()
    {
        bool useSmtc = IsSmtcSource();
        SidebarConnectionStateText.Text = useSmtc ? UiText.SidebarConnectedSmtc : UiText.SidebarConnectedNetease;
        SidebarConnectionSubText.Text = useSmtc ? "SMTC 媒体会话" : "CloudMusic(ProcessTitle)";
        ConnectionStatusText.Text = useSmtc ? UiText.Connected : UiText.SidebarConnectedNetease;
        ConnectionStatusSubText.Text = useSmtc ? "等待新的系统媒体会话。" : "等待网易云窗口标题刷新。";
        UpdatePlaybackHealthText(_playbackSession.LatestSnapshot);
    }

    private void InitializeLogWatcher()
    {
        try
        {
            string logPath = _diagnostic.LogFilePath;
            string directory = Path.GetDirectoryName(logPath) ?? AppDomain.CurrentDomain.BaseDirectory;
            string fileName = Path.GetFileName(logPath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            _logWatcher = new FileSystemWatcher(directory, fileName)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
                EnableRaisingEvents = true
            };
            _logWatcher.Changed += (_, _) => Dispatcher.InvokeAsync(RefreshLogView);
            _logWatcher.Created += (_, _) => Dispatcher.InvokeAsync(RefreshLogView);
            _logWatcher.Renamed += (_, _) => Dispatcher.InvokeAsync(RefreshLogView);
            RefreshLogView();
        }
        catch
        {
        }
    }

    private void RefreshLogView()
    {
        _logsViewModel.Refresh();
    }
}
