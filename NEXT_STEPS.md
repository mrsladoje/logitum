# Next Steps: Milestone 2 - Process Monitor

**Current Status**: ✅ Milestone 1 Complete - Plugin Structure Ready
**Next Goal**: Detect active window/application switches in real-time
**Branch**: `feature/process-monitor` (to be created)

---

## What We're Building

A **Process Monitor** component that:
1. Detects which application window is currently active
2. Tracks when the user switches between applications
3. Logs application names and process information
4. Fires events when context changes (for future MCP integration)

---

## Implementation Plan

### Phase 1: Core Process Detection (2-3 hours)

**Files to Create**:
```
src/LogitumAdaptiveRing/
├── Services/
│   ├── ProcessMonitor.cs         - Main process monitoring service
│   ├── IProcessMonitor.cs         - Interface for dependency injection
│   └── ProcessInfo.cs             - Data model for process information
└── Helpers/
    └── Win32Api.cs                - P/Invoke declarations for Windows APIs
```

**Key APIs to Use**:
- `GetForegroundWindow()` - Get active window handle
- `GetWindowThreadProcessId()` - Get process ID from window
- `Process.GetProcessById()` - Get process details
- `Timer` or `BackgroundWorker` - Poll for changes every 500ms

**Success Criteria**:
- ✅ Detects current active application
- ✅ Fires event when application switches
- ✅ Logs process name, window title, executable path
- ✅ No performance impact (<1% CPU usage)

---

### Phase 2: Integration with Plugin (1 hour)

**Files to Modify**:
```
src/LogitumAdaptiveRing/
├── AdaptiveRingPlugin.cs          - Wire up ProcessMonitor in Load()
└── Services/ProcessMonitor.cs     - Add event handlers
```

**Implementation**:
```csharp
// In AdaptiveRingPlugin.Load()
_processMonitor = new ProcessMonitor();
_processMonitor.ApplicationChanged += OnApplicationChanged;
_processMonitor.Start();

private void OnApplicationChanged(object sender, ProcessInfo info)
{
    this.Log.Info($"Active app changed: {info.ProcessName}");
    // TODO: Phase 3 - Query MCP Registry for this app
}
```

**Success Criteria**:
- ✅ Plugin starts monitoring on load
- ✅ Plugin stops monitoring on unload
- ✅ Logs appear in plugin debug output

---

### Phase 3: Testing Console App (1 hour)

**Files to Create**:
```
src/LogitumAdaptiveRing.TestConsole/
├── LogitumAdaptiveRing.TestConsole.csproj
└── Program.cs                     - Standalone test harness
```

**Purpose**:
- Test ProcessMonitor WITHOUT Logitech Options+ installed
- Verify detection works before plugin integration
- Debug and troubleshoot issues faster

**Test Console Features**:
```
[04:15:32] Active App: Visual Studio Code
           Process: Code.exe
           Path: C:\Users\...\Code.exe
           Window: logitum - Visual Studio Code

[04:15:45] 🔄 App Switch Detected!
           From: Visual Studio Code
           To:   Google Chrome

[04:15:48] Active App: Google Chrome
           Process: chrome.exe
           Path: C:\Program Files\Google\Chrome\Application\chrome.exe
           Window: GitHub - mrsladoje/logitum
```

**Success Criteria**:
- ✅ Console app runs standalone
- ✅ Real-time detection of app switches
- ✅ Clean, readable output
- ✅ Ctrl+C to exit gracefully

---

## Technical Implementation Details

### Windows API Approach

```csharp
// Win32Api.cs - P/Invoke declarations
[DllImport("user32.dll")]
public static extern IntPtr GetForegroundWindow();

[DllImport("user32.dll")]
public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

[DllImport("user32.dll")]
public static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);
```

### ProcessMonitor.cs - Core Logic

```csharp
public class ProcessMonitor : IDisposable
{
    private Timer _timer;
    private ProcessInfo _lastActiveProcess;

    public event EventHandler<ProcessInfo> ApplicationChanged;

    public void Start()
    {
        _timer = new Timer(CheckActiveWindow, null, 0, 500); // Poll every 500ms
    }

    private void CheckActiveWindow(object state)
    {
        var hwnd = Win32Api.GetForegroundWindow();
        Win32Api.GetWindowThreadProcessId(hwnd, out uint processId);

        var process = Process.GetProcessById((int)processId);
        var currentProcess = new ProcessInfo
        {
            ProcessId = processId,
            ProcessName = process.ProcessName,
            ExecutablePath = process.MainModule?.FileName,
            WindowTitle = GetWindowTitle(hwnd)
        };

        if (_lastActiveProcess?.ProcessName != currentProcess.ProcessName)
        {
            ApplicationChanged?.Invoke(this, currentProcess);
            _lastActiveProcess = currentProcess;
        }
    }
}
```

### ProcessInfo.cs - Data Model

```csharp
public class ProcessInfo
{
    public uint ProcessId { get; set; }
    public string ProcessName { get; set; }
    public string ExecutablePath { get; set; }
    public string WindowTitle { get; set; }
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
}
```

---

## Testing Instructions

### Option A: Test Console App (Recommended First)

**Purpose**: Verify process monitoring works before plugin integration

**Steps**:

1. **Build the test console**:
   ```bash
   cd src/LogitumAdaptiveRing.TestConsole
   dotnet build
   dotnet run
   ```

2. **Expected output**:
   ```
   🔍 MCP Adaptive Ring - Process Monitor Test
   ============================================
   Monitoring active window... Press Ctrl+C to exit

   [04:20:15] Visual Studio Code (Code.exe)
   [04:20:23] 🔄 Switch → Google Chrome (chrome.exe)
   [04:20:31] 🔄 Switch → Slack (slack.exe)
   [04:20:45] 🔄 Switch → Visual Studio Code (Code.exe)
   ```

3. **What to test**:
   - ✅ Switch between different apps → See detection events
   - ✅ Switch to same app → No duplicate events
   - ✅ CPU usage stays low (<1%)
   - ✅ Press Ctrl+C → Exits cleanly

4. **Success criteria**:
   - Detects all app switches within 500ms
   - No crashes or exceptions
   - Clean shutdown

---

### Option B: Test in Plugin (After Console Works)

**Purpose**: Verify integration with Logitech plugin

**Prerequisites**:
- ✅ Logitech Options+ installed
- ✅ MX device connected
- ✅ Plugin builds successfully

**Steps**:

1. **Build the plugin**:
   ```bash
   cd src/LogitumAdaptiveRing
   dotnet build
   ```

2. **Check plugin loaded**:
   - Open Logitech Options+
   - Go to **Settings** → **Plugins**
   - Look for "MCP Adaptive Ring"
   - Should show as **Enabled** with green status

3. **View plugin logs**:

   **Windows Event Viewer**:
   ```
   1. Open Event Viewer (eventvwr.msc)
   2. Navigate: Applications and Services Logs → Logi
   3. Look for "MCP-AdaptiveRing" entries
   ```

   **OR use DebugView** (recommended):
   ```
   1. Download DebugView from Microsoft Sysinternals
   2. Run as Administrator
   3. Enable: Capture → Capture Win32
   4. Filter for "[MCP-AdaptiveRing]"
   ```

4. **Expected log output**:
   ```
   [MCP-AdaptiveRing] Plugin constructor called
   [MCP-AdaptiveRing] Plugin loading...
   [MCP-AdaptiveRing] Starting process monitor...
   [MCP-AdaptiveRing] Plugin loaded successfully ✅
   [MCP-AdaptiveRing] Active app changed: Code
   [MCP-AdaptiveRing] Active app changed: chrome
   [MCP-AdaptiveRing] Active app changed: Slack
   ```

5. **What to test**:
   - ✅ Plugin loads without errors
   - ✅ Process monitor starts automatically
   - ✅ Logs show app switches in real-time
   - ✅ Plugin unloads cleanly (restart Options+)

6. **Success criteria**:
   - No errors in logs
   - All app switches detected
   - Plugin doesn't crash Options+

---

### Option C: Test Without Options+ (Development Mode)

**Purpose**: Test plugin code using stub classes (no Logitech software required)

**Steps**:

1. **Run plugin as console app** (temporary wrapper):
   ```bash
   # Create a quick Program.cs that instantiates the plugin
   dotnet run --project src/LogitumAdaptiveRing
   ```

2. **Modify AdaptiveRingPlugin.cs** temporarily:
   ```csharp
   // Add Main method for standalone testing
   #if DEBUG
   public static void Main(string[] args)
   {
       var plugin = new AdaptiveRingPlugin();
       plugin.Load();
       Console.WriteLine("Press any key to stop...");
       Console.ReadKey();
       plugin.Unload();
   }
   #endif
   ```

3. **Expected output**:
   ```
   [INFO] [MCP-AdaptiveRing] Plugin constructor called
   [INFO] [MCP-AdaptiveRing] Plugin loading...
   [INFO] [MCP-AdaptiveRing] Starting process monitor...
   [INFO] [MCP-AdaptiveRing] Active app changed: Code
   [INFO] [MCP-AdaptiveRing] Active app changed: chrome
   Press any key to stop...
   ```

4. **Success criteria**:
   - Runs without PluginApi.dll
   - Uses stub logger correctly
   - Process monitoring works

---

## Debugging Tips

### Issue: "GetForegroundWindow returns null"
**Cause**: Running without UI session or as service
**Fix**: Ensure console app runs in interactive user session

### Issue: "Access Denied getting process details"
**Cause**: Some processes require elevated privileges
**Fix**: Add try-catch and skip privileged processes gracefully

### Issue: "High CPU usage"
**Cause**: Polling too frequently
**Fix**: Increase timer interval from 500ms to 1000ms

### Issue: "No events firing"
**Cause**: Process name comparison case-sensitive
**Fix**: Use `StringComparer.OrdinalIgnoreCase`

---

## Performance Benchmarks

**Target**:
- CPU usage: <1% average
- Memory: <20MB
- Polling interval: 500ms (acceptable latency)
- Event detection: <500ms after switch

**Acceptable**:
- CPU usage: <2% average
- Memory: <50MB
- Polling interval: 1000ms
- Event detection: <1s after switch

---

## Code Quality Checklist

Before committing:
- [ ] All methods have XML documentation comments
- [ ] Proper exception handling with logging
- [ ] IDisposable implemented correctly
- [ ] No memory leaks (dispose timer)
- [ ] Event handlers unsubscribed in Unload()
- [ ] Unit tests added (optional for hackathon)

---

## File Checklist

**To Create**:
- [ ] `src/LogitumAdaptiveRing/Services/IProcessMonitor.cs`
- [ ] `src/LogitumAdaptiveRing/Services/ProcessMonitor.cs`
- [ ] `src/LogitumAdaptiveRing/Services/ProcessInfo.cs`
- [ ] `src/LogitumAdaptiveRing/Helpers/Win32Api.cs`
- [ ] `src/LogitumAdaptiveRing.TestConsole/Program.cs`
- [ ] `src/LogitumAdaptiveRing.TestConsole/LogitumAdaptiveRing.TestConsole.csproj`

**To Modify**:
- [ ] `src/LogitumAdaptiveRing/AdaptiveRingPlugin.cs` - Add ProcessMonitor integration
- [ ] `LogitumAdaptiveRing.sln` - Add TestConsole project
- [ ] `CLAUDE.md` - Update with Milestone 2 completion

---

## Success Metrics

**Milestone 2 is complete when**:
- ✅ ProcessMonitor class implemented and tested
- ✅ Test console app detects app switches reliably
- ✅ Plugin integration works (logs show events)
- ✅ CPU and memory usage within targets
- ✅ Code committed to `feature/process-monitor` branch
- ✅ CLAUDE.md updated with test results

---

## Next Milestone Preview

**Milestone 3: MCP Registry Integration**
- Query MCP Registry API for available servers
- Parse server metadata and tools
- Cache results in SQLite database
- Display available tools for current app

---

**Ready to start?** Let me know and I'll create the `feature/process-monitor` branch and begin implementation! 🚀
