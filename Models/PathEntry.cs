using CSharpApiExtractorGUI.Infrastructure;

namespace CSharpApiExtractorGUI.Models;

public sealed class PathEntry : BindableBase
{
    private string value = string.Empty;

    public string Value
    {
        get => value;
        set => SetProperty(ref this.value, value);
    }
}
