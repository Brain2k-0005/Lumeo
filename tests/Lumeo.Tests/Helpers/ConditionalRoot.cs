using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Lumeo.Tests.Helpers;

/// <summary>
/// Renders <see cref="ChildContent"/> only while <see cref="Show"/> is true.
/// Re-rendering with Show=false unmounts the subtree, which triggers component
/// disposal — used to assert dispose-while-open cleanup of overlay components
/// (the second close path besides "Open flipped to false externally").
/// </summary>
public sealed class ConditionalRoot : ComponentBase
{
    [Parameter] public bool Show { get; set; } = true;
    [Parameter] public RenderFragment? ChildContent { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        if (Show)
        {
            builder.AddContent(0, ChildContent);
        }
    }
}
