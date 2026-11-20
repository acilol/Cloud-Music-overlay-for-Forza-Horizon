using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using HorizonRadioOverlay.Models;
using HorizonRadioOverlay.Services;
using HorizonRadioOverlay.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace HorizonRadioOverlay.Tests;

[CollectionDefinition("Playback UI", DisableParallelization = true)]
public sealed class PlaybackUiCollection : ICollectionFixture<PlaybackUiHost>;

public sealed class PlaybackUiHost : IDisposable
{
    private readonly Thread _thread;
    private readonly TaskCompletionSource<Dispatcher> _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public PlaybackUiHost()
    {
        _thread = new Thread(() =>
        {
            try
            {
                var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                foreach (string name in new[] { "MaterialTheme", "Controls", "Cards" })
                    app.Resources.MergedDictionaries.Add(new ResourceDictionary
                    {
                        Source = new Uri($"pack://application:,,,/HorizonRadioOverlay;component/Styles/{name}.xaml")
                    });
                _ready.SetResult(app.Dispatcher);
                app.Run();
            }
            catch (Exception ex) { _ready.TrySetException(ex); }
        }) { IsBackground = true };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
    }

    public async Task Run(Func<Task> action)
    {
        Dispatcher dispatcher = await _ready.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await dispatcher.InvokeAsync(action).Task.Unwrap().WaitAsync(TimeSpan.FromSeconds(15));
    }

    public void Dispose()
    {
        if (_ready.Task.IsCompletedSuccessfully)
            _ready.Task.Result.Invoke(() => Application.Current.Shutdown());
        _thread.Join(TimeSpan.FromSeconds(5));
    }
}

[Collection("Playback UI")]
public sealed class PlaybackUiIntegrationTests(PlaybackUiHost host)
{
    [Fact]
    public Task New_status_and_error_text_reaches_the_status_bar() => host.Run(() =>
    {
        using var context = new Context();
        Invoke(context.Window, "SetStatus", "before", false);
        Invoke(context.Window, "SetStatusIfChanged", "after", true);
        Assert.Equal("after", ((TextBlock)context.Window.FindName("StatusText")).Text);
        Invoke(context.Window, "SetStatus", "recovered", false);
        Assert.Equal("recovered", ((TextBlock)context.Window.FindName("StatusText")).Text);
        return Task.CompletedTask;
    });

    [Fact]
    public Task Unchanged_track_retries_failed_lyrics_without_refetching_on_every_tick() => host.Run(async () =>
    {
        using var context = new Context(enableLyrics: true);
        context.Source.Track = Track("Song", Cover(255, 0, 0));
        await context.Poll();
        Assert.Equal(1, context.LyricSearches);
        Assert.False(context.Lyrics.HasLyrics);
        await context.Poll();
        Assert.Equal(1, context.LyricSearches);
        context.LyricClock += 10000;
        await context.Poll();
        Assert.Equal(2, context.LyricSearches);
        Assert.True(context.Lyrics.HasLyrics);
        Assert.Equal("Recovered lyric", context.ViewModel.LyricsPreview);
        await context.Poll();
        Assert.Equal(2, context.LyricSearches);
    });

    [Fact]
    public Task Disappearing_player_clears_persistent_overlay_and_pending_track() => host.Run(async () =>
    {
        using var context = new Context();
        context.Source.Track = Track("Song", Cover(255, 0, 0));
        await context.Poll();
        await Until(() => context.Overlay.IsContentVisible);
        await context.Overlay.ShowTrackAsync(Track("Queued song", Cover(0, 0, 255)));
        context.Source.Track = null;
        await context.Poll();
        await Task.Delay(350);
        Assert.False(context.Overlay.IsContentVisible);
        Assert.Empty(((TextBlock)context.Overlay.FindName("TitleText")).Text);
        Assert.Null(((Image)context.Overlay.FindName("CoverImage")).Source);
    });

    [Fact]
    public Task Paused_track_stays_hidden_and_resuming_reveals_current_track() => host.Run(async () =>
    {
        using var context = new Context(smtc: true, hideWhenPaused: true);
        context.Source.Playing = false;
        context.Source.Track = Track("Paused song", Cover(255, 0, 0));
        await context.Poll();
        await context.Poll();
        await Task.Delay(350);
        Assert.False(context.Overlay.IsContentVisible);
        var toggle = (Task<PlaybackCommandResult>)Invoke(context.Window, "ToggleOverlayVisibilityAsync")!;
        Assert.False((await toggle).Succeeded);
        Assert.False(context.Overlay.IsContentVisible);

        context.Source.Playing = true;
        await context.Poll();
        await Until(() => context.Overlay.IsContentVisible);
        Assert.Equal("Paused song", ((TextBlock)context.Overlay.FindName("TitleText")).Text);

        context.Source.Playing = false;
        context.Source.Track = Track("Another paused song", Cover(0, 0, 255));
        await context.Poll();
        await Task.Delay(350);
        Assert.False(context.Overlay.IsContentVisible);
    });

    [Fact]
    public Task Smtc_late_cover_updates_both_displays_without_clearing_on_missing_sample() => host.Run(async () =>
    {
        using var context = new Context(smtc: true);
        context.Source.Track = Track("Song", Cover(255, 0, 0));
        await context.Poll();
        await Until(() => context.Overlay.IsContentVisible);
        context.Source.Track = Track("Song", Cover(0, 0, 255));
        await context.Poll();
        AssertBlue(context.ViewModel.CoverImage!);
        AssertBlue((BitmapSource)((Image)context.Overlay.FindName("CoverImage")).Source);
        context.Source.Track = Track("Song", null);
        await context.Poll();
        AssertBlue(context.ViewModel.CoverImage!);
    });

    private static void AssertBlue(BitmapSource image)
    {
        byte[] pixel = new byte[4];
        image.CopyPixels(new Int32Rect(0, 0, 1, 1), pixel, 4, 0);
        Assert.Equal(255, pixel[0]);
        Assert.Equal(0, pixel[2]);
    }

    private static async Task Until(Func<bool> ready)
    {
        for (int i = 0; i < 300 && !ready(); i++) await Task.Delay(10);
        Assert.True(ready(), "Overlay did not reach the expected state.");
    }

    private static TrackInfo Track(string name, byte[]? cover) => new()
    {
        Name = name, Artist = "Test artist", SourceAppId = "test", CoverBytes = cover, DurationSeconds = 180
    };

    private static byte[] Cover(byte r, byte g, byte b)
    {
        var bitmap = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[] { b, g, r, 255 }, 4);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    private static object? Invoke(object instance, string method, params object?[] args) => instance.GetType()
        .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
        .Single(m => m.Name == method && m.GetParameters().Length == args.Length).Invoke(instance, args);

    private sealed class Context : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "HorizonReview-" + Guid.NewGuid().ToString("N"));
        private readonly ServiceProvider _provider;
        public readonly FakeSource Source;
        public readonly PlaybackSessionService Session;
        public readonly MainWindow Window;
        public readonly OverlayWindow Overlay;
        public readonly NowPlayingViewModel ViewModel;
        public readonly LyricsService Lyrics;
        public int LyricSearches;
        public long LyricClock = 10000;

        public Context(bool smtc = false, bool enableLyrics = false, bool hideWhenPaused = false)
        {
            var netease = new FakeSource(PlaybackSourceIds.Netease);
            var system = new FakeSource(PlaybackSourceIds.Smtc);
            Source = smtc ? system : netease;
            var coordinator = new PlaybackCoordinator([netease, system]);
            var diagnostic = new DiagnosticService(Path.Combine(_directory, "diagnostic.log"));
            Session = new PlaybackSessionService(coordinator, diagnostic);
            Lyrics = new LyricsService((_, _, _, _) =>
            {
                LyricSearches++;
                IReadOnlyList<LyricsService.SongSearchCandidate> candidates = LyricSearches == 1
                    ? [] : [new("1", 100, "Song", "Test artist", "", 180)];
                return Task.FromResult(candidates);
            }, _ => Task.FromResult("""{"lrc":{"lyric":"[00:00.00]Recovered lyric"}}"""), () => LyricClock);
            var settings = new OverlaySettingsService(Path.Combine(_directory, "settings.json"));
            settings.Save(new OverlaySettings
            {
                TrackSource = Source.Id, EnableLyrics = enableLyrics, EnableNeteaseMemoryTimeline = false,
                AlwaysShowOverlay = true, HideOverlayWhenPaused = hideWhenPaused, EnableRemoteControl = false
            });
            var services = new ServiceCollection().AddAppServices();
            services.AddSingleton(coordinator);
            services.AddSingleton(Session);
            services.AddSingleton(diagnostic);
            services.AddSingleton(Lyrics);
            services.AddSingleton(settings);
            _provider = services.BuildServiceProvider();
            Window = ActivatorUtilities.CreateInstance<MainWindow>(_provider, false);
            Overlay = _provider.GetRequiredService<OverlayWindow>();
            Overlay.Opacity = 0;
            ViewModel = _provider.GetRequiredService<NowPlayingViewModel>();
        }

        public async Task Poll()
        {
            var snapshot = await Session.RefreshAsync(true, true);
            await (Task)Invoke(Window, "ApplyPlaybackSnapshotAsync", snapshot)!;
        }

        public void Dispose()
        {
            typeof(MainWindow).GetField("_isRealExit", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(Window, true);
            Window.Close();
            Overlay.Close();
            _provider.Dispose();
            Session.Dispose();
            Lyrics.Dispose();
            Directory.Delete(_directory, true);
        }
    }

    private sealed class FakeSource(string id) : IPlaybackSource
    {
        public TrackInfo? Track;
        public bool Playing = true;
        public string Id => id;
        public string DisplayName => id;
        public bool IsAvailable => true;
        public PlaybackSourceCapabilities Capabilities => PlaybackSourceCapabilities.TrackMetadata | PlaybackSourceCapabilities.PlaybackState | PlaybackSourceCapabilities.Lyrics;
        public Task<TrackInfo?> GetCurrentTrackAsync() => Task.FromResult(Track);
        public Task<(TimeSpan Position, bool IsPlaying)?> GetPlaybackStateAsync() => Task.FromResult<(TimeSpan, bool)?>((TimeSpan.Zero, Playing));
        public Task<bool> ExecuteAsync(PlaybackCommand command, PlaybackControlBindings bindings) => Task.FromResult(false);
    }
}
