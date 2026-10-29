# Changelog

## [1.4.0.0] - 2026-10-30

### Added
- **Single-Instance Enforcement & Named Pipe IPC Wake**: Introduced `SingleInstanceService` using system named mutex (`Local\TapStrum_SingleInstance_Mutex`) and named pipe IPC (`Local\TapStrum_Wake_Pipe`) to eliminate duplicate background instances and summon the existing window to front upon secondary launches (PR #54).
- **WinUI 3 MVVM Architecture & Modular ViewModels**: Integrated `CommunityToolkit.Mvvm`, extracted independent views (`TyperView`, `HolderView`, `ClickerView`, `MacroView`, `SettingsView`, `AboutView`), and introduced `TaskViewModelBase` for unified asynchronous execution lifecycle and panic handling (PR #65).
- **Decoupled Core Domain Services**: Extracted dedicated interfaces and implementation engines for core capabilities (`IClickerService`, `ITyperService`, `IKeyHolderPipelineService`, `IAppSettingsService`) with shared `PanicDelayHelper` safety delay coordination (PR #64).
- **Dynamic JSON Localization Engine**: Replaced hardcoded string resources with dynamic JSON localization dictionaries (`zh-TW`, `zh-CN`, `en-US`, `ja-JP`), enabling runtime language switching and seamless community translation extensibility (PR #66).
- **Store Release Enrichment & Verification Suite**: Added Microsoft Store package probing, reconciliation workflow, and verification scripts to guarantee parity between GitHub releases and Microsoft Store packages (PR #58).
- **Direct Store Page Link & Error Dialog**: Added a dedicated Microsoft Store navigation button in AboutView with user-facing dialog fallback on failure (PR #57).
- **Guarded Branch Creation & Pre-Flight Toolchain**: Added `branch` subcommand to `pr_tool` and git `post-checkout` hook to mechanically prevent non-compliant branch naming (PR #59, PR #68).

### Changed
- **Comprehensive Brand Identity & Topology Consolidation**: Formally rebranded from Tapster to TapStrum across all assemblies, namespaces, project files, executables (`TapStrum.Core`, `TapStrum.Fluent`, `TapStrum.Launcher`), and Microsoft Partner Center package identities (PR #53, PR #63, PR #66, PR #67).
- **Full Namespace Standardization**: Harmonized internal namespaces across 82 C# source files to canonical `TapStrum`, `TapStrum.Fluent`, and `TapStrum.Services` (PR #67).
- **Monolithic Page Decomposition**: Refactored monolithic `MainPage` into a pure, lightweight navigation container hosting modular views (PR #65).
- **Standard Source Layout**: Relocated all project source trees into a unified `src/` directory topology and eliminated legacy WPF projects and solutions (PR #63, PR #67).
- **MSIX Package Footprint Optimization**: Retargeted Windows SDK to `10.0.19041.0` and adopted fail-safe file trimming and completeness gates, drastically reducing MSIX package size (PR #52).

### Fixed
- **Static Analysis & Anti-Pattern Remediation**: Resolved cyclomatic complexity warnings, unmanaged P/Invoke visibility (`CS-W1059`), uninitialized variable risks (`CS-W1022`), culture-invariant string comparisons (`CS-R1018`), and async cancellation token ordering (`CS-R1141`) across core modules (PR #60, PR #63, PR #66).
- **Process Enumeration Safety**: Materialized process queries to arrays in launcher instance helpers to avoid enumeration concurrency exceptions (PR #66).
- **GitHub Actions Least-Privilege Permissions**: Hardened CI workflow token scopes, granting explicit read permissions to caller workflows and strictly isolating release publishing privileges (PR #51, PR #61).
- **Unit Test Coverage Expansion**: Added 21 Fluent ViewModel unit tests, single instance tests, and core domain service tests, bringing total test count to 321 passing unit tests (PR #54, PR #64, PR #65).

## [1.3.0.0] - 2026-10-10

### Added
- **Multi-Language Localization Engine**: Full runtime dynamic UI localization across Traditional Chinese (繁體中文), Simplified Chinese (简体中文), English, and Japanese (日本語), supporting persistent app settings and automatic system default language matching (PR #49).
- **Multi-Stage Key Holder Pipeline**: Interactive step timeline with live step cards, customizable repeat counts, configurable step durations, and seamless combo-to-pipeline additions (PR #48).
- **96% Compact Virtual Keyboard**: Integrated full 4-key navigation arrow cluster and dedicated 17-key numpad with dual-column responsive grid layout and bidirectional key state highlights (PR #47).

### Changed
- **Unified Navigation Labels**: Streamlined navigation tabs into concise two-character Chinese labels (打字, 長按, 連點, 重放) and eliminated mixed English/Chinese labels throughout all panels (PR #49).
- **Fluent UI Accessibility & Layout**: Added full scrollviewer wrapping across all main functional panels (Typer, Holder, Clicker, Macro) with minimum window height and width safeguards to prevent UI clipping on compact screens (PR #46).
- **Input Validation & Safety Guards**: Disabled execution buttons when necessary inputs are empty to prevent invalid operations (PR #46).

### Fixed
- **Code Quality & Cognitive Complexity**: Decomposed large UI localization and step rendering methods into static null-safe setters, reducing SonarCloud cognitive complexity to 0 and eliminating duplicated string literals (PR #49).
- **Virtual Keyboard Spacing & Layout Alignment**: Fixed numpad alignment gap, eliminated unwanted nested scrollviewers, and centered the virtual keyboard in the step card pipeline (PR #47, PR #48).

## [1.2.0.0] - 2026-10-08

### Added
- **Hardware Panic Kill & Safety Safeguards**: Mouse shake gesture detection (>2500 px/s threshold), rapid triple-Esc cancellation guard (<500ms sliding window), and global F10 panic hotkey (PR #44).
- **Click Target Marker Overlay**: Zero-lag layered overlay window using native GDI `UpdateLayeredWindow`, sequential numbering tags, dynamic click ripple animations, and coordinate picker (PR #40, PR #41).
- **Market Convenience Suite**: Global action hotkeys (F6–F9), Hold-to-Click mode, dedicated Key Spammer, time and position jitter anti-detection (PR #36, PR #37).
- **Macro Filtering & Editor**: Live mouse move noise filtering, macro step deletion, and fine-grained macro inspector (PR #39).
- **Clipboard Typer Mode**: High-speed large text typing via Windows clipboard with trailing Enter/Tab keys and automated clipboard content restoration (PR #38).

### Changed
- Refactored `MacroRecorder` replay execution to minimize cognitive complexity and improve thread safety (PR #44).
- Transitioned window drag handling to native DWM caption hit-testing, eliminating mouse drag latency on high-polling rate mice (PR #35).
- Updated project test tooling (Microsoft.NET.Test.Sdk 18.10.1, xunit.runner.visualstudio 4.0.0, coverlet.collector 10.1.0) and dependencies to their latest compatible versions (PR #42, PR #43).

### Fixed
- Fixed launcher portable extraction path to dynamically derive application version, preventing downgrade extraction collisions.
- Fixed About screen release badge to reflect current 1.2.0.0 release.

## [1.1.1.0] - 2026-10-04

### Fixed
- Fixed MSIX launch crash caused by duplicate WinUI 3 runtime binaries and unpackaged packaging flags conflicting with `Microsoft.WindowsAppRuntime.2` (`CoreMessagingXP.dll` `0xc0000602`).
