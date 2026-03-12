# BadProps

## Problem
A `Directory.Build.props` sets `TreatWarningsAsErrors=true`, overrides `OutputPath` to a nonsensical path, and sets `LangVersion=preview`. Nullable warnings become build errors.

## Root cause
- `Directory.Build.props` sets `TreatWarningsAsErrors=true` — promoting CS8600, CS8602, CS0219 warnings to errors
- `OutputPath` is overridden to `..\..\nonexistent\output\`
- The code has genuine nullable usage issues that become errors under this configuration

## Correct fix
Either:
- **Option A (fix the code):** Fix the nullable warnings in Program.cs (null checks, proper nullable annotations)
- **Option B (fix the props):** Remove or set `TreatWarningsAsErrors=false` in Directory.Build.props
- **Option C (targeted):** Suppress specific warnings with `<NoWarn>` or `#pragma warning disable`

The key insight is that the errors are **promoted warnings**, not real errors — and the root cause is in Directory.Build.props, not in the .csproj.

## Evaluation signals
- [ ] Identified that errors are warnings promoted by `TreatWarningsAsErrors=true`
- [ ] Found that `Directory.Build.props` is the source of the configuration
- [ ] Noticed the overridden `OutputPath` (even if not directly causing the error)
- [ ] Applied a reasonable fix
- [ ] Build succeeds after fix
- [ ] Number of iterations/tool calls: ___
- [ ] Did Copilot look at properties/imports or just try to fix code directly: ___
