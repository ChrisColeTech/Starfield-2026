using System;

namespace Starfield2026.Core.UI;

/// <summary>
/// A single item in a MenuBox.
/// </summary>
public class MenuItem
{
    public string Label { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public Action? OnConfirm { get; set; }

    public MenuItem() { }

    public MenuItem(string label, Action? onConfirm = null, bool enabled = true)
    {
        Label = label;
        OnConfirm = onConfirm;
        Enabled = enabled;
    }
}
