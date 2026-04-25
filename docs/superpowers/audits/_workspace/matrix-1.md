| Accordion | OK — all contract checks pass | OK — all params present | OK — no findings | OK — page exists, 3 demos, API ref, indexed | OK — 4/4 files |
| Affix | OK — all contract checks pass | OK — all params present | OK — no findings | WARN — not listed in ComponentsIndex | OK — 1/1 files |
| AgentMessageList | WARN — DisposeAsync missing JSDisconnectedException guard | OK — all params present | WARN — DisposeAsync unguarded, may throw on disconnect | WARN — page is AiPage.razor (shared), not indexed | OK — 2/2 files |
| Alert | OK — all contract checks pass | WARN — Size param absent (Display class) | OK — no findings | OK — page exists, 7 demos, API ref, indexed | OK — 1/1 files |
| AlertDialog | WARN — root AlertDialog.razor has no Class/AdditionalAttributes (CascadingValue root, by design) | WARN — uses IsOpen/IsOpenChanged not Open/OpenChanged; OnOpen, OnClose, Disabled absent | OK — no findings | OK — page exists, 4 demos, API ref, indexed | OK — 9/9 files, spinner dep declared |
| AspectRatio | WARN — Class applied to inner div only; AdditionalAttributes on outer div | OK — ChildContent + Ratio present, Size N/A | OK — no findings | OK — page exists, 3 demos, API ref, indexed | OK — 1/1 files |
| Avatar | OK — all contract checks pass | OK — Size present; Variant replaced by Shape+Status | OK — no findings | OK — page exists, 4 demos, API ref, indexed | OK — 4/4 files |
| BackToTop | OK — all contract checks pass | WARN — Disabled, Variant, Size, OnClick absent (Trigger class) | OK — no findings | WARN — not listed in ComponentsIndex | OK — 1/1 files |
| Badge | OK — all contract checks pass | OK — Variant present; Size implicitly fixed | OK — no findings | OK — page exists, 7 demos, API ref, indexed | OK — 1/1 files |
| Bento | OK — all contract checks pass | OK — ChildContent, Gap, Columns, spans present | OK — no findings | WARN — not listed in ComponentsIndex | OK — 2/2 files |
| BlurFade | OK — all contract checks pass | OK — all params present | OK — no findings | WARN — no dedicated page; lives in MotionPage.razor; not indexed | OK — 1/1 files |
| BorderBeam | OK — all contract checks pass | OK — all params present | OK — no findings | WARN — no dedicated page; lives in MotionPage.razor; not indexed | OK — 1/1 files |
| BottomNav | OK — all contract checks pass | OK — all params present for all three files | OK — no findings | WARN — not listed in ComponentsIndex | OK — 3/3 files |
| Breadcrumb | OK — all contract checks pass | OK — all params present | OK — no findings | OK — page exists, 3 demos, API ref, indexed | OK — 7/7 files |
| Button | OK — all contract checks pass | OK — Disabled, Size, Variant, OnClick all present | OK — no findings | OK — page exists, 8 demos, API ref, indexed | OK — 1/1 files, spinner dep declared |
| Calendar | OK — all contract checks pass | WARN — Disabled/Required/Invalid/ErrorText/HelperText/Label/Name absent; uses IsDateDisabled Func | OK — no findings | OK — page exists, 5 demos, API ref, indexed | OK — 1/1 files |
