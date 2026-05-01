# AutobookLMM

[![GitHub license](https://img.shields.io/github/license/eryster/AutobookLMM)](https://github.com/eryster/AutobookLMM/blob/main/LICENSE)
[![GitHub stars](https://img.shields.io/github/stars/eryster/AutobookLMM)](https://github.com/eryster/AutobookLMM/stargazers)
[![GitHub forks](https://img.shields.io/github/forks/eryster/AutobookLMM)](https://github.com/eryster/AutobookLMM/network)

> [!WARNING]
> **AutobookLMM** is currently in an Alpha state and was created solely for educational and research purposes. The authors and contributors are not responsible for any account restrictions, platform policy enforcement, or damages that may result from using this automation framework.

**AutobookLMM** is a high-performance, professional modular automation library for Google NotebookLM, built on top of .NET 9 and Microsoft Playwright. It empowers developers to programmatically orchestrate complex AI workflows, manage multiple notebook sessions, upload context sources, and extract structured responses.

---

## Architectural Philosophy

The library is designed with strict separation of concerns, offering decoupled layers for robust orchestration:

- **High-Level Managers**: Provide a simplified, unified entry point for comprehensive workflow automation.
- **Abstractions Layer**: Declares clean interfaces for low-level interactions (`IAutobookManager`, `INotebookPage`, `INotebookChat`, `ISettingsPage`, and `IGeminiSession`) to guarantee enterprise-level testability and extensibility.
- **Core Session State**: Governs active browser contexts, manages authentication states via secure profiles, and coordinates internal tabs efficiently.

---

## Technical Features

### Multi-Tab Orchestration
Preloads and manages independent browser tabs (Notebook context, Settings, and Chat) simultaneously in parallel. This maximizes responsiveness and isolates actions, such as performing maintenance or deleting conversations without disrupting your main interaction.

### Authentication and State Persistence
- Built-in session state validator.
- Supports manual interactive login with automatic session persistence.
- Allows programmatic Google Cookie injection directly from raw JSON strings or local credential files.

### Notebook Management
- Automated creation, listing, and navigation of notebooks.
- Remote control of system instructions and custom prompt injection via native settings interfaces.
- Direct management of sources: programmatic uploads, selective source deletion, and real-time listing.

### Response Extraction and Streaming
- Asynchronous response polling with configurable intervals.
- Native support for real-time `IAsyncEnumerable<string>` token streaming for dynamic UI integration.
- Custom extraction scripts: Evaluate user-defined JavaScript against the page's DOM for tailored extraction pipelines.

### Data Injection Optimization
- Clipboard simulation via memory-based pasting: Direct injection of raw byte buffers bypassing traditional, slow file system uploads.

---

## Installation

### 1. Clone the Repository

```bash
git clone https://github.com/eryster/AutobookLMM.git
```

### 2. Add Project Reference

Add the reference directly to your executable project or solution:

```bash
dotnet add your-project.csproj reference path/to/AutobookLMM/src/AutobookLMM/AutobookLMM.csproj
```

### 3. Install Required Playwright Drivers

Since **AutobookLMM** uses Microsoft Playwright for its automation engine, ensure the appropriate browser binary dependencies are available in your execution environment:

```bash
pwsh bin/Debug/net9.0/playwright.ps1 install chrome
```

---

## Usage Guide

### Simple Quickstart

```csharp
using AutobookLMM.Managers;

// Initialize the high-level manager
await using var manager = new AutobookManager(headless: true);

// Configure and pre-load your targeted notebook workspace
await manager.OpenWorkspaceAsync("https://notebooklm.google.com/notebook/your-notebook-id");

// Submit a prompt and retrieve the complete response
var response = await manager.SendMessageAsync("What are the key takeaways from the provided sources?");
Console.WriteLine(response);
```

### Advanced Response Streaming

Using the internal low-level chat interface, you can subscribe to live output chunks:

```csharp
using AutobookLMM.Managers;

await using var manager = new AutobookManager(headless: true);
await manager.OpenWorkspaceAsync("https://notebooklm.google.com/notebook/your-notebook-id");

await foreach (var chunk in manager.Chat.StreamResponseAsync())
{
    Console.Write(chunk);
}
```

### Programmatic Setup of Fresh Notebooks

Initialize a notebook from scratch, upload relevant text or document sources, and apply specific system context instructions in a single pipeline:

```csharp
using System.Collections.Generic;
using AutobookLMM.Managers;

await using var manager = new AutobookManager(headless: false);

var files = new List<string> { "report2025.pdf", "data-summary.txt" };
var systemPrompt = "Analyze all user queries objectively using the attached data source.";

var metadata = await manager.InitializeNotebookAsync("Financial Review", files, systemPrompt);
Console.WriteLine($"Created Notebook: {metadata.Name} at URL: {metadata.Url}");
```

### In-Memory Multi-Modal Injection

To send message data or images directly to the active chat session without writing files to disk:

```csharp
using System.IO;
using AutobookLMM.Managers;

await using var manager = new AutobookManager();
await manager.OpenWorkspaceAsync("https://notebooklm.google.com/notebook/your-notebook-id");

byte[] imagePayload = await File.ReadAllBytesAsync("charts.png");
var prompt = "Correlate these metrics with the active workspace contents.";

var response = await manager.SendMessageAsync(prompt, new[] { imagePayload });
Console.WriteLine(response);
```

---

## Project Structural Breakdown

- **AutobookLMM.Abstractions**: Complete collection of decoupled interfaces guaranteeing a clean domain layer.
- **AutobookLMM.Core**: Handles browser lifecycle, state isolation, profiles, and cross-tab parallel processing.
- **AutobookLMM.Models**: Strongly typed representations of workspace metadata, cookie containers, and chats.
- **AutobookLMM.Pages**: Detailed automation actions mapped directly to the NotebookLM web surface.
- **AutobookLMM.Managers**: Orchestration layer combining multiple sub-services into unified developer-friendly actions.

---

## License

This project is licensed under the MIT License. See [LICENSE](https://github.com/eryster/AutobookLMM/blob/main/LICENSE) for more information.
