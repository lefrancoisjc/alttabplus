using System.Runtime.InteropServices;
using AltTabPlus.Diagnostics;

namespace AltTabPlus.Native;

internal static class KeyStroke
{
    public static void Chord(params int[] keys)
    {
        if (keys.Length == 0)
        {
            return;
        }

        var inputs = new NativeMethods.INPUT[keys.Length * 2];
        for (var i = 0; i < keys.Length; i++)
        {
            inputs[i] = Key(keys[i], down: true);
            inputs[inputs.Length - 1 - i] = Key(keys[i], down: false);
        }

        var sent = NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
        if (sent != inputs.Length)
        {
            Log.Write($"KeyStroke: SendInput failed ({sent}/{inputs.Length}, win32={Marshal.GetLastWin32Error()})");
        }
    }

    private static NativeMethods.INPUT Key(int vk, bool down) => new()
    {
        type = NativeMethods.INPUT_KEYBOARD,
        U = new NativeMethods.InputUnion
        {
            ki = new NativeMethods.KEYBDINPUT
            {
                wVk = (ushort)vk,
                dwFlags = down ? 0 : NativeMethods.KEYEVENTF_KEYUP,
            },
        },
    };
}
