using LiveSplit.Options;
using LiveSplit.View;
using System;
using System.IO;
using System.Text.Json;

namespace LiveSplit.Racetime;

/// <summary>
/// Keeps the racetime.gg OAuth tokens. The Windows version stores them in the Windows Credential
/// Manager; here they go to a file in LiveSplit's per-user data directory that only the current
/// user can read.
/// </summary>
public sealed class RacetimeTokenStore(string path)
{
    public static RacetimeTokenStore Default { get; } = new(Path.Combine(AppPaths.DataDirectory, "racetime.json"));

    private bool loaded;
    private string accessToken;
    private string refreshToken;

    public string AccessToken
    {
        get
        {
            Load();
            return accessToken;
        }
    }

    public string RefreshToken
    {
        get
        {
            Load();
            return refreshToken;
        }
    }

    public void Set(string access, string refresh)
    {
        loaded = true;
        accessToken = access;
        refreshToken = refresh;
        Save();
    }

    public void Clear()
    {
        Set(null, null);
    }

    private void Load()
    {
        if (loaded)
        {
            return;
        }

        loaded = true;
        try
        {
            if (File.Exists(path))
            {
                using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
                accessToken = document.RootElement.Str("access_token");
                refreshToken = document.RootElement.Str("refresh_token");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex);
        }
    }

    private void Save()
    {
        try
        {
            if (accessToken == null && refreshToken == null)
            {
                File.Delete(path);
                return;
            }

            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                writer.WriteString("access_token", accessToken);
                writer.WriteString("refresh_token", refreshToken);
                writer.WriteEndObject();
            }

            // Restrict the file to its owner before writing the tokens to it.
            const UnixFileMode ownerOnly = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            var options = new FileStreamOptions { Mode = FileMode.Create, Access = FileAccess.Write };
            if (!OperatingSystem.IsWindows())
            {
                options.UnixCreateMode = ownerOnly;
                if (File.Exists(path))
                {
                    File.SetUnixFileMode(path, ownerOnly);
                }
            }

            using var file = new FileStream(path, options);
            file.Write(stream.ToArray());
        }
        catch (Exception ex)
        {
            Log.Error(ex);
        }
    }
}
