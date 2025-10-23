using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace R3E.YaHud
{
    public static class EnumExtensions
    {
        public static string GetDisplayName(this Enum enumValue)
        {
            var members = enumValue.GetType().GetMember(enumValue.ToString());
            
            if (members.Length == 0)
            {
                return enumValue.ToString();
            }

            var displayAttribute = members[0].GetCustomAttribute<DisplayAttribute>();
            return displayAttribute?.Name ?? enumValue.ToString();
        }
    }
}
