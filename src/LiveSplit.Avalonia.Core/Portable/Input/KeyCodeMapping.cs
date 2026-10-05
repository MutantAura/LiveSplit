using SharpHook.Data;

namespace LiveSplit.Model.Input;

/// <summary>
/// Maps libuiohook virtual key codes to the WinForms <see cref="Keys"/> values LiveSplit stores.
/// </summary>
public static class KeyCodeMapping
{
    public static Keys ToKeys(KeyCode code)
    {
        // Letter and digit codes are not contiguous in libuiohook, so map them by name.
        string name = code.ToString();
        if (name.Length == 3 && name.StartsWith("Vc", System.StringComparison.Ordinal))
        {
            char c = name[2];
            if (c is >= 'A' and <= 'Z')
            {
                return Keys.A + (c - 'A');
            }

            if (c is >= '0' and <= '9')
            {
                return Keys.D0 + (c - '0');
            }
        }

        return code switch
        {
            KeyCode.VcNumPad0 => Keys.NumPad0,
            KeyCode.VcNumPad1 => Keys.NumPad1,
            KeyCode.VcNumPad2 => Keys.NumPad2,
            KeyCode.VcNumPad3 => Keys.NumPad3,
            KeyCode.VcNumPad4 => Keys.NumPad4,
            KeyCode.VcNumPad5 => Keys.NumPad5,
            KeyCode.VcNumPad6 => Keys.NumPad6,
            KeyCode.VcNumPad7 => Keys.NumPad7,
            KeyCode.VcNumPad8 => Keys.NumPad8,
            KeyCode.VcNumPad9 => Keys.NumPad9,
            KeyCode.VcNumPadAdd => Keys.Add,
            KeyCode.VcNumPadSubtract => Keys.Subtract,
            KeyCode.VcNumPadMultiply => Keys.Multiply,
            KeyCode.VcNumPadDivide => Keys.Divide,
            KeyCode.VcNumPadDecimal => Keys.Decimal,
            KeyCode.VcNumPadSeparator => Keys.Separator,
            KeyCode.VcNumPadEnter => Keys.Return,
            KeyCode.VcNumPadClear => Keys.Clear,
            KeyCode.VcF1 => Keys.F1,
            KeyCode.VcF2 => Keys.F2,
            KeyCode.VcF3 => Keys.F3,
            KeyCode.VcF4 => Keys.F4,
            KeyCode.VcF5 => Keys.F5,
            KeyCode.VcF6 => Keys.F6,
            KeyCode.VcF7 => Keys.F7,
            KeyCode.VcF8 => Keys.F8,
            KeyCode.VcF9 => Keys.F9,
            KeyCode.VcF10 => Keys.F10,
            KeyCode.VcF11 => Keys.F11,
            KeyCode.VcF12 => Keys.F12,
            KeyCode.VcF13 => Keys.F13,
            KeyCode.VcF14 => Keys.F14,
            KeyCode.VcF15 => Keys.F15,
            KeyCode.VcF16 => Keys.F16,
            KeyCode.VcF17 => Keys.F17,
            KeyCode.VcF18 => Keys.F18,
            KeyCode.VcF19 => Keys.F19,
            KeyCode.VcF20 => Keys.F20,
            KeyCode.VcF21 => Keys.F21,
            KeyCode.VcF22 => Keys.F22,
            KeyCode.VcF23 => Keys.F23,
            KeyCode.VcF24 => Keys.F24,
            KeyCode.VcEscape => Keys.Escape,
            KeyCode.VcBackQuote => Keys.Oemtilde,
            KeyCode.VcMinus => Keys.OemMinus,
            KeyCode.VcEquals => Keys.Oemplus,
            KeyCode.VcBackspace => Keys.Back,
            KeyCode.VcTab => Keys.Tab,
            KeyCode.VcCapsLock => Keys.CapsLock,
            KeyCode.VcOpenBracket => Keys.OemOpenBrackets,
            KeyCode.VcCloseBracket => Keys.OemCloseBrackets,
            KeyCode.VcBackslash => Keys.OemPipe,
            KeyCode.VcSemicolon => Keys.OemSemicolon,
            KeyCode.VcQuote => Keys.OemQuotes,
            KeyCode.VcEnter => Keys.Return,
            KeyCode.VcComma => Keys.Oemcomma,
            KeyCode.VcPeriod => Keys.OemPeriod,
            KeyCode.VcSlash => Keys.OemQuestion,
            KeyCode.VcSpace => Keys.Space,
            KeyCode.VcPrintScreen => Keys.PrintScreen,
            KeyCode.VcScrollLock => Keys.Scroll,
            KeyCode.VcPause => Keys.Pause,
            KeyCode.VcInsert => Keys.Insert,
            KeyCode.VcDelete => Keys.Delete,
            KeyCode.VcHome => Keys.Home,
            KeyCode.VcEnd => Keys.End,
            KeyCode.VcPageUp => Keys.PageUp,
            KeyCode.VcPageDown => Keys.PageDown,
            KeyCode.VcUp => Keys.Up,
            KeyCode.VcLeft => Keys.Left,
            KeyCode.VcRight => Keys.Right,
            KeyCode.VcDown => Keys.Down,
            KeyCode.VcNumLock => Keys.NumLock,
            KeyCode.VcLeftShift => Keys.LShiftKey,
            KeyCode.VcRightShift => Keys.RShiftKey,
            KeyCode.VcLeftControl => Keys.LControlKey,
            KeyCode.VcRightControl => Keys.RControlKey,
            KeyCode.VcLeftAlt => Keys.LMenu,
            KeyCode.VcRightAlt => Keys.RMenu,
            KeyCode.VcLeftMeta => Keys.LWin,
            KeyCode.VcRightMeta => Keys.RWin,
            KeyCode.VcContextMenu => Keys.Apps,
            KeyCode.VcVolumeMute => Keys.VolumeMute,
            KeyCode.VcVolumeDown => Keys.VolumeDown,
            KeyCode.VcVolumeUp => Keys.VolumeUp,
            KeyCode.VcMediaPlay => Keys.MediaPlayPause,
            KeyCode.VcMediaStop => Keys.MediaStop,
            KeyCode.VcMediaPrevious => Keys.MediaPreviousTrack,
            KeyCode.VcMediaNext => Keys.MediaNextTrack,
            KeyCode.VcMediaSelect => Keys.SelectMedia,
            KeyCode.VcAppMail => Keys.LaunchMail,
            KeyCode.VcBrowserBack => Keys.BrowserBack,
            KeyCode.VcBrowserForward => Keys.BrowserForward,
            KeyCode.VcBrowserRefresh => Keys.BrowserRefresh,
            KeyCode.VcBrowserStop => Keys.BrowserStop,
            KeyCode.VcBrowserSearch => Keys.BrowserSearch,
            KeyCode.VcBrowserFavorites => Keys.BrowserFavorites,
            KeyCode.VcBrowserHome => Keys.BrowserHome,
            KeyCode.VcSleep => Keys.Sleep,
            KeyCode.VcHelp => Keys.Help,
            _ => Keys.None
        };
    }
}
