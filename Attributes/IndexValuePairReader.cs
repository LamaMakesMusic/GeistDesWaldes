using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord.Commands;
using GeistDesWaldes.Misc;

namespace GeistDesWaldes.Attributes;

public class IndexValuePairReader : TypeReader
{
    // Expected Input e.g.: "1|a"
    private const char DEFAULT_SEPERATOR = '|';

    public override async Task<TypeReaderResult> ReadAsync(ICommandContext context, string input, IServiceProvider services)
    {
        return await StringToIndexValuePair(input);
    }

    public static async Task<TypeReaderResult> StringToIndexValuePair(string input)
    {
        TypeReaderResult splitStringResult = await ArrayReader.SplitToArray(input, new[] { DEFAULT_SEPERATOR });
        if (splitStringResult.IsSuccess)
        {
            string errorReason;
            string[] bestMatch = (string[])splitStringResult.BestMatch;


            if (bestMatch == null)
            {
                errorReason = "Best Match == NULL";
            }
            else if (bestMatch.Length > 2)
            {
                errorReason = $"String Split resulted in more than two results! -> Count: {bestMatch.Length}";
            }
            else
            {
                if (bestMatch.Length == 0)
                {
                    return TypeReaderResult.FromSuccess(null);
                }

                if (int.TryParse(bestMatch[0], out int index))
                {
                    return TypeReaderResult.FromSuccess(new IndexValuePair(index, bestMatch[1]));
                }

                errorReason = $"Could not parse to {typeof(int)} from '{bestMatch[0]}'!";
            }

            return TypeReaderResult.FromError(CommandError.ParseFailed, errorReason);
        }

        return splitStringResult;
    }
}

public class IndexValuePairArrayReader : TypeReader
{
    public override async Task<TypeReaderResult> ReadAsync(ICommandContext context, string input, IServiceProvider services)
    {
        TypeReaderResult inputToArrayResult = await ArrayReader.SplitToArray(input);
        if (inputToArrayResult.IsSuccess)
        {
            string[] stringArray = (string[])inputToArrayResult.BestMatch;

            string errorReason;

            if (stringArray == null)
            {
                errorReason = "String Array: Best Match == NULL";
            }
            else
            {
                var pairs = new List<IndexValuePair>();

                foreach (string line in stringArray)
                {
                    TypeReaderResult lineResult = await IndexValuePairReader.StringToIndexValuePair(line);

                    if (!lineResult.IsSuccess)
                    {
                        return lineResult;
                    }

                    if (lineResult.BestMatch is IndexValuePair readPair && readPair != null)
                    {
                        pairs.Add(readPair);
                    }
                }

                return TypeReaderResult.FromSuccess(pairs.ToArray());
            }

            return TypeReaderResult.FromError(CommandError.ParseFailed, errorReason);
        }

        return inputToArrayResult;
    }
}