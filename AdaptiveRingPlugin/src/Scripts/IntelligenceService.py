import os
import sys
import json
import argparse
import google.generativeai as genai
from typing import List, Dict, Any

def setup_logging():
    pass

def log(message):
    sys.stderr.write(f"[IntelligenceService] {message}\n")

def get_universal_defaults(app_name: str) -> List[Dict[str, Any]]:
    log(f"Generating universal defaults for {app_name}")
    defaults = [
        {"position": 0, "type": "Keybind", "actionName": "Copy", "actionData": {"keys": ["Ctrl", "C"], "description": "Copy selected text"}},
        {"position": 1, "type": "Keybind", "actionName": "Paste", "actionData": {"keys": ["Ctrl", "V"], "description": "Paste from clipboard"}},
        {"position": 2, "type": "Keybind", "actionName": "Save", "actionData": {"keys": ["Ctrl", "S"], "description": "Save current file"}},
        {"position": 3, "type": "Keybind", "actionName": "Undo", "actionData": {"keys": ["Ctrl", "Z"], "description": "Undo last action"}},
        {"position": 4, "type": "Keybind", "actionName": "Find", "actionData": {"keys": ["Ctrl", "F"], "description": "Find text"}},
        {"position": 5, "type": "Keybind", "actionName": "Select All", "actionData": {"keys": ["Ctrl", "A"], "description": "Select all"}},
        {"position": 6, "type": "Keybind", "actionName": "New Tab", "actionData": {"keys": ["Ctrl", "T"], "description": "New tab"}},
        {"position": 7, "type": "Keybind", "actionName": "Close", "actionData": {"keys": ["Ctrl", "W"], "description": "Smart close"}}
    ]
    return defaults

def suggest_actions(app_name: str, mcp_servers_json: str, api_key: str):
    try:
        mcp_servers = json.loads(mcp_servers_json)
    except json.JSONDecodeError:
        log("Failed to parse MCP servers JSON. Assuming empty.")
        mcp_servers = []

    genai.configure(api_key=api_key)
    model = genai.GenerativeModel('gemini-2.5-flash')

    prompt = f"""You are an expert at creating productivity workflows for {app_name}.

Generate exactly 8 actions for the Actions Ring (positions 0-7).

Priority order for action types:
1. Keybind (fastest execution) - Use for common shortcuts
2. Prompt (when MCP tools are available) - Use for AI-assisted tasks
3. Python (for complex automation) - Use for advanced scripting

Action type mixing guidelines:
- Aim for 60% Keybind, 30% Prompt (if MCP available), 10% Python
- If no MCP tools are available, use 100% Keybind actions

"""

    if mcp_servers:
        prompt += "Available MCP tools:\n"
        for server in mcp_servers:
            name = server.get('ServerName', 'Unknown')
            package = server.get('PackageName', '')
            prompt += f"  - Server: {name} ({package})\n"
            tools = server.get('Tools', {})
            if tools:
                for key, tool_info in list(tools.items())[:10]:
                    desc = tool_info.get('Description', 'No description')
                    prompt += f"    * {key}: {desc}\n"
        prompt += "\n"
    else:
        prompt += "Note: No MCP tools are available for this app. Use only Keybind actions.\n\n"

    prompt += f"""Return ONLY a valid JSON array with exactly 8 objects in this exact format:
[
  {{
    "position": 0,
    "type": "Keybind",
    "actionName": "Copy",
    "actionData": {{
      "keys": ["Ctrl", "C"],
      "description": "Copy selected text"
    }}
  }},
  {{
    "position": 1,
    "type": "Prompt",
    "actionName": "Analyze Code",
    "actionData": {{
      "mcpServerName": "vscode",
      "toolName": "analyze",
      "parameters": {{}},
      "description": "Analyze selected code"
    }}
  }}
]

Important rules:
- Return ONLY the raw JSON array. Do not wrap in markdown blocks (like ```json).
- Each action must have position (0-7), type (Keybind/Prompt/Python), actionName, and actionData
- For Keybind: actionData must have "keys" array and optional "description"
- For Prompt: actionData must have "mcpServerName", "toolName", optional "parameters" dict, and optional "description"
- For Python: actionData must have "scriptCode" or "scriptPath", optional "arguments" array, and optional "description"
- Make actions relevant and useful for {app_name}
- Prioritize common workflows and frequent tasks

Generate 8 optimal actions now:"""

    try:
        log(f"Requesting suggestions for {app_name}...")
        response = model.generate_content(prompt)
        text = response.text
        
        text = text.strip()
        if text.startswith("```json"):
            text = text[7:]
        elif text.startswith("```"):
            text = text[3:]
        if text.endswith("```"):
            text = text[:-3]
        text = text.strip()
        
        actions = json.loads(text)
        
        if len(actions) != 8:
            log(f"Received {len(actions)} actions, expected 8. Returning defaults.")
            print(json.dumps(get_universal_defaults(app_name)))
        else:
            log("Successfully generated actions.")
            print(json.dumps(actions))
            
    except Exception as e:
        log(f"Error calling Gemini: {str(e)}")
        print(json.dumps(get_universal_defaults(app_name)))

def orchestrate(tools_json: str, prompt: str, api_key: str):
    genai.configure(api_key=api_key)
    model = genai.GenerativeModel('gemini-2.5-flash')

    try:
        tools = json.loads(tools_json)
    except Exception as e:
        log(f"Error parsing tools JSON: {e}")
        print(json.dumps({"tool": "none", "error": "Invalid tools JSON"}))
        return

    tools_desc = "\n".join([f"- {t.get('Name', 'Unknown')}: {t.get('Description', 'No description')}" for t in tools])
    
    full_prompt = f"""You have access to the following tools:

{tools_desc}

User request: {prompt}

Which tool should be called and with what arguments? Respond in JSON format:
{{
  "tool": "tool_name",
  "arguments": {{}}
}}

If no tool is appropriate, respond with: {{"tool": "none"}}"""

    try:
        log(f"Orchestrating request: {prompt}")
        response = model.generate_content(full_prompt)
        text = response.text.strip()
        
        if text.startswith("```json"): text = text[7:]
        elif text.startswith("```"): text = text[3:]
        if text.endswith("```"): text = text[:-3]
        text = text.strip()
        
        print(text)
    except Exception as e:
        log(f"Error in orchestration: {e}")
        print(json.dumps({"tool": "none", "error": str(e)}))

def main():
    parser = argparse.ArgumentParser(description='AdaptiveRing Intelligence Service')
    parser.add_argument('--mode', choices=['suggest', 'orchestrate'], default='suggest', help='Operation mode')
    
    # Suggest mode args
    parser.add_argument('--app', help='Application name')
    parser.add_argument('--mcp-servers', help='JSON string of available MCP servers', default='[]')
    
    # Orchestrate mode args
    parser.add_argument('--tools', help='JSON string of available tools')
    parser.add_argument('--prompt', help='User prompt for orchestration')
    
    args = parser.parse_args()
    api_key = os.environ.get("GEMINI_API_KEY")
    
    if not api_key:
        log("GEMINI_API_KEY not set.")
        if args.mode == 'suggest':
            if args.app:
                print(json.dumps(get_universal_defaults(args.app)))
            else:
                 print(json.dumps(get_universal_defaults("Default")))
        else:
            print(json.dumps({"tool": "none", "error": "API key missing"}))
        return

    if args.mode == 'suggest':
        if not args.app:
            log("Error: --app required for suggest mode")
            return
        suggest_actions(args.app, args.mcp_servers, api_key)
        
    elif args.mode == 'orchestrate':
        if not args.tools or not args.prompt:
            log("Error: --tools and --prompt required for orchestrate mode")
            print(json.dumps({"tool": "none"}))
            return
        orchestrate(args.tools, args.prompt, api_key)

if __name__ == "__main__":
    main()
