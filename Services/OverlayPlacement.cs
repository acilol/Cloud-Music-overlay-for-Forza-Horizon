using System.Drawing;
using System.Runtime.Versioning;
using System.Windows.Forms;

namespace HorizonRadioOverlay.Services;

[SupportedOSPlatform("windows")]
public static class OverlayPlacement
{
    public static Screen ResolveScreen(string? deviceName)
    {
        if (!string.IsNullOrWhiteSpace(deviceName))
        {
            Screen? selected = Screen.AllScreens.FirstOrDefault(screen =>
                string.Equals(screen.DeviceName, deviceName, StringComparison.OrdinalIgnoreCase));
            if (selected != null) return selected;
        }

        return Screen.PrimaryScreen ?? Screen.AllScreens[0];
    }

    public static Point Position(Rectangle bounds, Size windowSize, double leftPercent, double topPercent)
    {
        int availableWidth = Math.Max(0, bounds.Width - windowSize.Width);
        int availableHeight = Math.Max(0, bounds.Height - windowSize.Height);
        return new Point(
            bounds.Left + (int)Math.Round(availableWidth * Math.Clamp(leftPercent, 0, 1)),
            bounds.Top + (int)Math.Round(availableHeight * Math.Clamp(topPercent, 0, 1)));
    }

    public static (double Left, double Top) Percent(Rectangle bounds, Size windowSize, Point position)
    {
        int availableWidth = Math.Max(0, bounds.Width - windowSize.Width);
        int availableHeight = Math.Max(0, bounds.Height - windowSize.Height);
        return (
            availableWidth == 0 ? 0 : Math.Clamp((double)(position.X - bounds.Left) / availableWidth, 0, 1),
            availableHeight == 0 ? 0 : Math.Clamp((double)(position.Y - bounds.Top) / availableHeight, 0, 1));
    }

    public static Point ClampAndSnap(Rectangle bounds, Size windowSize, Point position, int snapThreshold = 16)
    {
        int maxX = Math.Max(bounds.Left, bounds.Right - windowSize.Width);
        int maxY = Math.Max(bounds.Top, bounds.Bottom - windowSize.Height);
        int x = Math.Clamp(position.X, bounds.Left, maxX);
        int y = Math.Clamp(position.Y, bounds.Top, maxY);
        return new Point(
            Snap(x, bounds.Left, maxX, snapThreshold),
            Snap(y, bounds.Top, maxY, snapThreshold));
    }

    private static int Snap(int value, int min, int max, int threshold)
    {
        foreach (int anchor in new[] { min, min + (max - min) / 2, max })
        {
            if (Math.Abs(value - anchor) < threshold) return anchor;
        }
        return value;
    }
}
