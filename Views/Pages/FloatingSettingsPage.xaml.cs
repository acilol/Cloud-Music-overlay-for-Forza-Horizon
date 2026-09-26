using System.Windows.Controls;

namespace HorizonRadioOverlay.Views.Pages;

public partial class FloatingSettingsPage : UserControl
{
    public FloatingSettingsPage()
    {
        InitializeComponent();
    }

    private void PreviewCanvas_SizeChanged(object sender, System.Windows.SizeChangedEventArgs e) => UpdatePreviewPlacement();

    private void FloatingSettingsPage_SizeChanged(object sender, System.Windows.SizeChangedEventArgs e)
    {
        if (PageLayout is null || PositionSection is null || BehaviorSection is null)
            return;

        bool stacked = e.NewSize.Width < 760;
        PositionPreviewSurface.Height = stacked ? 170 : 232;
        PageLayout.ColumnDefinitions[0].Width = new System.Windows.GridLength(stacked ? 1 : 1.1, System.Windows.GridUnitType.Star);
        PageLayout.ColumnDefinitions[1].Width = new System.Windows.GridLength(stacked ? 0 : 32);
        PageLayout.ColumnDefinitions[2].Width = new System.Windows.GridLength(stacked ? 0 : 0.9, stacked ? System.Windows.GridUnitType.Pixel : System.Windows.GridUnitType.Star);
        PageLayout.RowDefinitions[1].Height = new System.Windows.GridLength(stacked ? 24 : 0);
        Grid.SetColumn(BehaviorSection, stacked ? 0 : 2);
        Grid.SetRow(BehaviorSection, stacked ? 2 : 0);
    }

    private void PreviewSlider_ValueChanged(object sender, System.Windows.RoutedPropertyChangedEventArgs<double> e) => UpdatePreviewPlacement();

    private void UpdatePreviewPlacement()
    {
        if (PreviewCanvas is null || PreviewOverlay is null || PreviewScaleTransform is null ||
            HorizontalSlider is null || BottomOffsetSlider is null || ScaleSlider is null)
            return;

        double scale = Math.Clamp(ScaleSlider.Value / 100.0 * 0.55, 0.44, 0.99);
        PreviewScaleTransform.ScaleX = scale;
        PreviewScaleTransform.ScaleY = scale;

        double availableWidth = Math.Max(0, PreviewCanvas.ActualWidth - PreviewOverlay.Width * scale);
        double availableHeight = Math.Max(0, PreviewCanvas.ActualHeight - PreviewOverlay.Height * scale);
        Canvas.SetLeft(PreviewOverlay, availableWidth * HorizontalSlider.Value / 100.0);
        Canvas.SetTop(PreviewOverlay, availableHeight * BottomOffsetSlider.Value / 100.0);
    }
}
