namespace R3E.YaHud
{
    public class NameShortener
    {
        public static string ShortenName(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName)) return string.Empty;

            // List of common prefixes to consider
            var prefixes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "van", "von", "der", "de", "del", "la", "le", "du", "mc", "mac" };

            // Split the name into parts
            var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1) return fullName; // If there's only one part, return as is.

            // Get the first name initial
            var firstNameInitial = $"{parts[0][0]}.";

            // Detect prefixes and construct last name
            string lastName = string.Join(" ", parts.Skip(1).Where(part => prefixes.Contains(part) || part != parts.Last()));

            // Add the actual surname to the lastName
            lastName += string.IsNullOrWhiteSpace(lastName) ? parts[^1] : $" {parts[^1]}";

            return $"{firstNameInitial} {lastName}";
        }
    }
}
