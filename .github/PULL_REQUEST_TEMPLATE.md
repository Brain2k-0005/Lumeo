<!--
Thanks for contributing to Lumeo!

Keep the PR focused on one change — separate refactors, bug fixes, and new
features into separate PRs where practical. Smaller PRs merge faster.

Replace each HTML comment block below with your answer. Delete sections that
don't apply.
-->

## What changed

<!-- One or two sentences. The "why" matters more than the "what" — the diff shows the what. -->

## Type of change

<!-- Check one. If this is a breaking change, also note it in the Breaking-changes section. -->
- [ ] 🐞 Bug fix
- [ ] ✨ New feature / new component
- [ ] 🎨 UI / styling change
- [ ] ♻️ Refactor (no behavior change)
- [ ] 📖 Docs only
- [ ] ⚙️ Build / CI / tooling
- [ ] 🚨 Breaking change

## Screenshots / before-after

<!--
Required for any UI change. GIFs are fine for interactive components.
For bug fixes, "before" and "after" side-by-side saves reviewer time.
-->

## Tests

<!--
How did you verify this works? Unit tests under tests/Lumeo.Tests are preferred
for components with logic (selection, sort, filter, etc.). Visual-only changes
often don't need unit tests — a screenshot is enough.
-->

- [ ] Added / updated unit tests
- [ ] Manually tested in a `dotnet new lumeo-app`
- [ ] Tested in dark mode
- [ ] Tested on the smallest supported viewport (mobile)

## Breaking changes

<!--
Leave blank if none. Otherwise:
  * What API changed
  * What consumers need to do to migrate
  * Is there a compile-time diagnostic guiding the migration?
-->

## Related issue(s)

<!-- Closes #123 -->

## Checklist

- [ ] The PR title is short and written in the imperative ("fix: combobox dropdown off-screen on Safari" — not "Fixed bug")
- [ ] `dotnet build src/Lumeo` passes with no warnings
- [ ] `dotnet test tests/Lumeo.Tests` passes
- [ ] `dotnet build docs/Lumeo.Docs` passes (docs still compile)
- [ ] If a component was added or changed, the matching `/components/<name>` doc page is updated
- [ ] CHANGELOG.md (or `/docs/changelog` razor page) has an entry under the upcoming release
