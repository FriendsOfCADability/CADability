using System;

namespace CADability
{
    /// <summary>
    /// String utilities: <see cref="RemoveExtraStrings"/> removes repeated occurrences of a substring, used when a typed number
    /// contains the decimal separator more than once. Internal helper, not an entry point of the API.
    /// </summary>
    public class StringHelper
    {
        /// <summary>
        /// Remove duplicate strings from end to start and leave only one occurence.
        /// </summary>
        /// <param name="input">string to manipulate</param>
        /// <param name="stringToRemove">string that will be removed</param>
        /// <returns></returns>
        public static string RemoveExtraStrings(string input, string stringToRemove)
        {
            if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(stringToRemove))
                return input;

            // Count the total number of occurrences of the string to remove
            int count = 0;
            int index = input.IndexOf(stringToRemove, StringComparison.Ordinal);
            while (index != -1)
            {
                count++;
                index = input.IndexOf(stringToRemove, index + stringToRemove.Length, StringComparison.Ordinal);
            }

            // If there are more than one occurrences, start removing from the end
            if (count > 1)
            {
                int removeLength = stringToRemove.Length;
                for (int i = input.Length - removeLength; i >= 0 && count > 1; i--)
                {
                    if (input.Substring(i, removeLength) == stringToRemove)
                    {
                        input = input.Remove(i, removeLength);
                        count--;
                    }
                }
            }

            return input;
        }

    }
}
