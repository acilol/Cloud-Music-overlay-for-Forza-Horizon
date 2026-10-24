using System.Runtime.InteropServices;
using HorizonRadioOverlay.Models;
using HorizonRadioOverlay.Services;

namespace HorizonRadioOverlay.Tests;

public sealed class GamepadInputServiceTests
{
    private const GamepadButton Previous = GamepadButton.LeftShoulder | GamepadButton.LeftThumb;
    private const GamepadButton Next = GamepadButton.LeftShoulder | GamepadButton.RightThumb;

    private static GamepadInputService StartInput()
    {
        var settings = new OverlaySettings();
        Assert.True(GamepadHotkeyParser.TryParse(settings.GamepadPrevHotkey, out var previous));
        Assert.True(GamepadHotkeyParser.TryParse(settings.GamepadNextHotkey, out var next));
        var input = new GamepadInputService { Enabled = true, PrevHotkey = previous, NextHotkey = next };
        input.Start();
        input.ProcessButtons(GamepadButton.None);
        return input;
    }

    [Fact]
    public void Stick_clicks_switch_tracks_once_per_press_while_LB_remains_held()
    {
        using var input = StartInput();
        int previous = 0, next = 0;
        input.PrevTriggered += (_, _) => previous++;
        input.NextTriggered += (_, _) => next++;

        input.ProcessButtons(GamepadButton.LeftShoulder | GamepadButton.DPadLeft);
        input.ProcessButtons(GamepadButton.LeftShoulder | GamepadButton.DPadRight);
        Assert.Equal(0, previous + next);

        input.ProcessButtons(Previous);
        input.ProcessButtons(Previous);
        Assert.Equal(1, previous);
        input.ProcessButtons(GamepadButton.LeftShoulder);
        input.ProcessButtons(Previous);
        Assert.Equal(2, previous);

        input.ProcessButtons(GamepadButton.LeftShoulder);
        input.ProcessButtons(Next);
        input.ProcessButtons(Next);
        Assert.Equal(1, next);
        input.ProcessButtons(GamepadButton.LeftShoulder);
        input.ProcessButtons(Next);
        Assert.Equal(2, next);
    }

    [Fact]
    public void Saving_settings_during_capture_does_not_resume_actions_or_overwrite_the_enabled_setting()
    {
        using var input = StartInput();
        int fired = 0;
        input.PrevTriggered += (_, _) => fired++;
        input.IsCapturingHotkey = true;
        Assert.True(input.Enabled);
        input.ProcessButtons(Previous);
        input.Enabled = true; // Saving the newly recorded binding reapplies the enabled setting.
        input.ProcessButtons(Previous);
        Assert.Equal(0, fired);

        input.IsCapturingHotkey = false; // Leaving the field, page, or window resumes listening.
        input.ProcessButtons(Previous); // Do not trigger the combination still held after recording.
        Assert.Equal(0, fired);
        input.ProcessButtons(GamepadButton.LeftShoulder);
        input.ProcessButtons(Previous);
        Assert.Equal(1, fired);
    }

    [Fact]
    public void Disabling_hotkeys_during_capture_remains_disabled_after_capture_ends()
    {
        using var input = StartInput();
        int fired = 0;
        input.NextTriggered += (_, _) => fired++;
        input.IsCapturingHotkey = true;
        input.Enabled = false;
        input.IsCapturingHotkey = false;
        input.ProcessButtons(GamepadButton.None);
        input.ProcessButtons(Next);
        Assert.False(input.Enabled);
        Assert.Equal(0, fired);

        input.Enabled = true;
        input.ProcessButtons(GamepadButton.None);
        input.ProcessButtons(Next);
        Assert.Equal(1, fired);
    }

    [Theory]
    [InlineData(GamepadButton.None)]
    [InlineData(Previous)]
    [InlineData(Next)]
    public void Xbox_emulation_owns_pressed_and_released_states_even_when_native_Sony_state_is_stale(GamepadButton xboxButtons)
    {
        var actual = GamepadInputService.ReadCurrentButtons(
            () => (true, xboxButtons),
            () => throw new InvalidOperationException("Native Sony input must not mask the virtual Xbox device."));

        Assert.Equal(xboxButtons, actual);
    }

    [Fact]
    public void Disconnecting_and_reconnecting_Xbox_emulation_switches_sources_without_a_sticky_Sony_flag()
    {
        bool xboxConnected = true;
        int fallbackReads = 0;
        GamepadButton Read() => GamepadInputService.ReadCurrentButtons(
            () => (xboxConnected, Next),
            () => { fallbackReads++; return Previous; });

        Assert.Equal(Next, Read());
        xboxConnected = false;
        Assert.Equal(Previous, Read());
        xboxConnected = true;
        Assert.Equal(Next, Read());
        Assert.Equal(1, fallbackReads);
    }

    [Fact]
    public void Raw_device_info_reserves_the_full_Win32_union_instead_of_only_the_HID_member()
    {
        Assert.Equal(32, Marshal.SizeOf<GamepadInputService.RawInputDeviceInfo>());
        Assert.Equal(new IntPtr(8), Marshal.OffsetOf<GamepadInputService.RawInputDeviceInfo>("Hid"));
    }
}
