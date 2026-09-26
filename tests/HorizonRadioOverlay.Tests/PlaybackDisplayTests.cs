using HorizonRadioOverlay.ViewModels;

namespace HorizonRadioOverlay.Tests;

public sealed class PlaybackDisplayTests
{
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
