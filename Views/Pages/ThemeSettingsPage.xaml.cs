using System.Windows.Controls;

namespace HorizonRadioOverlay.Views.Pages;

public partial class ThemeSettingsPage : UserControl
{
    public ThemeSettingsPage()
    {
        InitializeComponent();
    }

    private void ThemeSettingsPage_SizeChanged(object sender, System.Windows.SizeChangedEventArgs e)
    {
        if (PageLayout is null || AppearanceEditor is null || AppearancePreview is null)
            return;

        bool stacked = e.NewSize.Width < 760;
        PreviewSurface.Height = Math.Clamp(e.NewSize.Height - 140, 180, 340);
        PageLayout.ColumnDefinitions[0].Width = new System.Windows.GridLength(stacked ? 1 : 1.08, System.Windows.GridUnitType.Star);
        PageLayout.ColumnDefinitions[1].Width = new System.Windows.GridLength(stacked ? 0 : 32);
        PageLayout.ColumnDefinitions[2].Width = new System.Windows.GridLength(stacked ? 0 : 0.92, stacked ? System.Windows.GridUnitType.Pixel : System.Windows.GridUnitType.Star);
        PageLayout.RowDefinitions[1].Height = new System.Windows.GridLength(stacked ? 24 : 0);

        Grid.SetColumn(AppearancePreview, stacked ? 0 : 2);
        Grid.SetRow(AppearancePreview, 0);
        Grid.SetRow(AppearanceEditor, stacked ? 2 : 0);
    }

    private void PreviewBackdrop_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (sender is RadioButton { Tag: string color })
            PreviewSurface.Background = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color));
    }
}
