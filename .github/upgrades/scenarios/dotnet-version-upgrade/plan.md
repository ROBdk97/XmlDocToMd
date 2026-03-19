# .NET Version Upgrade Plan

## Overview

**Target**: Upgrade `ROBdk97.XmlDocToMd` from `.NET 8` to `.NET 10`.
**Scope**: Single SDK-style project with low complexity and no project-to-project dependencies.

### Selected Strategy
**All-At-Once** — All projects upgraded simultaneously in a single operation.
**Rationale**: 1 project, already on modern .NET (`net8.0`/`net8.0-windows`), no dependency tiers, no API incompatibilities reported.

## Tasks

### 01-project-upgrade: Upgrade target frameworks and dependencies for ROBdk97.XmlDocToMd

Upgrade the project target frameworks to .NET 10 and apply package/configuration changes required by the assessment, including removal of package references whose functionality is now framework-provided. Then validate restore/build and fix any resulting compile issues.

**Done when**: The project targets `.NET 10` for both base and windows-specific TFMs as needed, package references are compatible and cleaned up, and solution build succeeds with zero errors.

---

### 02-test-and-finalize: Validate upgraded solution and finalize upgrade artifacts

Run available tests and final validation after the framework and dependency upgrade is complete. Confirm no regressions from the upgrade and that modernization artifacts reflect final state.

**Done when**: Tests (if present) pass, build remains green, and workflow execution is ready to complete.
