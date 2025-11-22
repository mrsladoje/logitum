namespace Loupedeck.AdaptiveRingPlugin.Actions
{
    using System;
    using System.Collections.Generic;
    using Loupedeck;
    using Loupedeck.AdaptiveRingPlugin.Models;
    using Loupedeck.AdaptiveRingPlugin.Services;
    using System.Text.Json;
    using Loupedeck.AdaptiveRingPlugin.Models.ActionData;

    /// <summary>
    /// Base class for adaptive ring commands.
    /// </summary>
    public abstract class AdaptiveRingCommandBase : PluginDynamicCommand
    {
        private readonly int _position;
        private AdaptiveRingPlugin _plugin;
        private ActionsRingManager _manager;

        protected AdaptiveRingCommandBase(int position, string name)
            : base(displayName: name, description: $"Adaptive action for position {position + 1}", groupName: "Adaptive Ring")
        {
            _position = position;
        }

        protected override bool OnLoad()
        {
            _plugin = base.Plugin as AdaptiveRingPlugin;
            if (_plugin == null) return false;

            _manager = _plugin.ActionsRingManager;
            if (_manager != null)
            {
                _manager.ActionsUpdated += OnActionsUpdated;
            }
            return true;
        }

        protected override bool OnUnload()
        {
            if (_manager != null)
            {
                _manager.ActionsUpdated -= OnActionsUpdated;
            }
            return true;
        }

        private void OnActionsUpdated(object sender, EventArgs e)
        {
            this.ActionImageChanged();
        }

        protected override String GetCommandDisplayName(String actionParameter, PluginImageSize imageSize)
        {
            // Lazy init
            if (_manager == null && base.Plugin is AdaptiveRingPlugin p)
            {
                _manager = p.ActionsRingManager;
                if (_manager != null) _manager.ActionsUpdated += OnActionsUpdated;
            }

            if (_manager == null) return $"Slot {_position + 1} (Init)";

            var action = _manager.GetAction(_position);
            if (action == null || !action.Enabled)
            {
                return $"Action {_position + 1}";
            }

            var typeIcon = action.Type switch
            {
                ActionType.Keybind => "⌨️",
                ActionType.Prompt => "🤖",
                ActionType.Python => "🐍",
                _ => "❓"
            };

            return $"{typeIcon} {action.ActionName}";
        }

        protected override void RunCommand(String actionParameter)
        {
            if (_manager == null) return;

            var action = _manager.GetAction(_position);
            if (action == null || !action.Enabled) return;

            try
            {
                PluginLog.Info($"Executing action {_position}: {action.ActionName}");
                
                if (action.Type == ActionType.Keybind)
                {
                    KeybindExecutor.Execute(action);
                }
                else if (action.Type == ActionType.Prompt)
                {
                     PluginLog.Info($"[TODO] Execute MCP prompt: {action.ActionDataJson}");
                }
                else if (action.Type == ActionType.Python)
                {
                     PluginLog.Info($"[TODO] Execute Python: {action.ActionDataJson}");
                }
            }
            catch (Exception ex)
            {
                PluginLog.Error($"Error executing action {_position}: {ex.Message}");
            }
        }
    }

    // Concrete implementations for 8 slots
    public class AdaptiveRingCommand1 : AdaptiveRingCommandBase { public AdaptiveRingCommand1() : base(0, "Action 1") { } }
    public class AdaptiveRingCommand2 : AdaptiveRingCommandBase { public AdaptiveRingCommand2() : base(1, "Action 2") { } }
    public class AdaptiveRingCommand3 : AdaptiveRingCommandBase { public AdaptiveRingCommand3() : base(2, "Action 3") { } }
    public class AdaptiveRingCommand4 : AdaptiveRingCommandBase { public AdaptiveRingCommand4() : base(3, "Action 4") { } }
    public class AdaptiveRingCommand5 : AdaptiveRingCommandBase { public AdaptiveRingCommand5() : base(4, "Action 5") { } }
    public class AdaptiveRingCommand6 : AdaptiveRingCommandBase { public AdaptiveRingCommand6() : base(5, "Action 6") { } }
    public class AdaptiveRingCommand7 : AdaptiveRingCommandBase { public AdaptiveRingCommand7() : base(6, "Action 7") { } }
    public class AdaptiveRingCommand8 : AdaptiveRingCommandBase { public AdaptiveRingCommand8() : base(7, "Action 8") { } }
}
