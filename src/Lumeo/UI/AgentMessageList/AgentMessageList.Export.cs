using System.Globalization;
using System.Text;

namespace Lumeo;

public partial class AgentMessageList
{
    /// <summary>
    /// A single conversation turn used by <see cref="ToMarkdown"/>. Because
    /// <c>AgentMessage</c> bodies are arbitrary <c>RenderFragment</c>s (whose
    /// text can't be reliably extracted), the export takes the plain text explicitly.
    /// </summary>
    /// <param name="Role">The turn's role (drives the heading label).</param>
    /// <param name="Text">The message body as plain text / Markdown.</param>
    /// <param name="Name">Optional display name; overrides the default role label.</param>
    /// <param name="Timestamp">Optional timestamp appended to the heading.</param>
    public sealed record AgentMessageMarkdown(
        AgentMessage.AgentMessageRole Role,
        string Text,
        string? Name = null,
        DateTimeOffset? Timestamp = null);

    /// <summary>
    /// Serializes a conversation to a Markdown transcript — each turn becomes a bold
    /// role heading (with optional name + timestamp) followed by its body. Pair with
    /// <see cref="Services.IComponentInteropService.DownloadFile"/> to offer a download.
    /// </summary>
    /// <param name="messages">The turns, in display order.</param>
    /// <param name="title">Optional H2 title for the transcript.</param>
    /// <param name="timestampFormat">Format for turn timestamps (default <c>"yyyy-MM-dd HH:mm"</c>).</param>
    public static string ToMarkdown(
        IEnumerable<AgentMessageMarkdown> messages,
        string? title = null,
        string timestampFormat = "yyyy-MM-dd HH:mm")
    {
        ArgumentNullException.ThrowIfNull(messages);

        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(title))
            sb.Append("## ").Append(title!.Trim()).Append("\n\n");

        foreach (var m in messages)
        {
            sb.Append("**").Append(RoleLabel(m.Role, m.Name)).Append("**");
            if (m.Timestamp is { } ts)
                sb.Append(" — ").Append(ts.ToString(timestampFormat, CultureInfo.InvariantCulture));
            sb.Append("\n\n");
            sb.Append((m.Text ?? string.Empty).Trim()).Append("\n\n");
        }

        return sb.ToString().TrimEnd('\n') + "\n";
    }

    private static string RoleLabel(AgentMessage.AgentMessageRole role, string? name) =>
        !string.IsNullOrWhiteSpace(name)
            ? name!.Trim()
            : role switch
            {
                AgentMessage.AgentMessageRole.User => "User",
                AgentMessage.AgentMessageRole.Assistant => "Assistant",
                AgentMessage.AgentMessageRole.System => "System",
                AgentMessage.AgentMessageRole.Tool => "Tool",
                _ => "Message",
            };
}
