using System;
using System.Globalization;

namespace GeistDesWaldes.Dictionaries;

public static class ConstantsDictionary
{
    private const string TIME_TAG_OPEN = "{$TIME[";
    private const string TIME_TAG_CLOSE = "]}";


    public static string InjectConstants(string input, CultureInfo culture)
    {
        bool successful;

        do
        {
            successful = TryInjectTime(ref input, culture);
        } while (successful);

        return input;
    }

    private static bool TryInjectTime(ref string input, CultureInfo culture)
    {
        int startIdx = input.IndexOf(TIME_TAG_OPEN, StringComparison.Ordinal);

        if (startIdx == -1)
        {
            return false;
        }

        int endIdx = input.IndexOf(TIME_TAG_CLOSE, startIdx,  StringComparison.Ordinal);

        if (endIdx == -1)
        {
            return false;
        }

        int formatStart = startIdx + TIME_TAG_OPEN.Length;
        int formatLength = endIdx - formatStart;

        string format = input.Substring(formatStart, formatLength);

        input = input.Remove(startIdx, endIdx + TIME_TAG_CLOSE.Length - startIdx);
        input = input.Insert(startIdx, DateTime.Now.ToString(format, culture));

        return true;
    }
}