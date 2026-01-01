using Discord;
using Discord.Commands;
using GeistDesWaldes.Attributes;
using GeistDesWaldes.Dictionaries;
using GeistDesWaldes.Misc;
using GeistDesWaldes.TwitchIntegration;
using GeistDesWaldes.UserCommands;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace GeistDesWaldes.Communication
{
    public class ChannelMessage
    {
        public IMessageChannel Channel;

        public IUser Author;
        public Color? EmbedColor;
        public EmbedFooterBuilder Footer;
        public string URL;
        public string ImageURL;
        public bool CurrentTimeStamp = true;
        public bool TTS;
        public MessageReference MessageReference = null;

        public List<ChannelMessageContent> Contents;

        private MessageTemplateOption? _setTemplate;
        private CommandBundleEntry _bundleCallback;


        public ChannelMessage(ICommandContext context, bool tts = false)
        {
            Channel = context?.Channel;
            Contents = new List<ChannelMessageContent>();
            TTS = tts;

            if (context?.Message is MetaCommandMessage meta)
                _bundleCallback = meta.BundleCallback;
        }

        public async Task<RuntimeResult> SendAsync(bool forcePlainText = false)
        {
            try
            {
                if (_bundleCallback != null)
                {
                    _bundleCallback.SetMessage(this);
                    _bundleCallback = null;

                    return CustomRuntimeResult.FromSuccess();
                }
                else
                {
                    if (Channel == null)
                        throw new Exception($"{nameof(Channel)} == null! Make sure to set the channel before calling {nameof(ChannelMessage)}.{nameof(SendAsync)}()!");

                    EmbedBuilder msgBuilder = BuildMessage();

                    forcePlainText = forcePlainText || Channel is TwitchMessageChannel || Channel is ConsoleMessageChannel;

                    if (forcePlainText)
                    {
                        StringBuilder str = new StringBuilder();

                        if (!string.IsNullOrWhiteSpace(msgBuilder.Title) && msgBuilder.Title != "-")
                            str.AppendLine($">{msgBuilder.Title.Trim()}<");
                        if (!string.IsNullOrWhiteSpace(msgBuilder.Description) && msgBuilder.Description != "-")
                            str.AppendLine(msgBuilder.Description);

                        foreach (var field in msgBuilder.Fields)
                        {
                            str.AppendLine($">{field.Name.Trim()}<");
                            str.AppendLine((string)field.Value);
                        }
                        
                        if (!string.IsNullOrWhiteSpace(ImageURL))
                            str.AppendLine($"[ {ImageURL} ]");

                        if (Footer != null && !string.IsNullOrWhiteSpace(Footer.Text))
                            str.AppendLine($"({Footer.Text})");


                        await Channel.SendMessageAsync(text: str.ToString(), isTTS: TTS, messageReference: MessageReference);
                    }
                    else
                    {
                        await Channel.SendMessageAsync(embed: msgBuilder.Build(), isTTS: TTS, messageReference: MessageReference);
                    }

                    return CustomRuntimeResult.FromSuccess($"Sending Message to '{Channel?.Name}'...");
                }
            }
            catch (Exception e)
            {
                return CustomRuntimeResult.FromError(e.ToString());
            }
        }

        public EmbedBuilder BuildMessage()
        {
            ApplyTemplate();


            EmbedBuilder builder = new EmbedBuilder();

            if (Author != null)
                builder.WithAuthor(Author);

            if (EmbedColor.HasValue)
                builder.WithColor(EmbedColor.Value);

            if (Footer != null)
                builder.WithFooter(Footer);

            if (!string.IsNullOrWhiteSpace(URL))
                builder.WithUrl(URL);

            if (!string.IsNullOrWhiteSpace(ImageURL))
                builder.WithImageUrl(ImageURL);

            if (CurrentTimeStamp)
                builder.WithCurrentTimestamp();


            for (int i = 0; i < Contents?.Count; i++)
                builder = Contents[i].AddToBuilder(builder);


            // If no main title/description is set, use first field
            if (builder.Fields.Count > 0)
            {
                bool hasTitle = !string.IsNullOrWhiteSpace(builder.Title);
                bool hasDescr = !string.IsNullOrWhiteSpace(builder.Description);

                if (!hasTitle && !hasDescr)
                {
                    builder.WithTitle(builder.Fields[0].Name);
                    builder.WithDescription((string)builder.Fields[0].Value);

                    builder.Fields.RemoveAt(0);
                }
            }


            return builder;
        }


        public ChannelMessage AppendContent(ChannelMessage msg, MergeOption mergeTitle = MergeOption.Fill, MergeOption mergeURL = MergeOption.Overwrite, MergeOption mergeImage = MergeOption.Fill, MergeOption mergeFooter = MergeOption.Append)
        {
            msg.ApplyTemplate();

            if (URL != msg.URL)
            {
                switch (mergeURL)
                {
                    default:
                    case MergeOption.Fill:
                        if (string.IsNullOrWhiteSpace(URL) && !string.IsNullOrWhiteSpace(msg.URL))
                            URL = msg.URL;
                        break;

                    case MergeOption.Append:
                        URL = $"{URL}{(!string.IsNullOrWhiteSpace(URL) && !string.IsNullOrWhiteSpace(msg.URL) ? " | " : "")}{msg.URL}";
                        break;

                    case MergeOption.Overwrite:
                        if (!string.IsNullOrWhiteSpace(msg.URL))
                            URL = msg.URL;
                        break;
                }
            }

            if (ImageURL != msg.ImageURL)
            {
                switch (mergeImage)
                {
                    default:
                    case MergeOption.Fill:
                        if (string.IsNullOrWhiteSpace(ImageURL) && !string.IsNullOrWhiteSpace(msg.ImageURL))
                            ImageURL = msg.ImageURL;
                        break;

                    case MergeOption.Append:                    
                        ImageURL = $"{ImageURL}{(!string.IsNullOrWhiteSpace(ImageURL) && !string.IsNullOrWhiteSpace(msg.ImageURL) ? " | " : "")}{msg.ImageURL}";
                        break;

                    case MergeOption.Overwrite:
                        if (!string.IsNullOrWhiteSpace(msg.ImageURL))
                            ImageURL = msg.ImageURL;
                        break;
                }
            }

            if (Footer?.Text != msg.Footer?.Text)
            {
                switch (mergeFooter)
                {
                    case MergeOption.Fill:
                        if (Footer == null && msg.Footer != null)
                            Footer = msg.Footer;
                        break;

                    default:
                    case MergeOption.Append:
                        EmbedFooterBuilder newFoot = new EmbedFooterBuilder();

                        bool splitSign = Footer != null && msg.Footer != null && !string.IsNullOrWhiteSpace(Footer.Text) && !string.IsNullOrWhiteSpace(msg.Footer.Text);
                        newFoot.Text = $"{Footer?.Text}{(splitSign ? " | " : "")}{msg.Footer?.Text}";

                        if (!string.IsNullOrWhiteSpace(msg.Footer?.IconUrl))
                            newFoot.IconUrl = msg.Footer.IconUrl;

                        Footer = newFoot;
                        break;

                    case MergeOption.Overwrite:
                        if (msg.Footer != null)
                            Footer = msg.Footer;
                        break;
                }
            }

            Contents.AddRange(msg.Contents);

            return this;
        }
        public ChannelMessage AddContent(ChannelMessageContent content)
        {
            Contents.Add(content);
            return this;
        }
        public ChannelMessage SetChannel(IMessageChannel channel)
        {
            Channel = channel;
            return this;
        }
        public ChannelMessage SetFooter(string footer, string iconUrl = null)
        {
            Footer = new EmbedFooterBuilder()
            {
                Text = footer,
                IconUrl = iconUrl
            };

            return this;
        }
        public ChannelMessage SetURL(string url)
        {
            URL = url;
            return this;
        }
        public ChannelMessage SetImageURL(string imageURL)
        {
            ImageURL = imageURL;
            return this;
        }
        public ChannelMessage SetColor(Color embedColor)
        {
            EmbedColor = embedColor;
            return this;
        }
        public ChannelMessage SetTemplate(MessageTemplateOption template)
        {
            _setTemplate = template;
            return this;
        }


        public void ApplyTemplate()
        {
            if (_setTemplate.HasValue == false)
                return;

            (string text, string emoji) templateTitle = (null, null);
            Color? templateColour;

            switch (_setTemplate.Value)
            {
                case MessageTemplateOption.Error:
                case MessageTemplateOption.Negative:
                    templateColour = Color.Red;
                    templateTitle = (ReplyDictionary.NEGATIVE, EmojiDictionary.CROSS_MARK);
                    break;

                case MessageTemplateOption.Positive:
                    templateColour = Color.Green;
                    templateTitle = (ReplyDictionary.AFFIRMATIVE, EmojiDictionary.CHECK_MARK);
                    break;

                case MessageTemplateOption.Information:
                    templateColour = Color.Blue;
                    templateTitle = (ReplyDictionary.INFORMATION, EmojiDictionary.INFO);
                    break;

                case MessageTemplateOption.Counter:
                    templateColour = Color.DarkTeal;
                    templateTitle = (ReplyDictionary.COUNTER, EmojiDictionary.NUMBERS);
                    break;

                case MessageTemplateOption.Events:
                    templateColour = Color.Teal;
                    templateTitle = (ReplyDictionary.EVENTS, EmojiDictionary.PAGER);
                    break;

                case MessageTemplateOption.Calendar:
                    templateColour = Color.Magenta;
                    templateTitle = (ReplyDictionary.CALENDAR, EmojiDictionary.DATE);
                    break;

                case MessageTemplateOption.Citations:
                    templateColour = Color.DarkMagenta;
                    templateTitle = (ReplyDictionary.CITATIONS, EmojiDictionary.SPEECH_BALLOON);
                    break;

                case MessageTemplateOption.Points:
                    templateColour = Color.Gold;
                    templateTitle = (ReplyDictionary.POINTS, EmojiDictionary.MONEY_BAG);
                    break;

                case MessageTemplateOption.Templates:
                    templateColour = Color.LightOrange;
                    templateTitle = (ReplyDictionary.TEMPLATES, EmojiDictionary.BOOKMARK_TABS);
                    break;

                case MessageTemplateOption.Polls:
                    templateColour = Color.DarkPurple;
                    templateTitle = (ReplyDictionary.POLLS, EmojiDictionary.SCROLL);
                    break;

                case MessageTemplateOption.Twitch:
                    templateColour = Color.Purple;
                    templateTitle = (ReplyDictionary.TWITCH, EmojiDictionary.INFO);
                    break;

                case MessageTemplateOption.Modified:
                    templateColour = Color.LightOrange;
                    templateTitle = (ReplyDictionary.AFFIRMATIVE, EmojiDictionary.PENCIL);
                    break;

                case MessageTemplateOption.Birthday:
                    templateColour = Color.Gold;
                    templateTitle = (ReplyDictionary.BIRTHDAY, EmojiDictionary.BIRTHDAY_CAKE);
                    break;

                case MessageTemplateOption.Audio:
                    templateColour = Color.DarkBlue;
                    templateTitle = (ReplyDictionary.AUDIO, EmojiDictionary.NOTES);
                    break;

                case MessageTemplateOption.Statistics:
                    templateColour = Color.LightOrange;
                    templateTitle = (ReplyDictionary.STATISTICS, EmojiDictionary.CHART);
                    break;

                default:
                case MessageTemplateOption.Neutral:
                    templateColour = Color.LightGrey;
                    templateTitle = (string.Empty, EmojiDictionary.GHOST);
                    break;
            }


            if (templateColour.HasValue)
                EmbedColor = templateColour;

            if (Contents.Count > 0)
            {
                if (Contents[0].Title.text == null)
                    Contents[0].Title.text = templateTitle.text;

                if (Contents[0].Title.emoji == null)
                    Contents[0].Title.emoji = EmojiDictionary.GetEmoji(templateTitle.emoji);
            }
        }
        public enum MessageTemplateOption
        {
            Neutral = 0,
            Error = 1,
            Negative = 2,
            Positive = 3,
            Information = 4,
            Counter = 5,
            Events = 6,
            Calendar = 7,
            Citations = 8,
            Points = 9,
            Templates = 10,
            Polls = 11,
            Twitch = 12,
            Modified = 13,
            Birthday = 14,
            Audio = 15,
            Statistics = 16
        }

        public enum MergeOption
        {
            Fill = 0,
            Append = 1,
            Overwrite = 2
        }
    }
}
