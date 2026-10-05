using Avalonia.Markup.Xaml;
using Avalonia.Styling;

namespace LiveSplit.Themes;

public partial class MacOSTheme : Styles
{
    public MacOSTheme()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
