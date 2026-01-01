using Discord.Commands;
using System;
using System.Threading.Tasks;

namespace GeistDesWaldes.Attributes
{
    public class ArrayReader : TypeReader
    {
        private const char joker = '\\';
        public const char DEFAULT_ELEMENT_SEPERATOR = ';';
        private const string dividerPattern = "%?~§";


        public override Task<TypeReaderResult> ReadAsync(ICommandContext context, string input, IServiceProvider services)
        {
            return SplitToArray(input);
        }

        public static Task<TypeReaderResult> SplitToArray(string input, char[] seperator = null, char wildcard = joker)
        {
            return Task.Run(() =>
            {
                try
                {
                    if (seperator == null)
                        seperator = new char[] { DEFAULT_ELEMENT_SEPERATOR };

                    for (int i = 0; i < input.Length; i++)
                    {
                        if (input[i] == wildcard)
                        {
                            i++; // Skip next char
                            continue;
                        }

                        bool containsChar = false;
                        for (int j = 0; j < seperator.Length; j++)
                        {
                            if (seperator[j] == input[i])
                            {
                                containsChar = true;
                                break;
                            }
                        }

                        if (containsChar)
                        {
                            input = input.Insert(i, dividerPattern);
                            i += dividerPattern.Length; // Skip inserted divider
                        }
                    }

                    string[] seperatorArray = new string[seperator.Length];
                    for (int i = 0; i < seperator.Length; i++)
                        seperatorArray[i] = $"{dividerPattern}{seperator[i]}";


                    string[] splitParams = input.Split(seperatorArray, StringSplitOptions.RemoveEmptyEntries);


                    return TypeReaderResult.FromSuccess(splitParams);
                }
                catch (Exception e)
                {
                    return TypeReaderResult.FromError(e);
                }
            });
        }
    }
}
