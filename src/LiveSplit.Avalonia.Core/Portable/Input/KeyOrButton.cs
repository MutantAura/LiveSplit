using System;

namespace LiveSplit.Model.Input;

public delegate void EventHandlerT<T>(object sender, T value);

public readonly struct GamepadButton : IEquatable<GamepadButton>
{
    public string GamepadName { get; }
    public string Button { get; }

    public GamepadButton(string gamepadName, string button)
    {
        GamepadName = gamepadName;
        Button = button;
    }

    public bool Equals(GamepadButton other)
    {
        return GamepadName == other.GamepadName && Button == other.Button;
    }

    public override bool Equals(object obj)
    {
        return obj is GamepadButton other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(GamepadName, Button);
    }

    public static bool operator ==(GamepadButton a, GamepadButton b)
    {
        return a.Equals(b);
    }

    public static bool operator !=(GamepadButton a, GamepadButton b)
    {
        return !a.Equals(b);
    }
}

/// <summary>
/// Portable copy of LiveSplit.Core's KeyOrButton, with identical string representation.
/// </summary>
public class KeyOrButton
{
    public bool IsButton { get; protected set; }
    public bool IsKey { get => !IsButton; set => IsButton = !value; }

    public Keys Key { get; protected set; }
    public GamepadButton Button { get; protected set; }

    public KeyOrButton(Keys key)
    {
        Key = key;
        IsKey = true;
    }

    public KeyOrButton(GamepadButton button)
    {
        Button = button;
        IsButton = true;
    }

    public KeyOrButton(string stringRepresentation)
    {
        if (stringRepresentation.Contains(' ') && !stringRepresentation.Contains(", "))
        {
            string[] split = stringRepresentation.Split([' '], 2);
            Button = new GamepadButton(split[1], split[0]);
            IsButton = true;
        }
        else
        {
            Key = (Keys)Enum.Parse(typeof(Keys), stringRepresentation, true);
            IsKey = true;
        }
    }

    public override string ToString()
    {
        return IsKey
            ? Key.ToString()
            : Button.Button + " " + Button.GamepadName;
    }

    public static bool operator ==(KeyOrButton a, KeyOrButton b)
    {
        if (a is null && b is null)
        {
            return true;
        }

        if (a is null || b is null)
        {
            return false;
        }

        if (a.IsKey && b.IsKey)
        {
            return a.Key == b.Key;
        }
        else if (a.IsButton && b.IsButton)
        {
            return a.Button == b.Button;
        }

        return false;
    }

    public static bool operator !=(KeyOrButton a, KeyOrButton b)
    {
        return !(a == b);
    }

    public override bool Equals(object obj)
    {
        return obj is KeyOrButton other
            && this == other;
    }

    public override int GetHashCode()
    {
        return IsKey ? Key.GetHashCode() : Button.GetHashCode();
    }
}
