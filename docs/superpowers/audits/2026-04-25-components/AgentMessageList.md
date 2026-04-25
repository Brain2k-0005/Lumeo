# AgentMessageList

**Path:** `src/Lumeo/UI/AgentMessageList/`
**Class:** Other
**Files:** AgentMessage.razor, AgentMessageList.razor

## Contract — WARN
- AgentMessageList.razor: IAsyncDisposable implemented, ComponentInteropService used, no direct IJSRuntime.
- AgentMessageList.razor: JSDisconnectedException NOT caught in DisposeAsync — disposal could throw if circuit disconnects.

## API — OK
- All class-required parameters present. (Role, Avatar, Name, Timestamp, IsStreaming, ChildContent, Class, AdditionalAttributes; AutoScroll on list)

## Bugs — WARN
- AgentMessageList.razor DisposeAsync: `await Interop.AiDisposeAutoScroll(_listId)` has no JSDisconnectedException guard.

## Docs — WARN
- Page: `docs/Lumeo.Docs/Pages/Components/AiPage.razor` (exists — shared page, not named AgentMessageListPage.razor)
- 10 ComponentDemo blocks
- API Reference: present
- Indexed in ComponentsIndex.razor: no (not listed)

## CLI — OK
- Registry entry: present (key: agent-message-list)
- Files declared: 2 of 2
- Missing from registry: none
- Component deps declared: OK (no cross-component refs)
