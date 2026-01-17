using System;
using System.Text;
using Discord;
using GeistDesWaldes.Dictionaries;

namespace GeistDesWaldes.Communication;

public class ChannelMessageContent
{
    public enum DescriptionStyleOption
    {
        Default = 0,
        CodeBlock = 1
    }

    public (string text, DescriptionStyleOption style) Description;
    public (string text, Emoji emoji) Title;


    public ChannelMessageContent SetTitle(string title, string emoji = null)
    {
        if (title != null)
        {
            Title.text = title;
        }

        if (emoji != null)
        {
            Title.emoji = EmojiDictionary.GetEmoji(emoji);
        }

        return this;
    }

    public ChannelMessageContent SetDescription(string description, int style = -1)
    {
        if (description != null)
        {
            Description.text = description;
        }

        if (style != -1)
        {
            Description.style = (DescriptionStyleOption)style;
        }

        return this;
    }

    public EmbedBuilder AddToBuilder(EmbedBuilder builder)
    {
        string combinedTitle = $"{Title.emoji}   {Title.text}";
        bool hasTitle = !string.IsNullOrWhiteSpace(combinedTitle);
        bool hasDescr = !string.IsNullOrWhiteSpace(Description.text);

        if (hasTitle && !hasDescr)
        {
            Description.text = "-";
            hasDescr = true;
        }
        else if (!hasTitle && hasDescr)
        {
            if (builder.Fields.Count > 0 && !string.IsNullOrWhiteSpace(builder.Fields[^1].Name))
            {
                string existing = (string)builder.Fields[^1].Value;

                if (existing == "-")
                {
                    existing = string.Empty;
                }
                else
                {
                    existing = $"{existing}\n";
                }


                string descriptionFormatted = Description.text;

                switch (Description.style)
                {
                    default:
                    case DescriptionStyleOption.Default:
                        break;

                    case DescriptionStyleOption.CodeBlock:
                        descriptionFormatted = $"``` {descriptionFormatted} ```";
                        break;
                }

                // Merge with existing preceeding entry
                builder.Fields[^1].Value = $"{existing}{descriptionFormatted}";

                return builder;
            }

            combinedTitle = "-";
            hasTitle = true;
        }


        if (hasTitle && hasDescr)
        {
            int bodyCount = (int)Math.Ceiling(Description.text.Length / (double)1024);

            for (int i = 0; i < bodyCount; i++)
            {
                string msgBody = Description.text.Substring(i * 1024, Math.Min(1024, Description.text.Length - i * 1024));
                builder.AddField(new EmbedFieldBuilder().WithName($"{(bodyCount > 1 ? $"[{i + 1}/{bodyCount}] " : "")}{combinedTitle}").WithValue(msgBody));
            }
        }

        return builder;
    }

    public override string ToString()
    {
        StringBuilder str = new();


        if (Title.emoji != null || !string.IsNullOrWhiteSpace(Title.text))
        {
            str.Append($"<{(Title.emoji != null ? $"{Title.emoji} " : "")}{Title.text}> ");
        }

        if (!string.IsNullOrWhiteSpace(Description.text))
        {
            str.Append(Description.text);
        }


        return str.ToString();
    }
}