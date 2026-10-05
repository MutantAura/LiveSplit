using LiveSplit.Options;
using LiveSplit.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace LiveSplit.Racetime;

public enum AuthResult
{
    Success,
    Failure,
    Cancelled
}

/// <summary>
/// Signs in to racetime.gg with OAuth 2 (authorization code flow with PKCE). The user approves
/// LiveSplit in their browser, which then redirects to a listener on 127.0.0.1. Tokens are
/// remembered and refreshed so that this only has to happen once.
/// </summary>
public sealed class RacetimeAuthenticator(HttpClient http, RacetimeTokenStore tokens, Func<string, bool> openBrowser)
{
    private static readonly TimeSpan BrowserTimeout = TimeSpan.FromMinutes(5);

    private Task<AuthResult> pending;

    public RacetimeUser Identity { get; internal set; }
    public string Error { get; private set; }
    public string AccessToken => tokens.AccessToken;
    public bool HasStoredLogin => tokens.RefreshToken != null || tokens.AccessToken != null;

    /// <summary>
    /// Ensures there is a valid access token and the user's identity is known. Concurrent calls
    /// share the same sign-in attempt.
    /// </summary>
    public Task<AuthResult> AuthorizeAsync(CancellationToken cancellationToken = default)
    {
        if (pending is { IsCompleted: false })
        {
            return pending;
        }

        return pending = AuthorizeCoreAsync(cancellationToken);
    }

    public void SignOut()
    {
        tokens.Clear();
        Identity = null;
    }

    /// <summary>
    /// Forgets the access token after racetime.gg rejected it, so that the next authorization
    /// refreshes it.
    /// </summary>
    public void InvalidateAccessToken()
    {
        tokens.Set(null, tokens.RefreshToken);
    }

    private async Task<AuthResult> AuthorizeCoreAsync(CancellationToken cancellationToken)
    {
        Error = null;
        try
        {
            if (tokens.AccessToken != null && await TryGetUserInfo(cancellationToken))
            {
                return AuthResult.Success;
            }

            if (tokens.RefreshToken != null && await TryRefresh(cancellationToken) && await TryGetUserInfo(cancellationToken))
            {
                return AuthResult.Success;
            }

            AuthResult result = await SignInWithBrowser(cancellationToken);
            if (result != AuthResult.Success)
            {
                return result;
            }

            if (await TryGetUserInfo(cancellationToken))
            {
                return AuthResult.Success;
            }

            Error ??= "Signed in, but racetime.gg did not return your user information.";
            return AuthResult.Failure;
        }
        catch (OperationCanceledException)
        {
            return AuthResult.Cancelled;
        }
        catch (HttpRequestException ex)
        {
            Error = $"Could not reach racetime.gg: {ex.Message}";
            return AuthResult.Failure;
        }
        catch (Exception ex)
        {
            Log.Error(ex);
            Error = ex.Message;
            return AuthResult.Failure;
        }
    }

    private async Task<bool> TryGetUserInfo(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, RacetimeConfig.WebRoot + RacetimeConfig.UserInfoEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        using HttpResponseMessage response = await http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return false;
        }

        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        Identity = RacetimeUser.FromJson(document.RootElement);
        return Identity?.Id != null;
    }

    private Task<bool> TryRefresh(CancellationToken cancellationToken)
    {
        return RequestTokens(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = tokens.RefreshToken,
            ["client_id"] = RacetimeConfig.ClientId,
            ["client_secret"] = RacetimeConfig.ClientSecret
        }, cancellationToken);
    }

    private async Task<bool> RequestTokens(Dictionary<string, string> form, CancellationToken cancellationToken)
    {
        using var content = new FormUrlEncodedContent(form);
        using HttpResponseMessage response = await http.PostAsync(RacetimeConfig.WebRoot + RacetimeConfig.TokenEndpoint, content, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return false;
        }

        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        string access = document.RootElement.Str("access_token");
        if (access == null)
        {
            return false;
        }

        tokens.Set(access, document.RootElement.Str("refresh_token") ?? tokens.RefreshToken);
        return true;
    }

    private async Task<AuthResult> SignInWithBrowser(CancellationToken cancellationToken)
    {
        string verifier = RandomBase64Url(32);
        string state = RandomBase64Url(32);
        string challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

        var listener = new TcpListener(IPAddress.Loopback, RacetimeConfig.RedirectPort);
        try
        {
            listener.Start();
        }
        catch (SocketException)
        {
            Error = $"Port {RacetimeConfig.RedirectPort} is in use, so the racetime.gg sign-in can't complete. Close any other program using it (such as another LiveSplit) and try again.";
            return AuthResult.Failure;
        }

        try
        {
            string url = $"{RacetimeConfig.WebRoot}{RacetimeConfig.AuthorizeEndpoint}?response_type=code"
                + $"&client_id={Uri.EscapeDataString(RacetimeConfig.ClientId)}"
                + $"&scope={Uri.EscapeDataString(RacetimeConfig.Scopes)}"
                + $"&redirect_uri={Uri.EscapeDataString(RacetimeConfig.RedirectUri)}"
                + $"&state={state}&code_challenge={challenge}&code_challenge_method=S256";

            if (!openBrowser(url))
            {
                Error = "Could not open a web browser to sign in to racetime.gg.";
                return AuthResult.Failure;
            }

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(BrowserTimeout);

            (TcpClient redirect, Dictionary<string, string> query) = await WaitForRedirect(listener, timeout.Token);
            using TcpClient client = redirect;

            bool approved = query.TryGetValue("code", out string code) && query.GetValueOrDefault("state") == state;
            await SendRedirect(client, approved ? RacetimeConfig.DoneEndpoint : RacetimeConfig.DeniedEndpoint);

            if (query.TryGetValue("error", out string error))
            {
                Error = error == "access_denied" ? "Sign-in was declined." : $"racetime.gg rejected the sign-in ({error}).";
                return error == "access_denied" ? AuthResult.Cancelled : AuthResult.Failure;
            }

            if (!approved)
            {
                Error = "racetime.gg did not respond correctly to the sign-in request.";
                return AuthResult.Failure;
            }

            bool granted = await RequestTokens(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = RacetimeConfig.RedirectUri,
                ["client_id"] = RacetimeConfig.ClientId,
                ["client_secret"] = RacetimeConfig.ClientSecret,
                ["code_verifier"] = verifier
            }, cancellationToken);

            if (!granted)
            {
                Error = "Signed in, but racetime.gg did not grant access.";
                return AuthResult.Failure;
            }

            return AuthResult.Success;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            Error = "Timed out waiting for the racetime.gg sign-in to complete.";
            return AuthResult.Failure;
        }
        finally
        {
            listener.Stop();
        }
    }

    /// <summary>
    /// Waits for the browser to request the redirect URI and returns that connection with its
    /// query parameters. Connections are handled independently, because browsers may open
    /// speculative connections that never send a request.
    /// </summary>
    private static async Task<(TcpClient Client, Dictionary<string, string> Query)> WaitForRedirect(TcpListener listener, CancellationToken cancellationToken)
    {
        var result = new TaskCompletionSource<(TcpClient, Dictionary<string, string>)>(TaskCreationOptions.RunContinuationsAsynchronously);
        using CancellationTokenRegistration registration = cancellationToken.Register(() => result.TrySetCanceled(cancellationToken));

        // Cancelled once the redirect arrives, which closes any other connections still waiting.
        using var connections = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        async Task Handle(TcpClient client)
        {
            try
            {
                using var reader = new StreamReader(client.GetStream(), Encoding.ASCII, false, 1024, leaveOpen: true);
                Dictionary<string, string> query = ParseRequestLine(await reader.ReadLineAsync(connections.Token) ?? "");
                if ((query.ContainsKey("code") || query.ContainsKey("error")) && result.TrySetResult((client, query)))
                {
                    return;
                }

                await client.GetStream().WriteAsync("HTTP/1.1 404 Not Found\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"u8.ToArray(), connections.Token);
            }
            catch (Exception) { }

            client.Dispose();
        }

        try
        {
            while (!result.Task.IsCompleted)
            {
                Task<TcpClient> accept = listener.AcceptTcpClientAsync(connections.Token).AsTask();
                if (await Task.WhenAny(accept, result.Task) != accept)
                {
                    break;
                }

                _ = Handle(await accept);
            }

            return await result.Task;
        }
        finally
        {
            connections.Cancel();
        }
    }

    internal static Dictionary<string, string> ParseRequestLine(string requestLine)
    {
        // "GET /?code=...&state=... HTTP/1.1"
        var result = new Dictionary<string, string>();
        string[] parts = requestLine.Split(' ');
        if (parts.Length < 2)
        {
            return result;
        }

        int queryStart = parts[1].IndexOf('?');
        if (queryStart < 0)
        {
            return result;
        }

        foreach (string pair in parts[1][(queryStart + 1)..].Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            int separator = pair.IndexOf('=');
            string key = Uri.UnescapeDataString(separator < 0 ? pair : pair[..separator]);
            string value = separator < 0 ? "" : Uri.UnescapeDataString(pair[(separator + 1)..].Replace('+', ' '));
            result[key] = value;
        }

        return result;
    }

    private static async Task SendRedirect(TcpClient client, string endpoint)
    {
        byte[] response = Encoding.ASCII.GetBytes(
            "HTTP/1.1 302 Found\r\n" +
            $"Location: {RacetimeConfig.WebRoot}{endpoint}\r\n" +
            "Content-Length: 0\r\n" +
            "Connection: close\r\n\r\n");
        await client.GetStream().WriteAsync(response);
    }

    private static string RandomBase64Url(int length)
    {
        return Base64Url(RandomNumberGenerator.GetBytes(length));
    }

    private static string Base64Url(byte[] data)
    {
        return Convert.ToBase64String(data).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }
}
