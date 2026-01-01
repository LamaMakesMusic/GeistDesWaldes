
namespace GeistDesWaldes.Misc
{
    public static class Extensions
    {

        public static string AsMarkdown(this string s, MarkdownOption style)
        {
            return style switch
            {
                MarkdownOption.H1 => $" # {s} ",
                MarkdownOption.H2 => $" ## {s} ",
                MarkdownOption.H3 => $" ### {s} ",
                MarkdownOption.Italic => $" _{s}_ ",
                MarkdownOption.Bold => $" **{s}** ",
                MarkdownOption.ListUnsorted => $" * {s} ",
                MarkdownOption.ListSorted => $" 1. {s} ",
                MarkdownOption.Seperator => " *** ",
                _ => s
            };
        }        
    }

    public enum MarkdownOption
    {
        None = 0,
        H1 = 1,
        H2 = 2,
        H3 = 3,
        Italic = 4,
        Bold = 5,
        ListUnsorted = 6,
        ListSorted = 7,
        Seperator = 8
    }
}