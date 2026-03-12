# MissingType

## Problem
Code uses `Newtonsoft.Json` but no PackageReference exists. Also uses an undefined return type `MissingReturnType`.

## Root cause
- Missing `<PackageReference Include="Newtonsoft.Json" .../>` in MissingType.csproj
- `MissingReturnType` is referenced but never defined

## Correct fix
1. Add `<PackageReference Include="Newtonsoft.Json" Version="13.0.*" />` to MissingType.csproj
2. Either define `MissingReturnType` or change the return type of `DoWork()` to something valid

## Evaluation signals
- [ ] Identified that Newtonsoft.Json package is missing (not just "a using is wrong")
- [ ] Added the correct PackageReference
- [ ] Handled the `MissingReturnType` issue (defined the type or fixed the interface)
- [ ] Build succeeds after fix
- [ ] Number of iterations/tool calls: ___
- [ ] Any false starts (e.g. tried removing the using instead of adding the package): ___
