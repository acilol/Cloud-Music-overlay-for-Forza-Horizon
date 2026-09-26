using System.Windows.Controls;

namespace HorizonRadioOverlay.Views.Pages;

public partial class NowPlayingPage : UserControl
{
    public NowPlayingPage()
    {
        InitializeComponent();
    }

    private void NowPlayingPage_SizeChanged(object sender, System.Windows.SizeChangedEventArgs e)
    {
        if (AlbumSurface == null) return;
        bool compact = e.NewSize.Height < 440;
        ElapsedPanel.Visibility = compact ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;
        PreviousLyricLine.Visibility = compact ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;
        double size = Math.Clamp(Math.Min((e.NewSize.Width - 112) * 0.46, e.NewSize.Height - 86), 240, 440);
        RecordDisc.Width = size * 0.875;
        RecordDisc.Height = size * 0.875;
        CoverColumn.Width = new System.Windows.GridLength(size);
        AlbumSurface.Width = size;
        AlbumSurface.Height = size;
        ArtworkStage.Width = size;
        ArtworkStage.Height = size;
        AlbumClip.Rect = new System.Windows.Rect(0, 0, size, size);
    }
}
