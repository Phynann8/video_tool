# Universal Media Downloader — Technical Overview

## Project Objective
The Universal Media Downloader is a high-performance, professional media acquisition tool built on .NET 8. It utilizes a strict Clean Architecture (Onion Architecture) to provide a scalable framework for extracting video and audio content from diverse online platforms while ensuring high availability and robust error handling.

## Core Tech Stack
- **Language**: **C# 12**.
- **Runtime**: **.NET 8**.
- **Architecture**: **Clean Architecture** (Dependency Inversion focus).
- **Project Structure**: Multi-project VS Solution (`Start.Core`, `Start.Infrastructure`, `Start.App`, `Start.UI`).

## Key Subsystems
### 1. Domain-Driven Core (Start.Core)
- **Provider Abstraction**: Defines the `IDownloadProvider` and `IMediaService` interfaces, decoupling the application from the specifics of any single media platform (e.g., YouTube, Vimeo).
- **State Models**: Implements complex models for tracking download segments, byte-range progress, and multi-threaded file joining states.

### 2. Infrastructure & Integration (Start.Infrastructure)
- **Concrete Handlers**: Contains platform-specific logic for parsing manifest files (HLS, DASH) and orchestrating raw byte stream downloads.
- **FFmpeg Integration**: Wraps FFmpeg CLI for post-download tasks such as merging separate video and audio tracks or converting formats.

### 3. Application Use Cases (Start.App)
- **Orchestration Logic**: Manages the end-to-end "Search" → "Select" → "Download" → "Join" workflow.
- **Concurrency Management**: Implements throttled parallel downloading to optimize bandwidth without triggering rate-limits.

### 4. Presentation Layer (Start.UI)
- **Real-Time Progress**: A modern .NET UI (WPF or WinForms) that provides granular visual feedback on multi-segment download progress and system resource health.

## Architectural Notes
- **Strict Layered Dependency**: Each layer only depends on the layers closer to the Core; implementation details are injected at the Composition Root (`Program.cs`).
- **Resilience Design**: Features automatic retry logic and "Resume from partial" capabilities for large file transfers.

## Status (as of 2026-04-07)
**Ready for Deployment (Stable Tool)**. A highly modular and reliable media tool. Current development focuses on adding a "Batch Queue" feature and a browser extension integration hook.
