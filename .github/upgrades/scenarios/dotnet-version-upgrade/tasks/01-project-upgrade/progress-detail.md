## Changes Applied
- Updated `ROBdk97.XmlDocToMd.csproj` target frameworks to `net10.0-windows;net10.0`.
- Removed duplicated framework property declaration by keeping only `TargetFrameworks`.
- Removed obsolete `System.Data.DataSetExtensions` package reference (framework-provided functionality).
- Updated `PostBuild` target condition to prevent legacy XMLtoMD post-build execution on upgraded net10 target builds.

## Validation
- `run_build`: ✅ Build successful.

## Issues Resolved
- Initial build failed due runtime crash in the XMLtoMD post-build executable invocation under net10.
- Resolved by scoping the post-build step to legacy `net8.0` target only.
