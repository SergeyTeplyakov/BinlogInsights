# RestoreFail

## Problem
The project references a NuGet package that doesn't exist on any configured source.

## Root cause
`<PackageReference Include="Totally.Fake.Package.DoesNotExist.XYZ" Version="99.0.0" />` in the csproj. This package does not exist on nuget.org.

## Correct fix
Remove the `Totally.Fake.Package.DoesNotExist.XYZ` PackageReference (the code doesn't actually use it).

## Evaluation signals
- [ ] Identified the exact package name causing NU1101
- [ ] Recognized it's a restore-time failure, not a compile-time failure
- [ ] Removed the bad PackageReference (not the Newtonsoft.Json one)
- [ ] Build succeeds after fix
- [ ] Number of iterations/tool calls: ___
- [ ] Did Copilot check NuGet restore diagnostics or just read the error message: ___
