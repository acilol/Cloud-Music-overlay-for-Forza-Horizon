using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using HorizonRadioOverlay.Models;

namespace HorizonRadioOverlay.Tests;

public sealed class OverlayRenderingTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public Task Cancelling_cover_transition_keeps_latest_track_and_cover_visible(bool repeatNewTrack) => RunSta(async () =>
    {
        var window = new OverlayWindow { Opacity = 0 };
        try
        {
            window.ApplySettings(new OverlaySettings { EnableCoverWingEffect = true, AlwaysShowOverlay = true });
            var first = new TrackInfo { Name = "A", Artist = "Test", CoverBytes = Cover(255, 0, 0) };
            var second = new TrackInfo { Name = "B", Artist = "Test", CoverBytes = Cover(0, 0, 255) };
            await window.ShowTrackAsync(first);
            await Until(() => window.IsContentVisible);
            var root = (Grid)window.FindName("OverlayRoot");
            root.BeginAnimation(UIElement.OpacityProperty, null);
            root.Opacity = 1;
            var originalBrush = Field<ImageBrush>(window, "_coverFlowCenterBrush");

            await window.ShowTrackAsync(second);
            await Until(() => originalBrush.HasAnimatedProperties);
            var latest = repeatNewTrack ? second : first;
            await window.ShowTrackAsync(latest);
            await Task.Delay(600);

            Assert.Equal(latest.Name, ((TextBlock)window.FindName("TitleText")).Text);
            var cover = (BitmapSource)((Image)window.FindName("CoverImage")).Source;
            byte[] pixel = new byte[4];
            cover.CopyPixels(new Int32Rect(0, 0, 1, 1), pixel, 4, 0);
            Assert.Equal(repeatNewTrack ? 255 : 0, pixel[0]);
            Assert.Equal(repeatNewTrack ? 0 : 255, pixel[2]);
            Assert.Equal(1, Field<ImageBrush>(window, "_coverFlowCenterBrush").Opacity, 3);
        }
        finally { window.Close(); }
    });

    [Theory]
    [InlineData(19)]
    [InlineData(24)]
    [InlineData(32)]
    public Task Short_titles_keep_the_configured_font_size(double fontSize) => RunSta(() =>
    {
        var window = new OverlayWindow();
        try
        {
            window.ApplySettings(new OverlaySettings { TitleFontSize = fontSize });
            var title = (TextBlock)window.FindName("TitleText");
            title.Text = "AB";
            var root = (Grid)window.FindName("OverlayRoot");
            root.Measure(new Size(root.Width, root.Height));
            root.Arrange(new Rect(0, 0, root.Width, root.Height));
            root.UpdateLayout();
            var transform = title.TransformToAncestor(root);
            double scale = transform.Transform(new Point(0, 1)).Y - transform.Transform(new Point(0, 0)).Y;
            Assert.Equal(1, scale, 3);
        }
        finally { window.Close(); }
        return Task.CompletedTask;
    });

    private static T Field<T>(object instance, string name) => (T)instance.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(instance)!;

    private static async Task Until(Func<bool> ready)
    {
        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (!ready())
        {
            Assert.True(DateTime.UtcNow < deadline, "The overlay did not reach the expected state.");
            await Task.Delay(5);
        }
    }

    private static byte[] Cover(byte r, byte g, byte b)
    {
        var bitmap = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[] { b, g, r, 255 }, 4);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    private static Task RunSta(Func<Task> action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            dispatcher.BeginInvoke(async () =>
            {
                try { await action(); completion.TrySetResult(); }
                catch (Exception ex) { completion.TrySetException(ex); }
                finally { dispatcher.BeginInvokeShutdown(DispatcherPriority.Background); }
            });
            Dispatcher.Run();
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }
}
