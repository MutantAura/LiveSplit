using Avalonia.Markup.Xaml;
using Avalonia.Styling;

namespace LiveSplit.Themes;

public partial class ClassicTheme : Styles
{
    public ClassicTheme()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
