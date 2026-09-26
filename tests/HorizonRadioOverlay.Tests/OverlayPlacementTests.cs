using System.Drawing;
using HorizonRadioOverlay.Services;

namespace HorizonRadioOverlay.Tests;

public sealed class OverlayPlacementTests
{
    [Fact]
    public void Position_and_percent_support_monitor_left_of_primary()
    {
        Rectangle monitor = new(-1920, -120, 1920, 1080);
        Size overlay = new(210, 198);

        Point position = OverlayPlacement.Position(monitor, overlay, 0.4, 0.6);
        (double left, double top) = OverlayPlacement.Percent(monitor, overlay, position);

        Assert.InRange(position.X, -1920, -210);
        Assert.InRange(position.Y, -120, 762);
        Assert.InRange(left, 0.399, 0.401);
        Assert.InRange(top, 0.599, 0.601);
    }

    [Fact]
    public void Drag_position_is_clamped_and_snaps_to_edges_on_secondary_monitor()
    {
        Rectangle monitor = new(2560, 0, 1920, 1080);
        Size overlay = new(210, 198);

        Assert.Equal(new Point(2560, 0),
            OverlayPlacement.ClampAndSnap(monitor, overlay, new Point(2569, -50)));
        Assert.Equal(new Point(4270, 882),
            OverlayPlacement.ClampAndSnap(monitor, overlay, new Point(5000, 1200)));
    }

    [Fact]
    public void Oversized_overlay_stays_at_monitor_origin()
    {
        Rectangle monitor = new(-800, 100, 800, 600);
        Size overlay = new(1000, 800);

        Assert.Equal(new Point(-800, 100), OverlayPlacement.Position(monitor, overlay, 1, 1));
        Assert.Equal(new Point(-800, 100),
            OverlayPlacement.ClampAndSnap(monitor, overlay, new Point(-900, 300)));
    }
}
