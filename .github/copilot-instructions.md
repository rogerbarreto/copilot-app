# Copilot Instructions

## Git Commits

- **Never** add `Co-authored-by` trailers to commits.
- Keep commit messages concise and descriptive.

## Code Style

- C# with nullable enabled, WinForms (.NET 10).
- `dotnet format` enforced — run before committing.
- Internal classes visible to tests via `InternalsVisibleTo`.

## Testing

- xUnit (not MSTest).
- Validate assertions with integration tests whenever possible.

## Release

- Update version in both `CopilotApp.csproj` and `installer.iss`.
- Update `CHANGELOG.md` before tagging.
- Push `v<version>` tag to trigger release CI.
