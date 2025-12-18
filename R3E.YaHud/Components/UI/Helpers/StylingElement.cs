namespace R3E.YaHud.Components.UI.Helpers
{
    public class StylingElement
    {
        public Dictionary<string, string> styles = new Dictionary<string, string>();
        public classList classlist = new classList();

        public void setStyle(string key, string value)
        {
            styles[key] = value;
        }

        public string getStyle(string key)
        {
            return styles.TryGetValue(key, out var v) ? v : "";
        }

        public void removeStyle(string key)
        {
            styles.Remove(key);
        }

        public void updateStyleString(string key, string value)
        {
            if (styles.ContainsKey(key))
            {
                styles[key] = value;
            }
        }

        public void appendStyleString(string key, string value)
        {
            if (styles.ContainsKey(key))
            {
                if (string.IsNullOrEmpty(styles[key]))
                    styles[key] = value;
                else
                    styles[key] = styles[key] + " " + value;
            }
            else
            {
                styles[key] = value;
            }
        }

        public void removeStyleString(string key, string value)
        {
            if (styles.TryGetValue(key, out var existing) && !string.IsNullOrEmpty(existing))
            {
                var parts = existing.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                                    .Where(p => !string.Equals(p, value, StringComparison.Ordinal))
                                    .ToArray();
                styles[key] = string.Join(" ", parts);
            }
        }

        public override string ToString()
        {

            if (styles.TryGetValue("display", out var displayValue) && !string.IsNullOrEmpty(displayValue))
            {
                return $"display: {displayValue};";
            }


            var sb = new System.Text.StringBuilder();
            var first = true;
            foreach (var kvp in styles)
            {
                if (kvp.Key == "display") continue;
                var value = kvp.Value ?? string.Empty;
                if (!first) sb.Append("; ");
                sb.Append(kvp.Key);
                sb.Append(": ");
                sb.Append(FormatValue(kvp.Key, value));
                first = false;
            }
            return sb.ToString();
        }


        private static string FormatValue(string key, string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return key switch
            {
                "width" or "height" or "top" or "left" or "border-radius" or "opacity" => value.Replace(',', '.'),
                _ => value
            };
        }

        public class classList
        {
            private readonly HashSet<string> classes = new HashSet<string>(StringComparer.Ordinal);

            public void add(string className)
            {
                if (!string.IsNullOrWhiteSpace(className))
                    classes.Add(className.Trim());
            }

            public void remove(string className)
            {
                if (!string.IsNullOrWhiteSpace(className))
                    classes.Remove(className.Trim());
            }

            public override string ToString()
            {
                return string.Join(" ", classes);
            }
        }
    }
}
