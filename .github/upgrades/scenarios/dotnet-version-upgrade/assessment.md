# Projects and dependencies analysis

This document provides a comprehensive overview of the projects and their dependencies in the context of upgrading to .NETCoreApp,Version=v10.0.

## Table of Contents

- [Executive Summary](#executive-Summary)
  - [Highlevel Metrics](#highlevel-metrics)
  - [Projects Compatibility](#projects-compatibility)
  - [Package Compatibility](#package-compatibility)
  - [API Compatibility](#api-compatibility)
- [Aggregate NuGet packages details](#aggregate-nuget-packages-details)
- [Top API Migration Challenges](#top-api-migration-challenges)
  - [Technologies and Features](#technologies-and-features)
  - [Most Frequent API Issues](#most-frequent-api-issues)
- [Projects Relationship Graph](#projects-relationship-graph)
- [Project Details](#project-details)

  - [ROBdk97.XmlDocToMd.csproj](#robdk97xmldoctomdcsproj)


## Executive Summary

### Highlevel Metrics

| Metric | Count | Status |
| :--- | :---: | :--- |
| Total Projects | 1 | All require upgrade |
| Total NuGet Packages | 3 | All compatible |
| Total Code Files | 11 |  |
| Total Code Files with Incidents | 1 |  |
| Total Lines of Code | 1710 |  |
| Total Number of Issues | 2 |  |
| Estimated LOC to modify | 0+ | at least 0,0% of codebase |

### Projects Compatibility

| Project | Target Framework | Difficulty | Package Issues | API Issues | Est. LOC Impact | Description |
| :--- | :---: | :---: | :---: | :---: | :---: | :--- |
| [ROBdk97.XmlDocToMd.csproj](#robdk97xmldoctomdcsproj) | net8.0-windows;net8.0 | 🟢 Low | 1 | 0 |  | DotNetCoreApp, Sdk Style = True |

### Package Compatibility

| Status | Count | Percentage |
| :--- | :---: | :---: |
| ✅ Compatible | 3 | 100,0% |
| ⚠️ Incompatible | 0 | 0,0% |
| 🔄 Upgrade Recommended | 0 | 0,0% |
| ***Total NuGet Packages*** | ***3*** | ***100%*** |

### API Compatibility

| Category | Count | Impact |
| :--- | :---: | :--- |
| 🔴 Binary Incompatible | 0 | High - Require code changes |
| 🟡 Source Incompatible | 0 | Medium - Needs re-compilation and potential conflicting API error fixing |
| 🔵 Behavioral change | 0 | Low - Behavioral changes that may require testing at runtime |
| ✅ Compatible | 1949 |  |
| ***Total APIs Analyzed*** | ***1949*** |  |

## Aggregate NuGet packages details

| Package | Current Version | Suggested Version | Projects | Description |
| :--- | :---: | :---: | :--- | :--- |
| CommandLineParser | 2.9.1 |  | [ROBdk97.XmlDocToMd.csproj](#robdk97xmldoctomdcsproj) | ✅Compatible |
| Microsoft.CSharp | 4.7.0 |  | [ROBdk97.XmlDocToMd.csproj](#robdk97xmldoctomdcsproj) | ✅Compatible |
| System.Data.DataSetExtensions | 4.5.0 |  | [ROBdk97.XmlDocToMd.csproj](#robdk97xmldoctomdcsproj) | NuGet package functionality is included with framework reference |

## Top API Migration Challenges

### Technologies and Features

| Technology | Issues | Percentage | Migration Path |
| :--- | :---: | :---: | :--- |

### Most Frequent API Issues

| API | Count | Percentage | Category |
| :--- | :---: | :---: | :--- |

## Projects Relationship Graph

Legend:
📦 SDK-style project
⚙️ Classic project

```mermaid
flowchart LR
    P1["<b>📦&nbsp;ROBdk97.XmlDocToMd.csproj</b><br/><small>net8.0-windows;net8.0</small>"]
    click P1 "#robdk97xmldoctomdcsproj"

```

## Project Details

<a id="robdk97xmldoctomdcsproj"></a>
### ROBdk97.XmlDocToMd.csproj

#### Project Info

- **Current Target Framework:** net8.0-windows;net8.0
- **Proposed Target Framework:** net8.0-windows;net8.0;net10.0--windows;net10.0
- **SDK-style**: True
- **Project Kind:** DotNetCoreApp
- **Dependencies**: 0
- **Dependants**: 0
- **Number of Files**: 11
- **Number of Files with Incidents**: 1
- **Lines of Code**: 1710
- **Estimated LOC to modify**: 0+ (at least 0,0% of the project)

#### Dependency Graph

Legend:
📦 SDK-style project
⚙️ Classic project

```mermaid
flowchart TB
    subgraph current["ROBdk97.XmlDocToMd.csproj"]
        MAIN["<b>📦&nbsp;ROBdk97.XmlDocToMd.csproj</b><br/><small>net8.0-windows;net8.0</small>"]
        click MAIN "#robdk97xmldoctomdcsproj"
    end

```

### API Compatibility

| Category | Count | Impact |
| :--- | :---: | :--- |
| 🔴 Binary Incompatible | 0 | High - Require code changes |
| 🟡 Source Incompatible | 0 | Medium - Needs re-compilation and potential conflicting API error fixing |
| 🔵 Behavioral change | 0 | Low - Behavioral changes that may require testing at runtime |
| ✅ Compatible | 1949 |  |
| ***Total APIs Analyzed*** | ***1949*** |  |

