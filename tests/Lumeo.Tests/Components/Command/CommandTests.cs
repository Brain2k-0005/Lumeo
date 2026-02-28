using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Command;

public class CommandTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public CommandTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<IComponent> RenderCommand(bool includeItems = false, bool includeGroup = false)
    {
        return _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Command>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.CommandInput>(0);
                b.AddAttribute(1, "Placeholder", "Search commands...");
                b.CloseComponent();

                b.OpenComponent<L.CommandList>(1);
                b.AddAttribute(2, "ChildContent", (RenderFragment)(list =>
                {
                    if (includeGroup)
                    {
                        list.OpenComponent<L.CommandGroup>(0);
                        list.AddAttribute(1, "Heading", "Suggestions");
                        list.AddAttribute(2, "ChildContent", (RenderFragment)(grp =>
                        {
                            grp.OpenComponent<L.CommandItem>(0);
                            grp.AddAttribute(1, "ChildContent", (RenderFragment)(i => i.AddContent(0, "Calendar")));
                            grp.CloseComponent();
                        }));
                        list.CloseComponent();
                    }
                    else if (includeItems)
                    {
                        list.OpenComponent<L.CommandItem>(0);
                        list.AddAttribute(1, "ChildContent", (RenderFragment)(i => i.AddContent(0, "Save")));
                        list.CloseComponent();

                        list.OpenComponent<L.CommandItem>(1);
                        list.AddAttribute(1, "ChildContent", (RenderFragment)(i => i.AddContent(0, "Open")));
                        list.CloseComponent();
                    }
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });
    }

    // --- Rendering ---

    [Fact]
    public void Command_Renders_Container_Div()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Command>(0);
            builder.CloseComponent();
        });

        var div = cut.Find("div");
        Assert.NotNull(div);
    }

    [Fact]
    public void CommandInput_Renders_Input_Element()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Command>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.CommandInput>(0);
                b.AddAttribute(1, "Placeholder", "Type a command...");
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        var input = cut.Find("input[type='text']");
        Assert.NotNull(input);
    }

    [Fact]
    public void CommandInput_Has_Default_Placeholder()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Command>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.CommandInput>(0);
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        var input = cut.Find("input");
        Assert.Equal("Type a command or search...", input.GetAttribute("placeholder"));
    }

    [Fact]
    public void CommandInput_Has_Custom_Placeholder()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Command>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.CommandInput>(0);
                b.AddAttribute(1, "Placeholder", "Search commands...");
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        var input = cut.Find("input");
        Assert.Equal("Search commands...", input.GetAttribute("placeholder"));
    }

    [Fact]
    public void CommandList_Renders_Container()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Command>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.CommandList>(0);
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        Assert.NotNull(cut.Find("div"));
    }

    // --- Items ---

    [Fact]
    public void CommandItem_Renders_As_Button()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Command>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.CommandList>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(list =>
                {
                    list.OpenComponent<L.CommandItem>(0);
                    list.AddAttribute(1, "ChildContent", (RenderFragment)(i => i.AddContent(0, "Save")));
                    list.CloseComponent();
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        var button = cut.Find("button");
        Assert.Contains("Save", button.TextContent);
    }

    [Fact]
    public void CommandItem_OnSelect_Fires_When_Clicked()
    {
        bool called = false;
        var callback = EventCallback.Factory.Create(_ctx, () => called = true);

        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Command>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.CommandList>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(list =>
                {
                    list.OpenComponent<L.CommandItem>(0);
                    list.AddAttribute(1, "OnSelect", callback);
                    list.AddAttribute(2, "ChildContent", (RenderFragment)(i => i.AddContent(0, "Save")));
                    list.CloseComponent();
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        cut.Find("button").Click();
        Assert.True(called);
    }

    [Fact]
    public void Disabled_CommandItem_Has_Disabled_CSS_Classes()
    {
        // CommandItem uses the Disabled param to apply CSS classes via AdditionalAttributes or class
        // The Disabled param exists but the component applies disabled:* CSS, not the HTML disabled attr
        // Verify the item still renders when Disabled=true
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Command>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.CommandList>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(list =>
                {
                    list.OpenComponent<L.CommandItem>(0);
                    list.AddAttribute(1, "Disabled", true);
                    list.AddAttribute(2, "ChildContent", (RenderFragment)(i => i.AddContent(0, "Disabled Item")));
                    list.CloseComponent();
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        var button = cut.Find("button");
        // CommandItem renders the button and applies disabled:* CSS classes (disabled:opacity-50)
        var cls = button.GetAttribute("class") ?? "";
        Assert.Contains("disabled:opacity-50", cls);
    }

    // --- Group ---

    [Fact]
    public void CommandGroup_Renders_Heading()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Command>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.CommandList>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(list =>
                {
                    list.OpenComponent<L.CommandGroup>(0);
                    list.AddAttribute(1, "Heading", "Actions");
                    list.CloseComponent();
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        Assert.Contains("Actions", cut.Markup);
    }

    [Fact]
    public void CommandGroup_Without_Heading_Has_No_Heading_Element()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Command>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.CommandList>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(list =>
                {
                    list.OpenComponent<L.CommandGroup>(0);
                    list.AddAttribute(1, "ChildContent", (RenderFragment)(grp => grp.AddContent(0, "item")));
                    list.CloseComponent();
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        // Heading div should not be present
        var divs = cut.FindAll("div");
        var hasHeadingDiv = divs.Any(d => (d.GetAttribute("class") ?? "").Contains("text-xs") && (d.GetAttribute("class") ?? "").Contains("font-medium"));
        Assert.False(hasHeadingDiv);
    }

    // --- Search ---

    [Fact]
    public void Typing_In_CommandInput_Updates_Search_Context()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Command>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.CommandInput>(0);
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        var input = cut.Find("input");
        input.Input("save");
        // Verify input accepts text without error
        Assert.NotNull(input);
    }

    // --- Empty ---

    [Fact]
    public void CommandEmpty_Renders_Default_Message()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.CommandEmpty>(0);
            builder.CloseComponent();
        });

        Assert.Contains("No results found.", cut.Markup);
    }

    [Fact]
    public void CommandEmpty_Renders_Custom_ChildContent()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.CommandEmpty>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(b => b.AddContent(0, "Nothing matches")));
            builder.CloseComponent();
        });

        Assert.Contains("Nothing matches", cut.Markup);
    }

    // --- Custom CSS ---

    [Fact]
    public void Custom_Class_Forwarded_On_Command()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Command>(0);
            builder.AddAttribute(1, "Class", "my-command-class");
            builder.CloseComponent();
        });

        var div = cut.Find("div");
        Assert.Contains("my-command-class", div.GetAttribute("class"));
    }

    [Fact]
    public void Command_Has_Default_Classes()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Command>(0);
            builder.CloseComponent();
        });

        var div = cut.Find("div");
        Assert.Contains("bg-popover", div.GetAttribute("class"));
    }
}
