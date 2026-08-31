using System;
using System.Text;

namespace Carnassial.Util
{
    public static class StringExtensions
    {
        public static string JoinSkippingNullAndWhiteSpace(string separator, params string?[] values)
        {
            StringBuilder stringBuilder = new();
            for (int index = 0; index < values.Length; ++index)
            {
                string? value = values[index];
                if (String.IsNullOrWhiteSpace(value) == false)
                {
                    stringBuilder.Append($"{(stringBuilder.Length > 0 ? separator : String.Empty)}value");
                }
            }
            return stringBuilder.ToString();
        }
    }
}
