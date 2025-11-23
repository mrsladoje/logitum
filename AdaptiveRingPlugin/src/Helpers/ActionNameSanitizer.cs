namespace Loupedeck.AdaptiveRingPlugin.Helpers
{
    using System;
    using System.Text;

    /// <summary>
    /// Utility class to sanitize action names and descriptions by removing emojis and special Unicode symbols
    /// </summary>
    public static class ActionNameSanitizer
    {
        /// <summary>
        /// Removes all emojis and special Unicode symbols from action names and descriptions.
        /// Keeps only letters, numbers, spaces, and basic punctuation.
        /// </summary>
        public static string Sanitize(string? input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;

            // Remove emojis and special Unicode characters, keeping only:
            // - Letters (A-Z, a-z, and Unicode letters)
            // - Numbers (0-9)
            // - Spaces
            // - Basic punctuation: . , - _ ( ) [ ] : ; ! ?
            var result = new StringBuilder();
            foreach (char c in input)
            {
                if (char.IsLetterOrDigit(c) || 
                    char.IsWhiteSpace(c) || 
                    c == '.' || c == ',' || c == '-' || c == '_' || 
                    c == '(' || c == ')' || c == '[' || c == ']' || 
                    c == ':' || c == ';' || c == '!' || c == '?')
                {
                    result.Append(c);
                }
                // Skip all other characters (emojis, symbols, etc.)
            }
            return result.ToString().Trim();
        }
    }
}

