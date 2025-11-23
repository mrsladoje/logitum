<div align="center">

# 🎯 Logitum Adaptive Ring

### _Your Hardware Just Got Smarter_

**AI-powered, context-aware actions at your fingertips. Literally.**

[![Made for Logitech](https://img.shields.io/badge/Made%20for-Logitech-00B8FC?style=for-the-badge&logo=logitech&logoColor=white)](https://logitech.com)
[![HackaTUM 2025](https://img.shields.io/badge/HackaTUM-2025-FF6B35?style=for-the-badge&logo=hack-the-box&logoColor=white)](https://hack.tum.de/)
[![Built with MCP](https://img.shields.io/badge/MCP-Powered-7C3AED?style=for-the-badge&logo=protocol&logoColor=white)](https://modelcontextprotocol.io)

<img src="https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square&logo=dotnet&logoColor=white" />
<img src="https://img.shields.io/badge/C%23-12.0-239120?style=flat-square&logo=csharp&logoColor=white" />
<img src="https://img.shields.io/badge/AI-Gemini-4285F4?style=flat-square&logo=google&logoColor=white" />
<img src="https://img.shields.io/badge/Embeddings-Voyage%20AI-FF6F61?style=flat-square" />

[Features](#-key-features) •
[How It Works](#-how-it-works) •
[Installation](#-installation) •
[Demo](#-demo) •
[Architecture](#-architecture)

---

</div>

## 🤔 The Problem

You know that feeling when you're drowning in tabs, constantly switching contexts, hunting through menus for features you _know_ exist somewhere?

```
┌─────────────────────────────────────────────┐
│  Monday, 9:47 AM                            │
│  • 27 browser tabs open                     │
│  • Switched apps 184 times                  │
│  • Clicked through menus... so many menus   │
│  • Lost in tool overload                    │
└─────────────────────────────────────────────┘
```

**We've all been there.** It's like living in the 90s, but with more RAM.

## 💡 The Solution

What if your Logitech device _just knew_ what you needed? What if it adapted to your workflow, learned your patterns, and brought the right tools to your fingertips **exactly when you need them**?

### Enter: **Logitum Adaptive Ring** 🎉

A Logitech plugin that transforms your MX device into an intelligent, self-adapting command center powered by **6,000+ AI tools** through the Model Context Protocol.

<div align="center">

### ✨ Three Words: **Intelligence. Adaptation. Zero Config.**

</div>

---

## 🚀 Key Features

### 🎯 **Instant Intelligence**

Switch to VS Code? Get _Commit_, _Push_, _Debug_, _Run Tests_.  
Switch to Slack? Get _Send Message_, _@Mention_, _React_, _Search_.  
Switch to Chrome? Get _Bookmark_, _New Tab_, _History_, _Search_.

**All automatically.** No configuration. No setup. Just works.

### 🧠 **Learns Your Workflow**

The system tracks your behavior (with your permission, always):

- Notices you run tests in VS Code 20 times a week
- Automatically adds it to your actions ring
- **Hardware that adapts to YOU**

### 🌐 **6,000+ Apps via MCP**

Built on the [Model Context Protocol](https://modelcontextprotocol.io) — the open standard adopted by:

- ✅ OpenAI (March 2025)
- ✅ Google DeepMind (April 2025)
- ✅ Microsoft & Anthropic
- ✅ 6,480+ servers and growing

### 🔮 **Future-Proof**

New MCP server published tomorrow? **You get it automatically.** No plugin updates. No configuration. It just appears.

---

## 🎬 How It Works

```
┌─────────────────────────────────────────────────────────────────┐
│                    🔄 THE ADAPTIVE CYCLE                        │
└─────────────────────────────────────────────────────────────────┘

    1️⃣  You switch apps            →  Process Monitor detects it
                ↓
    2️⃣  Query MCP Registry        →  Find relevant servers
                ↓
    3️⃣  AI suggests workflows     →  "Commit", "Push", "Debug"
                ↓
    4️⃣  Actions Ring updates       →  Instantly available
                ↓
    5️⃣  You click action           →  MCP executes the tool
                ↓
    6️⃣  UI Monitor tracks patterns →  "Hmm, they do this a lot..."
                ↓
    7️⃣  AI interprets behavior    →  "User runs tests frequently"
                ↓
    8️⃣  Vector clustering          →  Similar actions group together
                ↓
    9️⃣  Frequency threshold hit    →  "Want to add 'Run Tests'?"
                ↓
    🔟  Ring updated               →  Ring adapts permanently

                    ♻️  REPEAT. IMPROVE. ADAPT.
```

---

## 🏗️ Architecture

### Three-Tier Intelligence System

```
┌─────────────────────────────────────────────────────┐
│  TIER 1: DISCOVERY (Instant)                        │
│  • MCP Registry lookup on app switch                │
│  • AI suggests 6-8 common workflows                 │
│  • Actions Ring populates in <500ms                 │
└─────────────────────────────────────────────────────┘
                       ↓
┌─────────────────────────────────────────────────────┐
│  TIER 2: OBSERVATION (Passive)                      │
│  • Windows UI Automation monitors clicks            │
│  • Tracks focus changes, inputs, shortcuts          │
│  • Stores interaction data (24-hour TTL)            │
└─────────────────────────────────────────────────────┘
                       ↓
┌─────────────────────────────────────────────────────┐
│  TIER 3: ADAPTATION (Intelligent)                   │
│  • Gemini AI interprets action sequences            │
│  • VoyageAI embeddings cluster similar patterns     │
│  • DBSCAN algorithm identifies workflows            │
└─────────────────────────────────────────────────────┘
```

### Tech Stack

| Layer              | Technology                  | Why We Love It                              |
| ------------------ | --------------------------- | ------------------------------------------- |
| 🎮 **Plugin**      | Logitech Actions SDK (C#)   | Native hardware integration                 |
| 🤖 **AI Brain**    | Google Gemini               | Workflow understanding & suggestions        |
| 🧮 **Embeddings**  | VoyageAI (`voyage-code-3`)  | Semantic similarity detection               |
| 📊 **Clustering**  | DBSCAN + Cosine Similarity  | Pattern recognition                         |
| 👀 **UI Tracking** | Windows UI Automation       | Behavioral learning                         |
| 🗄️ **Storage**     | SQLite + Milvus (vector DB) | Fast, local (if you set Milvus so), private |
| 🌐 **Ecosystem**   | Model Context Protocol      | 6,000+ tools, one standard                  |

---

## 🎨 What Makes This Special?

### vs. Traditional Solutions

| Approach                   | Limitation                         | Logitum                                   |
| -------------------------- | ---------------------------------- | ----------------------------------------- |
| **Keyboard Shortcuts**     | Memorize 100+ combinations         | ✅ Visual, contextual, max 8 at once      |
| **Command Palettes**       | Still requires typing & searching  | ✅ Physical button, instant               |
| **Claude Desktop**         | Shows ALL tools, manual navigation | ✅ Shows RELEVANT tools, learns           |
| **Logitech Smart Actions** | Manual setup, no MCP, static       | ✅ Auto MCP discovery, AI-powered, adapts |
| **Other Plugins**          | App-specific, limited scope        | ✅ Universal (6k+ apps), future-proof     |

### 🎯 The Killer Combo

1. **Physical Interface** - Tactile, instant, no context switching
2. **AI-Powered** - Understands context and intent
3. **Self-Learning** - Improves with your usage
4. **Open Standard** - MCP means infinite extensibility
5. **Zero Config** - Works immediately, improves automatically

---

## 🔧 Installation

### Prerequisites

- **Hardware**: Logitech MX Master 3/4, Loupedeck CT/Live/Live S, or Razer Stream Controller
- **OS**: Windows 10/11 (macOS support planned)
- **Software**: Logitech Options+ / Loupedeck Software
- **.NET**: 8.0 or higher

### Setup

```bash
# Clone the repo
git clone https://github.com/yourusername/logitum.git
cd logitum/AdaptiveRingPlugin

# Set up environment variables
$env:GEMINI_API_KEY="your-gemini-api-key"
$env:VOYAGEAI_API_KEY="your-voyageai-api-key"

# Build the plugin
cd src
dotnet build -c Release

# Plugin auto-deploys to:
# C:\Users\<YOU>\AppData\Local\Logi\LogiPluginService\Plugins\
```

### Get API Keys (Free Tiers Available!)

- **Gemini**: [aistudio.google.com](https://aistudio.google.com)
- **VoyageAI**: [voyageai.com](https://www.voyageai.com)

---

## 🧪 Development

### Project Structure

```
logitum/
├── AdaptiveRingPlugin/
│   ├── src/
│   │   ├── Actions/              # Command implementations
│   │   ├── Services/             # Core intelligence
│   │   │   ├── ProcessMonitor.cs         # App detection
│   │   │   ├── MCPRegistryClient.cs      # MCP integration
│   │   │   ├── GeminiActionSuggestor.cs  # AI suggestions
│   │   │   ├── UIInteractionMonitor.cs   # Behavior tracking
│   │   │   ├── SemanticWorkflowProcessor.cs  # Pattern analysis
│   │   │   ├── VoyageAIClient.cs         # Embeddings
│   │   │   ├── VectorClusteringService.cs    # DBSCAN
│   │   │   └── ActionRankingService.cs   # Smart sorting
│   │   ├── Models/               # Data structures
│   │   ├── Helpers/              # Utilities
│   │   └── Scripts/              # Python AI services
│   └── bin/Release/              # Compiled plugin
├── PLAN.md                       # Original hackathon plan
├── FEATURE_IMPLEMENTATION_SUMMARY.md  # Technical docs
└── README.md                     # You are here! 👋
```

### Build from Source

```bash
# Debug build
dotnet build

# Release build (optimized)
dotnet build -c Release

# Watch mode (auto-rebuild)
dotnet watch build
```

### Run Tests

```bash
# Unit tests
dotnet test

# Integration tests (requires hardware)
dotnet test --filter Category=Integration
```

---

## 🗄️ Database Schema

```sql
-- Core tables
apps                    → Registered applications
app_actions            → MCP actions per app
mcp_servers           → Known MCP registry entries

-- Intelligence tables
ui_interactions       → Raw UI events (24h TTL)
semantic_workflows    → AI-interpreted patterns
workflow_embeddings   → Vector representations
workflow_clusters     → Grouped similar actions

-- All stored locally in:
-- C:\Users\<YOU>\AppData\Local\Logitum\adaptive_ring.db
```

---

## 🔐 Privacy & Security

### What We Track (If You Enable Learning)

✅ **App names** (e.g., "chrome.exe", "slack.exe")  
✅ **Button clicks** (e.g., "clicked 'Commit'")  
✅ **Window titles** (e.g., "Gmail - Inbox")  
✅ **Action sequences** (e.g., "Type→Click→Submit")

### What We DON'T Track

❌ **Passwords** (masked as `******`)  
❌ **Email/message content**  
❌ **Browsing history URLs**  
❌ **File paths or code**

### Where Data Lives

🏠 **100% Local** - Everything stored on your machine (and could stay there if you use a local LLM)
🔒 **No Cloud Sync** (unless you enable it)  
🗑️ **24-Hour Auto-Delete** for raw UI events

### API Calls

Only sent to **Gemini** (workflow interpretation) and **VoyageAI** (embeddings).  
**You control the API keys.**

---

## 🌟 Roadmap

### ✅ Completed (HackaTUM 2025)

- [x] MCP Registry integration
- [x] Gemini AI workflow suggestions
- [x] Windows UI Automation tracking
- [x] Vector embedding clustering
- [x] Adaptive action learning
- [x] SQLite + Milvus storage

### 🚧 In Progress

- [ ] macOS support (via NSAccessibility)
- [ ] Manual action editor UI
- [ ] Export/import action profiles
- [ ] Performance optimization (caching)

### 🔮 Future Vision

- [ ] Multi-device sync (encrypted cloud)
- [ ] Voice control integration
- [ ] Cross-app workflow automation
- [ ] Team collaboration (shared profiles)
- [ ] Logitech Marketplace submission
- [ ] Plugin SDK for custom actions

---

## 🤝 Contributing

We'd love your help! Here's how:

### 🐛 Report Bugs

Open an issue with:

- OS version
- Logitech device model
- Steps to reproduce
- Log files (found in `C:\Users\<YOU>\AppData\Local\Logi\LogiPluginService\Logs\`)

### 💡 Suggest Features

Open an issue with the `enhancement` label.

### 📝 Improve Docs

Found a typo? Explanation unclear? PRs welcome!

---

## 🙏 Acknowledgments

### Built With

- **[Logitech Actions SDK](https://logitech.github.io/actions-sdk-docs/)** - Hardware magic
- **[Model Context Protocol](https://modelcontextprotocol.io)** - Open AI standard
- **[Google Gemini](https://ai.google.dev/)** - Workflow intelligence
- **[VoyageAI](https://www.voyageai.com)** - Semantic embeddings
- **[MCP Registry](https://registry.modelcontextprotocol.io)** - Tool discovery

### Inspired By

- The frustration of tab overload
- The vision of tangible computing
- The power of open standards
- The community of 6,000+ MCP servers

### Special Thanks

- **Logitech** - For making incredible hardware and opening it up to developers
- **HackaTUM** - For the challenge that sparked this
- **MCP Community** - For building the future of AI interoperability
- **You** - For checking out this project! 🙌

---

## 📞 Contact & Support

### Get Help

- 💬 **GitHub Issues**: [github.com/mrsladoje/logitum/issues](https://github.com/yourusername/logitum/issues)
- 📧 **Email**: mrsladoje@gmail.com

### Stay Updated

⭐ **Star this repo** to follow development  
👀 **Watch** for release notifications  
🔔 **Subscribe** to issues for discussions

---

<div align="center">

## 🚀 Ready to Make Your Hardware Smarter?

**[⬇️ Download Latest Release](https://github.com/mrsladoje/logitum/releases)** • **[🎥 Watch Demo](https://www.youtube.com/watch?v=5bFkX00qs2A)**

---

### _"The best way to predict the future is to build it."_

Made with ❤️ in 36 hours for HackaTUM 2025

**[Back to Top ↑](#-logitum-adaptive-ring)**

</div>

---

<div align="center">

### 🌟 If this project helped you, give it a star! 🌟

[![GitHub stars](https://img.shields.io/github/stars/yourusername/logitum?style=social)](https://github.com/yourusername/logitum)
[![GitHub forks](https://img.shields.io/github/forks/yourusername/logitum?style=social)](https://github.com/yourusername/logitum/fork)
[![GitHub watchers](https://img.shields.io/github/watchers/yourusername/logitum?style=social)](https://github.com/yourusername/logitum)

</div>
