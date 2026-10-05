using LiveSplit.Options;
using SharpHook;
using SharpHook.Data;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LiveSplit.Model.Input;

/// <summary>
/// Cross-platform replacement for LiveSplit.Core's CompositeHook. Global keyboard hotkeys are
/// captured through SharpHook (libuiohook), which supports Windows, macOS (requires the
/// Accessibility permission) and Linux on X11. Gamepad hotkeys are not supported yet.
/// </summary>
/// <remarks>
/// <see cref="KeyOrButtonPressed"/> is raised on the hook's background thread, only for keys
/// registered through <see cref="RegisterHotKey(Keys)"/>.
/// </remarks>
public class CompositeHook : IDisposable
{
    private readonly object sync = new();
    private readonly HashSet<Keys> registeredKeys = [];
    private SimpleGlobalHook hook;
    private bool hookFailed;

    public bool AllowGamepads { get; set; }

    /// <summary>
    /// True once the global hook has failed to start, e.g. on Wayland or when macOS denies
    /// accessibility access. Hotkeys then only work while the LiveSplit window has focus.
    /// </summary>
    public bool IsUnavailable => hookFailed;

    public event EventHandlerT<KeyOrButton> KeyOrButtonPressed;
    public event EventHandler GamepadHookInitialized;

    public CompositeHook() : this(false) { }

    public CompositeHook(bool allowGamepads)
    {
        AllowGamepads = allowGamepads;
    }

    private void EnsureHookRunning()
    {
        if (hook != null || hookFailed)
        {
            return;
        }

        try
        {
            hook = new SimpleGlobalHook();
            hook.KeyPressed += Hook_KeyPressed;
            Task run = hook.RunAsync();
            run.ContinueWith(t =>
            {
                hookFailed = true;
                Log.Error(t.Exception);
            }, TaskContinuationOptions.OnlyOnFaulted);
        }
        catch (Exception ex)
        {
            hookFailed = true;
            Log.Error(ex);
        }

        GamepadHookInitialized?.Invoke(this, EventArgs.Empty);
    }

    private void Hook_KeyPressed(object sender, KeyboardHookEventArgs e)
    {
        Keys key = KeyCodeMapping.ToKeys(e.Data.KeyCode);
        if (key == Keys.None)
        {
            return;
        }

        EventMask mask = e.RawEvent.Mask;
        if (mask.HasShift())
        {
            key |= Keys.Shift;
        }

        if (mask.HasCtrl())
        {
            key |= Keys.Control;
        }

        if (mask.HasAlt())
        {
            key |= Keys.Alt;
        }

        bool registered;
        lock (sync)
        {
            registered = registeredKeys.Contains(key);
        }

        if (registered)
        {
            KeyOrButtonPressed?.Invoke(this, new KeyOrButton(key));
        }
    }

    public void RegisterHotKey(Keys key)
    {
        lock (sync)
        {
            registeredKeys.Add(key);
        }

        EnsureHookRunning();
    }

    public void RegisterHotKey(KeyOrButton keyOrButton)
    {
        if (keyOrButton.IsKey)
        {
            RegisterHotKey(keyOrButton.Key);
        }
    }

    public void Poll() { }

    public void UnregisterAllHotkeys()
    {
        lock (sync)
        {
            registeredKeys.Clear();
        }
    }

    public void Dispose()
    {
        try
        {
            hook?.Dispose();
        }
        catch (Exception ex)
        {
            Log.Error(ex);
        }

        hook = null;
    }
}
