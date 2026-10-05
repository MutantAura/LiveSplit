using Avalonia.Markup.Xaml;
using Avalonia.Styling;

namespace LiveSplit.Themes;

public partial class WinUITheme : Styles
{
    public WinUITheme()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
