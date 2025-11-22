# Logitum Adaptive Ring Plugin

Logi Plugin SDK plugin for adaptive ring functionality on Logitech devices.

## Structure

- `AdaptiveRingPlugin/` - Main plugin implementation
- `ReferencePlugin/` - Example reference plugin

## Development

Build:
```bash
dotnet build
```

Run:
```bash
dotnet run --project AdaptiveRingPlugin
```

## Plugin Details

- Uses Logi Plugin SDK (.NET)
- Implements adaptive ring controls for compatible devices
- Deployed to: `C:\Users\panonit\AppData\Local\Logi\LogiPluginService\Plugins`

## Key Files

- `AdaptiveRingPlugin.cs` - Main plugin logic
- `plugin.json` - Plugin metadata
