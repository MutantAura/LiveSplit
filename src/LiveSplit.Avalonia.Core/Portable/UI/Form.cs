using Avalonia.Controls;

namespace LiveSplit.UI.Portable;

/// <summary>
/// Base class of the main timer window. LiveSplitState exposes the main window as
/// <c>Forms.Form</c>; in the portable build that alias resolves to this Avalonia window.
/// </summary>
public class Form : Window
{
}
