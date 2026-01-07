namespace R3E.YaHud.Components.UI.Helpers
{
    /// <summary>
    /// Helper to build inline style and class attributes for Blazor components/widgets.
    /// Use <see cref="SetStyle"/>, <see cref="AppendStyleString"/>, and <see cref="RemoveStyle"/>
    /// to manage individual CSS declarations. Use the nested <see cref="ClassList"/> to manage
    /// space-separated CSS classes.
    /// 
    /// This class is intentionally lightweight and allocates simple collections; callers should
    /// reuse instances where appropriate to avoid unnecessary allocations in tight update loops.
    /// </summary>
    public class StylingElement
    {
        /// <summary>
        /// Dictionary of CSS property -> value pairs. Values should be preformatted (use invariant
        /// culture for numbers) when possible so <see cref="ToString"/> can emit them directly.
        /// </summary>
        public Dictionary<string, string> Styles { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Helper to maintain a deduplicated set of CSS classes for the element.
        /// </summary>
        public ClassList Classes { get; set; } = new ClassList();

        /// <summary>
        /// Clear all styles and classes and reset to an empty state.
        /// </summary>
        public void Clear()
        {
            Styles = new Dictionary<string, string>();
            Classes = new ClassList();
        }

        /// <summary>
        /// Set or replace a CSS property value.
        /// </summary>
        /// <param name="key">CSS property name (e.g. "width", "transform").</param>
        /// <param name="value">Property value (e.g. "10px", "translate3d(10px,0,0)"). Use invariant formatting for numbers.</param>
        public void SetStyle(string key, string value)
        {
            Styles[key] = value;
        }

        /// <summary>
        /// Get a style value or empty string if not present.
        /// </summary>
        /// <param name="key">CSS property name.</param>
        /// <returns>The value for the property or <c>""</c>.</returns>
        public string GetStyle(string key)
        {
            return Styles.TryGetValue(key, out var v) ? v : "";
        }

        /// <summary>
        /// Remove a style entry if present.
        /// </summary>
        /// <param name="key">CSS property name to remove.</param>
        public void RemoveStyle(string key)
        {
            Styles.Remove(key);
        }

        /// <summary>
        /// Update an existing style entry only if it already exists.
        /// </summary>
        /// <param name="key">CSS property name.</param>
        /// <param name="value">New value.</param>
        public void UpdateStyleString(string key, string value)
        {
            if (Styles.ContainsKey(key))
            {
                Styles[key] = value;
            }
        }

        /// <summary>
        /// Append a whitespace-separated token to an existing style value or create the entry.
        /// Useful for properties like <c>background-image</c> that may contain multiple comma/space separated items.
        /// </summary>
        /// <param name="key">CSS property name.</param>
        /// <param name="value">Token to append.</param>
        public void AppendStyleString(string key, string value)
        {
            if (Styles.TryGetValue(key, out var existing))
            {
                Styles[key] = string.IsNullOrEmpty(existing) ? value : existing + " " + value;
            }
            else
            {
                Styles[key] = value;
            }
        }

        public void AppendStyleStringIfNotExist(string key, string value)
        {
            if (Styles.TryGetValue(key, out var existing))
            {
                if (!string.IsNullOrEmpty(existing) && !existing.Contains(value))
                    Styles[key] = string.IsNullOrEmpty(existing) ? value : existing + " " + value;
            }
            else
            {
                Styles[key] = value;
            }
        }

        /// <summary>
        /// Remove a whitespace-separated token from an existing style entry.
        /// The operation is safe and will only remove exact token matches.
        /// </summary>
        /// <param name="key">CSS property name.</param>
        /// <param name="value">Token to remove.</param>
        public void RemoveStyleString(string key, string value)
        {
            if (Styles.TryGetValue(key, out var existing) && !string.IsNullOrEmpty(existing))
            {
                var parts = existing.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                                    .Where(p => !string.Equals(p, value, StringComparison.Ordinal))
                                    .ToArray();
                Styles[key] = string.Join(" ", parts);
            }
        }

        /// <summary>
        /// Build the inline style string (e.g. <c>"left: 10px; top: 20px; width: 40px"</c>).
        /// Special-case: if a <c>display</c> style is present it returns only that declaration
        /// (preserves the previous behavior where display overrides other style output).
        /// </summary>
        /// <returns>CSS inline style string (no surrounding attribute).</returns>
        public override string ToString()
        {

            if (Styles.TryGetValue("display", out var displayValue) && !string.IsNullOrEmpty(displayValue))
            {
                return $"display: {displayValue};";
            }


            var sb = new System.Text.StringBuilder();
            var first = true;
            foreach (var kvp in Styles.Where(kvp => !string.Equals(kvp.Key, "display", StringComparison.Ordinal)))
            {
                var value = kvp.Value ?? string.Empty;
                if (!first) sb.Append("; ");
                sb.Append(kvp.Key);
                sb.Append(": ");
                sb.Append(FormatValue(kvp.Key, value));
                first = false;
            }
            return sb.ToString();
        }

        /// <summary>
        /// Lightweight formatting normalization for common numeric-like properties.
        /// Callers are encouraged to format numeric values with <see cref="System.Globalization.CultureInfo.InvariantCulture"/>
        /// before calling <see cref="SetStyle"/> to avoid allocation and string replacements.
        /// </summary>
        /// <remarks>
        /// Currently replaces comma with dot for a small set of keys to ensure CSS uses '.' as decimal separator.
        /// </remarks>
        private static string FormatValue(string key, string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return key switch
            {
                "width" or "height" or "top" or "left" or "border-radius" or "opacity" => value.Replace(',', '.'),
                _ => value
            };
        }

        /// <summary>
        /// Minimal, deduplicating class list helper.
        /// Methods are intentionally named in lower-case to match prior usage in codebase:
        /// call <see cref="Add(string)"/> and <see cref="Remove(string)"/> from widgets.
        /// </summary>
        public class ClassList
        {
            private readonly HashSet<string> classes = new HashSet<string>(StringComparer.Ordinal);

            /// <summary>
            /// Add a class name (trimmed). Duplicate names are ignored.
            /// </summary>
            /// <param name="className">Class name to add.</param>
            public void Add(string className)
            {
                if (!string.IsNullOrWhiteSpace(className))
                    classes.Add(className.Trim());
            }

            /// <summary>
            /// Remove a class name if present.
            /// </summary>
            /// <param name="className">Class name to remove.</param>
            public void Remove(string className)
            {
                if (!string.IsNullOrWhiteSpace(className))
                    classes.Remove(className.Trim());
            }

            /// <summary>
            /// Returns the space-separated class string suitable for an element's class attribute.
            /// </summary>
            public override string ToString()
            {
                return string.Join(" ", classes);
            }
        }
    }
}
