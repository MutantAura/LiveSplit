using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using LiveSplit.Racetime;
using LiveSplit.UI;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace LiveSplit.View;

/// <summary>
/// A racetime.gg race room: race details, entrants, chat and the runner's race actions. Replaces
/// the WebView2-hosted page of the Windows version with native controls, so it works the same on
/// every platform.
/// </summary>
public sealed class RaceRoomWindow : Window
{
    private const int MaxChatMessages = 1000;

    private static readonly IBrush MutedBrush = Brushes.Gray;
    private static readonly IBrush ErrorBrush = new SolidColorBrush(Color.FromRgb(0xE5, 0x48, 0x4D));
    private static readonly IBrush SystemBrush = new SolidColorBrush(Color.FromRgb(0x5B, 0x9B, 0xD5));
    private static readonly IBrush HighlightBrush = new SolidColorBrush(Color.FromArgb(0x30, 0xFF, 0xC1, 0x07));

    private readonly RacetimeChannel channel;
    private readonly ObservableCollection<ChatMessage> messages = [];
    private readonly ObservableCollection<Entrant> entrants = [];
    private readonly TextBlock gameText;
    private readonly TextBlock goalText;
    private readonly TextBlock infoText;
    private readonly TextBlock raceStatusText;
    private readonly TextBlock connectionText;
    private readonly WrapPanel actionPanel;
    private readonly TextBox input;
    private readonly ScrollViewer chatScroller;
    private readonly DispatcherTimer clock;
    private bool closeConfirmed;

    public RaceRoomWindow(RacetimeChannel channel)
    {
        this.channel = channel;

        Title = "Connecting to " + channel.RaceId[(channel.RaceId.IndexOf('/') + 1)..];
        Width = 860;
        Height = 620;
        MinWidth = 520;
        MinHeight = 360;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        gameText = new TextBlock { FontSize = 18, FontWeight = FontWeight.Bold, TextTrimming = TextTrimming.CharacterEllipsis };
        goalText = new TextBlock { FontSize = 14, TextWrapping = TextWrapping.Wrap };
        infoText = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = MutedBrush, IsVisible = false };
        raceStatusText = new TextBlock { FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center };
        connectionText = new TextBlock { Foreground = MutedBrush, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };

        var openInBrowser = new Button { Content = "Open in Browser" };
        ToolTip.SetTip(openInBrowser, "Open this race room on racetime.gg, e.g. for race monitor tools.");
        openInBrowser.Click += (s, e) => UrlLauncher.Open(RacetimeConfig.RaceUrl(channel.RaceId));

        var statusRow = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 12 };
        AddToGrid(statusRow, raceStatusText, 0);
        AddToGrid(statusRow, connectionText, 1);
        AddToGrid(statusRow, openInBrowser, 2);

        var header = new StackPanel
        {
            Spacing = 4,
            Margin = new Thickness(0, 0, 0, 10),
            Children = { gameText, goalText, infoText, statusRow }
        };

        var chatList = new ItemsControl { ItemsSource = messages, ItemTemplate = new FuncDataTemplate<ChatMessage>((message, _) => BuildMessage(message)) };
        chatScroller = new ScrollViewer { Content = chatList, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };

        var entrantList = new ItemsControl { ItemsSource = entrants, ItemTemplate = new FuncDataTemplate<Entrant>((entrant, _) => BuildEntrant(entrant)) };
        var entrantPanel = new DockPanel();
        var entrantsHeader = new TextBlock { Text = "Entrants", FontWeight = FontWeight.Bold, Margin = new Thickness(0, 0, 0, 6) };
        DockPanel.SetDock(entrantsHeader, Dock.Top);
        entrantPanel.Children.Add(entrantsHeader);
        entrantPanel.Children.Add(new ScrollViewer { Content = entrantList });

        var body = new Grid { ColumnDefinitions = new ColumnDefinitions("*,4,260") };
        AddToGrid(body, new Border { Child = chatScroller, BorderBrush = MutedBrush, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4), Padding = new Thickness(6) }, 0);
        AddToGrid(body, new GridSplitter { Background = Brushes.Transparent, ResizeDirection = GridResizeDirection.Columns }, 1);
        AddToGrid(body, new Border { Child = entrantPanel, Padding = new Thickness(8, 0, 0, 0) }, 2);

        actionPanel = new WrapPanel { ItemSpacing = 6, LineSpacing = 6 };

        input = new TextBox { PlaceholderText ="Send a message, or a command like .ready", MaxLength = 1000 };
        input.KeyDown += (s, e) =>
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                SendInput();
            }
        };
        var send = new Button { Content = "Send" };
        send.Click += (s, e) => SendInput();

        var inputRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 6 };
        AddToGrid(inputRow, input, 0);
        AddToGrid(inputRow, send, 1);

        var footer = new StackPanel { Spacing = 8, Margin = new Thickness(0, 10, 0, 0), Children = { actionPanel, inputRow } };

        var root = new DockPanel { Margin = new Thickness(10) };
        DockPanel.SetDock(header, Dock.Top);
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(header);
        root.Children.Add(footer);
        root.Children.Add(body);
        Content = root;

        channel.RaceChanged += Channel_RaceChanged;
        channel.StateChanged += Channel_StateChanged;
        channel.MessageReceived += Channel_MessageReceived;
        channel.MessageDeleted += Channel_MessageDeleted;
        channel.UserPurged += Channel_UserPurged;

        clock = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (s, e) => UpdateRaceStatus());

        Opened += (s, e) =>
        {
            clock.Start();
            UpdateConnection();
            _ = channel.RunAsync();
        };
        Closing += RaceRoomWindow_Closing;
        Closed += (s, e) =>
        {
            clock.Stop();
            channel.RaceChanged -= Channel_RaceChanged;
            channel.StateChanged -= Channel_StateChanged;
            channel.MessageReceived -= Channel_MessageReceived;
            channel.MessageDeleted -= Channel_MessageDeleted;
            channel.UserPurged -= Channel_UserPurged;
            channel.Dispose();
        };
    }

    public RacetimeChannel Channel => channel;

    private static void AddToGrid(Grid grid, Control control, int column)
    {
        Grid.SetColumn(control, column);
        grid.Children.Add(control);
    }

    #region Channel events

    private void Channel_RaceChanged(object sender, EventArgs e)
    {
        Race race = channel.Race;
        Title = $"{race.Goal} [{race.GameName}] - {race.Slug}";
        gameText.Text = race.GameName;
        goalText.Text = race.Goal;
        infoText.Text = race.Info;
        infoText.IsVisible = !string.IsNullOrWhiteSpace(race.Info);

        entrants.Clear();
        foreach (Entrant entrant in race.Entrants)
        {
            entrants.Add(entrant);
        }

        UpdateRaceStatus();
        UpdateActions();
    }

    private void Channel_StateChanged(object sender, EventArgs e)
    {
        UpdateConnection();
        UpdateActions();
    }

    private void Channel_MessageReceived(object sender, ChatMessage message)
    {
        bool atBottom = chatScroller.Offset.Y >= chatScroller.Extent.Height - chatScroller.Viewport.Height - 8;

        messages.Add(message);
        while (messages.Count > MaxChatMessages)
        {
            messages.RemoveAt(0);
        }

        if (atBottom)
        {
            Dispatcher.UIThread.Post(chatScroller.ScrollToEnd, DispatcherPriority.Background);
        }
    }

    private void Channel_MessageDeleted(object sender, string id)
    {
        ChatMessage message = messages.FirstOrDefault(x => x.Id == id);
        if (message != null)
        {
            messages.Remove(message);
        }
    }

    private void Channel_UserPurged(object sender, string userId)
    {
        foreach (ChatMessage message in messages.Where(x => x.User?.Id == userId).ToList())
        {
            messages.Remove(message);
        }
    }

    #endregion

    #region Display

    private void UpdateConnection()
    {
        string user = channel.User != null && channel.State == ChannelState.Connected ? $" as {channel.User.DisplayName}" : "";
        connectionText.Text = channel.StatusText + user;
        input.IsEnabled = channel.State == ChannelState.Connected;
    }

    private void UpdateRaceStatus()
    {
        Race race = channel.Race;
        if (race == null)
        {
            raceStatusText.Text = "";
            return;
        }

        string status = race.StatusText ?? race.StatusValue;
        if (race.StartedAt is DateTime startedAt && race.State is RaceState.Starting or RaceState.Started)
        {
            TimeSpan elapsed = DateTime.UtcNow - startedAt;
            status += elapsed < TimeSpan.Zero
                ? $" · starting in {Math.Ceiling(-elapsed.TotalSeconds):0}s"
                : $" · {(int)elapsed.TotalHours}:{elapsed:mm\\:ss}";
        }

        raceStatusText.Text = $"{status} · {race.NumEntrants} {(race.NumEntrants == 1 ? "entrant" : "entrants")}";
    }

    private Control BuildMessage(ChatMessage message)
    {
        if (message == null)
        {
            return new Control();
        }

        var text = new SelectableTextBlock { TextWrapping = TextWrapping.Wrap };
        text.Inlines.Add(new Run(message.PostedAt.ToString("HH:mm") + "  ") { Foreground = MutedBrush });

        string author = message.Author;
        if (author != null)
        {
            var authorRun = new Run(author + ": ") { FontWeight = FontWeight.Bold };
            if (message.Kind is ChatMessageKind.Bot or ChatMessageKind.LiveSplit)
            {
                authorRun.Foreground = SystemBrush;
            }

            text.Inlines.Add(authorRun);
        }

        // Only set a foreground where it differs: a null brush would hide the text rather than
        // inherit the theme's text color.
        var body = new Run((message.IsPinned ? "📌 " : "") + message.Text)
        {
            FontStyle = message.Kind == ChatMessageKind.System ? FontStyle.Italic : FontStyle.Normal
        };
        IBrush bodyBrush = message.Kind switch
        {
            ChatMessageKind.Error => ErrorBrush,
            ChatMessageKind.System or ChatMessageKind.LiveSplit => MutedBrush,
            _ => null
        };
        if (bodyBrush != null)
        {
            body.Foreground = bodyBrush;
        }

        text.Inlines.Add(body);

        return new Border
        {
            Child = text,
            Padding = new Thickness(4, 2),
            CornerRadius = new CornerRadius(3),
            Background = message.Highlight && message.Kind != ChatMessageKind.Error ? HighlightBrush : null
        };
    }

    private Control BuildEntrant(Entrant entrant)
    {
        if (entrant == null)
        {
            return new Control();
        }

        string result = entrant.Status == UserStatus.Finished && entrant.FinishTime is TimeSpan time
            ? $"{(int)time.TotalHours}:{time:mm\\:ss}"
            : entrant.StatusText;

        var name = new TextBlock
        {
            Text = (entrant.PlaceOrdinal != null ? entrant.PlaceOrdinal + "  " : "") + (entrant.User?.DisplayName ?? "(hidden)"),
            FontWeight = entrant.User?.Id == channel.UserId ? FontWeight.Bold : FontWeight.Normal,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        var status = new TextBlock
        {
            Text = result,
            Foreground = entrant.Status is UserStatus.Forfeit or UserStatus.Disqualified ? ErrorBrush : MutedBrush,
            Margin = new Thickness(8, 0, 0, 0)
        };

        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        AddToGrid(row, name, 0);
        AddToGrid(row, status, 1);

        var panel = new StackPanel { Margin = new Thickness(0, 2, 0, 4), Children = { row } };
        if (!string.IsNullOrWhiteSpace(entrant.Comment))
        {
            panel.Children.Add(new TextBlock { Text = entrant.Comment, FontStyle = FontStyle.Italic, Foreground = MutedBrush, TextWrapping = TextWrapping.Wrap });
        }

        if (entrant.StreamLive)
        {
            ToolTip.SetTip(panel, "Streaming live");
        }

        return panel;
    }

    #endregion

    #region Actions

    private void UpdateActions()
    {
        actionPanel.Children.Clear();
        Race race = channel.Race;
        if (race == null || channel.State != ChannelState.Connected)
        {
            return;
        }

        Entrant me = channel.Me;
        if (me == null)
        {
            if (race.State == RaceState.Open)
            {
                AddAction("Join Race", () => channel.SendCommand("join"));
            }
            else if (race.State == RaceState.OpenInviteOnly)
            {
                AddAction("Request to Join", () => channel.SendCommand("requestinvite"));
            }

            return;
        }

        foreach (string action in me.Actions)
        {
            switch (action)
            {
                case "accept_invite":
                    AddAction("Accept Invite", () => channel.SendCommand("acceptinvite"));
                    break;
                case "decline_invite":
                    AddAction("Decline Invite", () => channel.SendCommand("declineinvite"));
                    break;
                case "cancel_invite":
                    AddAction("Cancel Join Request", () => channel.SendCommand("cancelinvite"));
                    break;
                case "ready":
                    AddAction("Ready", () => channel.SendCommand("ready"));
                    break;
                case "unready":
                    AddAction("Not Ready", () => channel.SendCommand("unready"));
                    break;
                case "not_live":
                    AddAction("Not Live", null, "This race requires you to be streaming live before you can ready up.");
                    break;
                case "leave":
                    AddAction("Leave Race", () => channel.SendCommand("leave"));
                    break;
                case "add_comment":
                case "change_comment":
                    AddAction(action == "add_comment" ? "Add Comment" : "Change Comment", AddComment);
                    break;
                case "done":
                    AddAction("Done", () => channel.SendCommand("done"), "Finish the race. Splitting on your final split does this automatically.");
                    break;
                case "undone":
                    AddAction("Undo Finish", () => channel.SendCommand("undone"));
                    break;
                case "forfeit":
                    AddAction("Forfeit", ConfirmForfeit);
                    break;
                case "unforfeit":
                    AddAction("Undo Forfeit", () => channel.SendCommand("unforfeit"));
                    break;
                case "set_team":
                case "partition":
                    AddAction("Continue on racetime.gg...", () => { UrlLauncher.Open(RacetimeConfig.RaceUrl(channel.RaceId)); return Task.CompletedTask; },
                        "This step can only be done on the racetime.gg website.");
                    break;
            }
        }
    }

    private void AddAction(string label, Func<Task> action, string tip = null)
    {
        var button = new Button { Content = label, IsEnabled = action != null };
        if (tip != null)
        {
            ToolTip.SetTip(button, tip);
        }

        if (action != null)
        {
            button.Click += async (s, e) =>
            {
                button.IsEnabled = false;
                try
                {
                    await action();
                }
                finally
                {
                    button.IsEnabled = true;
                }
            };
        }

        actionPanel.Children.Add(button);
    }

    private async Task AddComment()
    {
        string comment = await InputBox.Show(this, "Comment on your race (visible to everyone):", "Race Comment", channel.Me?.Comment ?? "", 200);
        if (!string.IsNullOrWhiteSpace(comment))
        {
            await channel.SendCommand("comment", ("comment", comment.Trim()));
        }
    }

    private async Task ConfirmForfeit()
    {
        DialogResult result = await MessageBox.Show(this, "Do you really want to forfeit this race?", "Forfeit", MessageBoxButtons.YesNo);
        if (result == DialogResult.Yes)
        {
            await channel.Forfeit();
        }
    }

    private void SendInput()
    {
        string text = input.Text;
        input.Text = "";
        _ = channel.SendChatMessage(text);
    }

    #endregion

    #region Closing

    /// <summary>
    /// Closes the window, first asking whether to forfeit if the runner is still racing.
    /// Returns false if the user cancelled.
    /// </summary>
    public async Task<bool> TryCloseAsync()
    {
        if (!await ConfirmLeaving())
        {
            return false;
        }

        closeConfirmed = true;
        Close();
        return true;
    }

    private async void RaceRoomWindow_Closing(object sender, WindowClosingEventArgs e)
    {
        if (closeConfirmed || channel.Race?.State != RaceState.Started || channel.PersonalStatus != UserStatus.Racing)
        {
            return;
        }

        e.Cancel = true;
        if (await ConfirmLeaving())
        {
            closeConfirmed = true;
            Close();
        }
    }

    private async Task<bool> ConfirmLeaving()
    {
        if (channel.Race?.State != RaceState.Started || channel.PersonalStatus != UserStatus.Racing)
        {
            return true;
        }

        Activate();
        DialogResult result = await MessageBox.Show(this, "Do you want to forfeit before closing the race room?", "Leave Race Room", MessageBoxButtons.YesNoCancel);
        if (result == DialogResult.Cancel)
        {
            return false;
        }

        if (result == DialogResult.Yes)
        {
            await channel.Forfeit();
        }

        return true;
    }

    #endregion
}
