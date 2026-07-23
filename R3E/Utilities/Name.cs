namespace R3E.Utilities
{
    public static class Name
    {
        // Common name-prefix tokens (e.g. "van der Berg", "de la Cruz") that belong with the surname.
        private static readonly HashSet<string> Prefixes = new(StringComparer.OrdinalIgnoreCase)
        {
            "van", "von", "der", "de", "del", "la", "le", "du", "mc", "mac"
        };

        public static string ShortenDriverName(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName)) return string.Empty;

            // Split the name into parts
            var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1) return fullName; // If there's only one part, return as is.

            // Get the first name initial
            var firstNameInitial = $"{parts[0][0]}.";

            // Walk backward from the surname, absorbing any contiguous prefix tokens
            // immediately preceding it (e.g. "van der Berg"). Any other middle names are dropped.
            var surnameStart = parts.Length - 1;
            while (surnameStart > 1 && Prefixes.Contains(parts[surnameStart - 1]))
            {
                surnameStart--;
            }

            var lastName = string.Join(" ", parts.Skip(surnameStart));

            return $"{firstNameInitial} {lastName}";
        }
    }
}
