using System.Text.RegularExpressions;

namespace Crawling
{
    public static class StringExt
    {
        public static string[] SplitOnce(this string input, char separator)
        {
            return input.Split(new[] { separator }, 2);
        }

        public static string[] SplitAtLast(this string input, char separator)
        {
            var index = input.LastIndexOf(separator);
            if (index < 0) return new[] { input };

            return new[] { input.Substring(0, index), input.Substring(index + 1) };
        }

        public static string RemoveMultipleWhitespaces(this string input)
        {
            RegexOptions options = RegexOptions.None;
            Regex regex = new Regex("[ ]{2,}", options);
            return regex.Replace(input, " ");
        }
    }
}