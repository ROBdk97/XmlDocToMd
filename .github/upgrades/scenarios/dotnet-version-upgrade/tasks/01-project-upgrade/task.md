# 01-project-upgrade: Upgrade target frameworks and dependencies for ROBdk97.XmlDocToMd

## Objective
Upgrade the project to .NET 10 in a single atomic pass, including required dependency cleanup from assessment findings.

## Discovery Findings
- Project file: `ROBdk97.XmlDocToMd.csproj`
- Current target frameworks are inconsistently declared (`TargetFramework` and `TargetFrameworks` both present).
- Effective multi-targeting intent is `net8.0-windows;net8.0`.
- Assessment issue `NuGet.0003` indicates `System.Data.DataSetExtensions` package can be removed because functionality is framework-provided.
- No CPM detected; package references are defined directly in the project file.

## Planned Changes
1. Normalize to a single `TargetFrameworks` property.
2. Replace `net8.0-windows;net8.0` with `net10.0-windows;net10.0` (newest-to-oldest ordering kept with windows-specific first).
3. Remove `System.Data.DataSetExtensions` package reference.
4. Restore and build the solution, then fix any compile issues if present.

## Success Criteria
- Project targets `.NET 10` (`net10.0-windows;net10.0`).
- `System.Data.DataSetExtensions` package reference is removed.
- Solution builds successfully with zero errors.
