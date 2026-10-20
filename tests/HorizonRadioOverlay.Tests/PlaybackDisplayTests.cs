using HorizonRadioOverlay.ViewModels;

namespace HorizonRadioOverlay.Tests;

public sealed class PlaybackDisplayTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Successful_toggle_immediately_updates_glyph_and_accessible_action()
    {
        var vm = new NowPlayingViewModel { IsPlaying = true };
        var changed = new List<string?>();
        vm.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        vm.ConfirmPlaybackToggle(Start);

        Assert.False(vm.IsPlaying);
        Assert.Equal("\uE768", vm.PlaybackGlyph);
        Assert.Equal("播放", vm.PlaybackActionText);
        Assert.Contains(nameof(vm.PlaybackGlyph), changed);
        Assert.Contains(nameof(vm.PlaybackActionText), changed);

        vm.ConfirmPlaybackToggle(Start.AddMilliseconds(100));
        Assert.True(vm.IsPlaying);
        Assert.Equal("\uE769", vm.PlaybackGlyph);
        Assert.Equal("暂停", vm.PlaybackActionText);
    }

    [Fact]
    public void Stale_and_slow_player_samples_do_not_undo_a_just_sent_toggle()
    {
        var vm = new NowPlayingViewModel { IsPlaying = true };
        vm.ConfirmPlaybackToggle(Start);

        vm.UpdatePlaybackState(true, Start.AddMilliseconds(-100), Start.AddSeconds(3));
        Assert.False(vm.IsPlaying);

        vm.UpdatePlaybackState(true, Start.AddMilliseconds(100), Start.AddMilliseconds(100));
        Assert.False(vm.IsPlaying);

        // Once the player confirms the command, subsequent external controls apply immediately.
        vm.UpdatePlaybackState(false, Start.AddMilliseconds(500), Start.AddMilliseconds(500));
        vm.UpdatePlaybackState(true, Start.AddMilliseconds(600), Start.AddMilliseconds(600));
        Assert.True(vm.IsPlaying);
    }

    [Fact]
    public void Unhandled_shortcut_is_corrected_even_when_player_state_has_not_changed()
    {
        var vm = new NowPlayingViewModel { IsPlaying = true };
        vm.ConfirmPlaybackToggle(Start);

        vm.UpdatePlaybackState(true, Start.AddSeconds(2), Start.AddSeconds(2));

        Assert.True(vm.IsPlaying);
    }

    [Fact]
    public void Missing_timeline_does_not_claim_playback_is_paused()
    {
        var vm = new NowPlayingViewModel();
        vm.ConfirmPlaybackToggle(Start);

        vm.UpdatePlaybackState(null, Start.AddSeconds(3), Start.AddSeconds(3));
        vm.UpdatePlaybackState(false, null, Start.AddSeconds(3));

        Assert.True(vm.IsPlaying);
    }

    [Fact]
    public void Resetting_track_clears_pending_command_before_new_source_samples_arrive()
    {
        var vm = new NowPlayingViewModel();
        vm.ConfirmPlaybackToggle(Start);
        vm.ResetTrack();

        vm.UpdatePlaybackState(true, Start.AddMilliseconds(50), Start.AddMilliseconds(50));

        Assert.True(vm.IsPlaying);
    }

    [Fact]
    public void Timeline_displays_hours_without_wrapping_back_to_minutes()
    {
        var vm = new NowPlayingViewModel { PositionSeconds = 3672, DurationSeconds = 7200 };
        Assert.Equal("1:01:12", vm.ElapsedText);
        Assert.Equal("2:00:00", vm.DurationText);
        Assert.Equal(51, vm.ProgressPercent, 2);
    }

    [Fact]
    public void Clearing_lyrics_clears_the_previous_track_line()
    {
        var vm = new NowPlayingViewModel { LyricsPreview = "First line" };
        vm.LyricsPreview = "Second line";
        Assert.Equal("First line", vm.PreviousLyric);
        vm.LyricsPreview = UiText.LyricsPreviewPlaceholder;
        Assert.Empty(vm.PreviousLyric);
    }
}
