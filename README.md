# AutobookLMM 🚀

**AutobookLMM** is a high-performance, professional modular automation library for **Google NotebookLM**, built on top of .NET 9 and Playwright. It allows developers to orchestrate complex AI workflows, manage multiple chat sessions, and extract structured data from NotebookLM with ease.

## 🌟 Key Features

- **Professional Modular Architecture**: Separate abstractions for Notebook management, Chat interactions, and Settings.
- **Intelligent Multi-Tab Support**: Run background tasks (like clearing history) without interrupting your active session.
- **Master Blaster Messaging**: 
    - **Advanced Streaming**: Support for `IAsyncEnumerable<string>` for modern real-time UI updates.
    - **Unified API**: Send messages and receive responses with a single `SendMessageAsync` call.
    - **Memory-Based Image Pasting**: Injects images directly via JavaScript clipboard simulation (no slow file uploads).
- **Custom Extraction Scripts**: Inject your own JS logic to extract data exactly how you need it.
- **Smart Workspace Setup**: Open Notebook, Chat, and Settings tabs simultaneously with one command.
- **Full Metadata Support**: Returns rich objects with IDs and URLs for easy database integration.

## 🛠 Installation

1. **Clone the repository** and add the project reference to your solution:
   ```bash
   dotnet add reference path/to/AutobookLMM.csproj
   ```

2. **Install Playwright Browsers**:
   Since this library uses Playwright, you need to ensure the browsers are installed on your environment:
   ```bash
   pwsh bin/Debug/net9.0/playwright.ps1 install chrome
   ```

## 🚀 Quick Start

```csharp
using AutobookLMM.Core;

// 1. Initialize the session
var session = new GeminiSession();

// 2. Setup your workspace (opens all necessary tabs)
var manager = session.CreateManager();
await manager.OpenWorkspaceAsync("https://notebooklm.google.com/notebook/your-id");

// 3. Send a message and stream the response
await foreach (var chunk in session.Chat.StreamResponseAsync())
{
    Console.Write(chunk);
}
```

## 🧠 Advanced Usage

### Sending Images via Memory
```csharp
byte[] imageBytes = File.ReadAllBytes("chart.png");
await session.Chat.SendMessageAsync("Analyze this chart", new[] { imageBytes });
```

### Custom Extraction Script
```csharp
var myScript = "el => el.querySelector('.specific-class').innerText";
var result = await session.Chat.SendMessageAsync("Extract data", extractionScript: myScript);
```

### Rotating Chats (Hard Reset)
```csharp
// Deletes the current chat and opens a fresh one in one go
var newChatInfo = await manager.RotateChatAsync("Old Conversation Title");
Console.WriteLine($"New Chat URL: {newChatInfo.Url}");
```

## 📂 Project Structure

- **Core**: Manages the browser lifecycle and session state.
- **Abstractions**: Clean interfaces for easy mocking and testing.
- **Pages**: Low-level implementation of NotebookLM UI interactions.
- **Managers**: High-level orchestrators for complex workflows.

## ⚖️ License

This project is licensed under the MIT License.
