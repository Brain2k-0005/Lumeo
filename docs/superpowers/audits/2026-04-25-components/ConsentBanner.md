# ConsentBanner

**Path:** `src/Lumeo/UI/ConsentBanner/`
**Class:** Other (GDPR utility)
**Files:** ConsentBanner.razor

## Contract — FAIL
- `ConsentBanner.razor` is missing `[Parameter] public string? Class { get; set; }`.
- `ConsentBanner.razor` is missing `[Parameter(CaptureUnmatchedValues = true)] public Dictionary<string, object>? AdditionalAttributes { get; set; }`.
- No `@attributes="AdditionalAttributes"` on root element.

## API — OK
- Rich copy parameters (Title, Description, AcceptLabel, RejectLabel, etc.), `Categories`, `PrivacyPolicyUrl` all present.
- Implements `IDisposable`; correctly unsubscribes from `ConsentService` events.

## Bugs — OK
- No findings.

## Docs — WARN
- Page: `docs/Lumeo.Docs/Pages/Components/ConsentBannerPage.razor` (exists)
- 6 ComponentDemo blocks
- API Reference: present
- Indexed in ComponentsIndex.razor: no

## CLI — WARN
- Registry entry: present (key `consent-banner`)
- Files declared: 1 of 1
- Missing from registry: `Services/ConsentService.cs` is a companion service not listed (separate from UI dir)
- Component deps declared: none declared; `ConsentService` dependency missing from registry entry
