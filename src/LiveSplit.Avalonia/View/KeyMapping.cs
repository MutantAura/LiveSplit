using Avalonia.Input;
using System;
using Keys = LiveSplit.Model.Input.Keys;

namespace LiveSplit.View;

/// <summary>
/// Converts Avalonia key events into the WinForms-compatible <see cref="Keys"/> values that
/// hotkeys are stored as.
/// </summary>
public static class KeyMapping
{
    public static Keys FromAvalonia(Key key, KeyModifiers modifiers)
    {
        Keys result = FromAvalonia(key);
        if (result == Keys.None)
        {
            return Keys.None;
        }

        if (modifiers.HasFlag(KeyModifiers.Shift))
        {
            result |= Keys.Shift;
        }

        if (modifiers.HasFlag(KeyModifiers.Control))
        {
            result |= Keys.Control;
        }

        if (modifiers.HasFlag(KeyModifiers.Alt))
        {
            result |= Keys.Alt;
        }

        return result;
    }

    public static Keys FromAvalonia(Key key)
    {
        switch (key)
        {
            case Key.None: return Keys.None;
            case Key.Enter: return Keys.Return;
            case Key.Back: return Keys.Back;
            case Key.Escape: return Keys.Escape;
            case Key.CapsLock: return Keys.CapsLock;
            case Key.PageUp: return Keys.PageUp;
            case Key.PageDown: return Keys.PageDown;
            case Key.LeftShift: return Keys.LShiftKey;
            case Key.RightShift: return Keys.RShiftKey;
            case Key.LeftCtrl: return Keys.LControlKey;
            case Key.RightCtrl: return Keys.RControlKey;
            case Key.LeftAlt: return Keys.LMenu;
            case Key.RightAlt: return Keys.RMenu;
            case Key.LWin: return Keys.LWin;
            case Key.RWin: return Keys.RWin;
            case Key.Scroll: return Keys.Scroll;
            case Key.OemSemicolon: return Keys.OemSemicolon;
            case Key.OemPlus: return Keys.Oemplus;
            case Key.OemComma: return Keys.Oemcomma;
            case Key.OemMinus: return Keys.OemMinus;
            case Key.OemPeriod: return Keys.OemPeriod;
            case Key.OemQuestion: return Keys.OemQuestion;
            case Key.OemTilde: return Keys.Oemtilde;
            case Key.OemOpenBrackets: return Keys.OemOpenBrackets;
            case Key.OemPipe: return Keys.OemPipe;
            case Key.OemCloseBrackets: return Keys.OemCloseBrackets;
            case Key.OemQuotes: return Keys.OemQuotes;
            case Key.OemBackslash: return Keys.OemBackslash;
        }

        // Letters, digits (D0-D9), NumPad0-9, F1-F24, arrows and most other keys share their
        // names between Avalonia and WinForms.
        return Enum.TryParse(key.ToString(), out Keys parsed) ? parsed : Keys.None;
    }
}
