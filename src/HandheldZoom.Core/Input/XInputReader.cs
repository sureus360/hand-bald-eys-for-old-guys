using System.ComponentModel;
using System.Runtime.InteropServices;

namespace HandheldZoom.Core.Input;

[Flags]
public enum GamepadButtons : ushort
{
    Up = 1, Down = 2, Left = 4, Right = 8,
    Start = 0x10, Back = 0x20, LS = 0x40, RS = 0x80,
    LB = 0x100, RB = 0x200, LT = 0x400, RT = 0x800, A = 0x1000, B = 0x2000, X = 0x4000, Y = 0x8000
}

public readonly record struct GamepadSnapshot(bool Connected, GamepadButtons Buttons,
    short LeftX = 0, short LeftY = 0, short RightX = 0, short RightY = 0);

public static class GamepadChord
{
    public static GamepadButtons Parse(string value)
    {
        var result = (GamepadButtons)0;
        foreach (var name in value.Split('+', StringSplitOptions.TrimEntries))
        {
            if (!Enum.GetNames<GamepadButtons>().Contains(name, StringComparer.OrdinalIgnoreCase) || !Enum.TryParse<GamepadButtons>(name, true, out var button) || !Enum.IsDefined(button) || (result & button) != 0)
                throw new ArgumentException($"Ungültige oder doppelte Controller-Taste: {name}. Erlaubt: LB, RB, LT, RT, A, B, X, Y, Start, Back, LS, RS, Up, Down, Left, Right.");
            result |= button;
        }
        if (result == 0) throw new ArgumentException("Mindestens eine Controller-Taste wählen.");
        return result;
    }
}

public sealed class XInputReader
{
    private readonly uint _index;
    private long _nextReconnect;
    public XInputReader(int index)
    {
        if (index is < 0 or > 3) throw new ArgumentOutOfRangeException(nameof(index), "Controller-Index: 0 bis 3.");
        _index = (uint)index;
    }
    public GamepadSnapshot Read()
    {
        if (Environment.TickCount64 < _nextReconnect) return default;
        var error = XInputGetState(_index, out var state);
        if (error == 1167) { _nextReconnect = Environment.TickCount64 + 1000; return default; }
        if (error != 0) throw new Win32Exception((int)error, "XInputGetState fehlgeschlagen.");
        var buttons = (GamepadButtons)(state.Gamepad.Buttons & 0xF3FF);
        if (state.Gamepad.LeftTrigger > 30) buttons |= GamepadButtons.LT;
        if (state.Gamepad.RightTrigger > 30) buttons |= GamepadButtons.RT;
        return new(true, buttons, state.Gamepad.LeftX, state.Gamepad.LeftY, state.Gamepad.RightX, state.Gamepad.RightY);
    }
    [DllImport("xinput1_4.dll", ExactSpelling = true)]
    private static extern uint XInputGetState(uint index, out State state);
    [StructLayout(LayoutKind.Sequential)]
    private struct State { public uint PacketNumber; public Gamepad Gamepad; }
    [StructLayout(LayoutKind.Sequential)]
    private struct Gamepad
    {
        public ushort Buttons;
        public byte LeftTrigger, RightTrigger;
        public short LeftX, LeftY, RightX, RightY;
    }
}
