# AutobookLMM `Alpha Version`

[![GitHub license](https://img.shields.io/github/license/eryster/AutobookLMM)](https://github.com/eryster/AutobookLMM/blob/main/LICENSE)
[![GitHub stars](https://img.shields.io/github/stars/eryster/AutobookLMM)](https://github.com/eryster/AutobookLMM/stargazers)
[![GitHub forks](https://img.shields.io/github/forks/eryster/AutobookLMM)](https://github.com/eryster/AutobookLMM/network)

> [!WARNING]
> **AutobookLMM** is currently in an **Alpha** state. This library was created solely for **educational and research purposes**. 
> The authors and contributors are not responsible for any bans, restrictions, or damages that may result from using this automation on Google NotebookLM. Use at your own risk.

**AutobookLMM** is a high-performance, professional modular automation library for **Google NotebookLM**, built on top of .NET 9 and Playwright. It allows developers to orchestrate complex AI workflows, manage multiple chat sessions, and extract structured data from NotebookLM with ease.

## Key Features

- **Professional Modular Architecture**: Separate abstractions for Notebook management, Chat interactions, and Settings.
- **Intelligent Multi-Tab Support**: Run background tasks (like clearing history) without interrupting your active session.
- **Master Blaster Messaging**: 
    - **Advanced Streaming**: Support for `IAsyncEnumerable<string>` for modern real-time UI updates.
    - **Unified API**: Send messages and receive responses with a single `SendMessageAsync` call.
    - **Memory-Based Image Pasting**: Injects images directly via JavaScript clipboard simulation (no slow file uploads).
- **Custom Extraction Scripts**: Inject your own JS logic to extract data exactly how you need it.
- **Smart Workspace Setup**: Open Notebook, Chat, and Settings tabs simultaneously with one command.
- **Full Metadata Support**: Returns rich objects with IDs and URLs for easy database integration.

## Installation Tutorial

To install and integrate **AutobookLMM** into your project, follow the instructions below using the official GitHub repository source:

### 1. Clone the repository
Clone the official repository from GitHub:
```bash
git clone https://github.com/eryster/AutobookLMM.git
```

### 2. Add Project Reference
Add the project reference to your own solution or project:
```bash
dotnet add your-project.csproj reference path/to/AutobookLMM/src/AutobookLMM/AutobookLMM.csproj
```

### 3. Install Playwright Browsers
Since **AutobookLMM** uses Playwright under the hood, make sure to install the Chrome browser:
```bash
pwsh bin/Debug/net9.0/playwright.ps1 install chrome
```

## Quick Start

```csharp
using AutobookLMM.Managers;

// 1. Initialize the manager directly (supports passing headless mode)
await using var manager = new AutobookManager(headless: true);

// 2. Setup your workspace (opens all necessary tabs and ensures you are logged in)
await manager.OpenWorkspaceAsync("https://notebooklm.google.com/notebook/your-id");

// 3. Send a message and wait for the response
var response = await manager.SendMessageAsync("What is this notebook about?");
Console.WriteLine(response);

// 4. Alternatively, use the internal low-level Chat tab directly for advanced streaming
await foreach (var chunk in manager.Chat.StreamResponseAsync())
{
    Console.Write(chunk);
}
```

## Advanced Usage

### Sending Images via Memory
```csharp
byte[] imageBytes = File.ReadAllBytes("chart.png");
await manager.SendMessageAsync("Analyze this chart", new[] { imageBytes });
```

### Custom Extraction Script
```csharp
var myScript = "el => el.querySelector('.specific-class').innerText";
var result = await manager.SendMessageAsync("Extract data", extractionScript: myScript);
```

### Rotating Chats (Hard Reset)
```csharp
// Deletes the current chat, starts a fresh one, and sends the first message
var newChatInfo = await manager.RotateChatAsync("Old Conversation Title", "Hello again!");
Console.WriteLine($"New Chat URL: {newChatInfo.Url}");
```

## Project Structure

- **Core**: Manages the browser lifecycle and session state.
- **Abstractions**: Clean interfaces for easy mocking and testing.
- **Pages**: Low-level implementation of NotebookLM UI interactions.
- **Managers**: High-level orchestrators for complex workflows.

## License

This project is licensed under the MIT License. See [LICENSE](https://github.com/eryster/AutobookLMM/blob/main/LICENSE) for more information.
