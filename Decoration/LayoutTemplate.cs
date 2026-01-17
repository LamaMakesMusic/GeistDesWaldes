using Discord;
using GeistDesWaldes.Attributes;
using GeistDesWaldes.Dictionaries;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Microsoft.Extensions.DependencyInjection;

namespace GeistDesWaldes.Decoration
{
    [Serializable]
    public class LayoutTemplate
    {
        [XmlIgnore] public int TemplateNameHash;
        public string TemplateName;
        public List<ChannelLayout> ChannelLayouts;


        public LayoutTemplate()
        {
            ChannelLayouts = new List<ChannelLayout>();
        }
        public LayoutTemplate(string templateName)
        {
            ChannelLayouts = new List<ChannelLayout>();
            SetName(templateName);
        }

        public void SetName(string name)
        {
            TemplateName = name;
            TemplateNameHash = name.ToLower().GetHashCode();
        }

        public async Task<CustomRuntimeResult> PerformSelfCheck()
        {
            bool issues = false;
            StringBuilder builder = new();

            foreach (ChannelLayout layout in ChannelLayouts)
            {
                CustomRuntimeResult result = layout.PerformSelfCheck();

                if (!result.IsSuccess)
                {
                    issues = true;
                    builder.AppendLine($"......{(await Launcher.Instance.GetChannel<IChannel>(layout.ChannelId))?.Name} ({layout.ChannelId})");
                    builder.AppendLine(result.Reason);
                }
            }

            if (issues)
                return CustomRuntimeResult.FromError(builder.ToString());
            
            return CustomRuntimeResult.FromSuccess();
        }


        public async Task<CustomRuntimeResult> ApplyAsync(LogHandler logger)
        {
            try
            {
                StringBuilder issues = new();

                foreach (ChannelLayout layout in ChannelLayouts)
                {
                    CustomRuntimeResult result = await layout.ApplyChannelLayoutAsync();

                    if (!result.IsSuccess)
                    {
                        IChannel channel = await Launcher.Instance.GetChannel<IChannel>(layout.ChannelId);
                        issues.AppendLine($"{channel.Name} ({channel.Id}): {result.Reason}");
                    }
                }

                if (issues.Length == 0)
                    return CustomRuntimeResult.FromSuccess();

                await logger.Log(new LogMessage(LogSeverity.Error, nameof(ApplyAsync), issues.ToString()));

                return CustomRuntimeResult.FromError(issues.ToString());
            }
            catch (Exception e)
            {
                return CustomRuntimeResult.FromError(e.ToString());
            }
        }
        public async Task<CustomRuntimeResult> RevertAsync(LogHandler logger)
        {
            try
            {
                StringBuilder issues = new();

                foreach (ChannelLayout layout in ChannelLayouts)
                {
                    CustomRuntimeResult result = await layout.RevertChannelLayoutAsync();

                    if (!result.IsSuccess)
                    {
                        IChannel channel = await Launcher.Instance.GetChannel<IChannel>(layout.ChannelId);
                        issues.AppendLine($"{channel.Name} ({channel.Id}): {result.Reason}");
                    }
                }

                if (issues.Length == 0)
                    return CustomRuntimeResult.FromSuccess();

                await logger.Log(new LogMessage(LogSeverity.Error, nameof(ApplyAsync), issues.ToString()));

                return CustomRuntimeResult.FromError(issues.ToString());
            }
            catch (Exception e)
            {
                return CustomRuntimeResult.FromError(e.ToString());
            }
        }


        public async Task<CustomRuntimeResult> AddChannelLayout(ulong channelId)
        {
            if (Launcher.Instance.DiscordClient.GetChannel(channelId) is { } channel)
            {
                if ((await GetChannelLayout(channelId)).IsSuccess)
                    return CustomRuntimeResult.FromError(await ReplyDictionary.ReplaceStringInvariantCase(ReplyDictionary.CHANNEL_X_ALREADY_IN_TEMPLATE, "{x}", channelId.ToString()));

                ChannelLayout layout = new()
                {
                    ChannelId = channel.Id,
                    TemplateReference = this
                };

                ChannelLayouts.Add(layout);

                return CustomRuntimeResult.FromSuccess();
            }

            return CustomRuntimeResult.FromError($"{ReplyDictionary.COULD_NOT_FIND_CHANNEL_WITH_ID} -> {channelId}");
        }
        public async Task<CustomRuntimeResult<ChannelLayout>> GetChannelLayout(ulong channelId)
        {
            ChannelLayout result = ChannelLayouts.Find(c => c.ChannelId == channelId);

            if (result != null)
                return CustomRuntimeResult<ChannelLayout>.FromSuccess(value: result);

            return CustomRuntimeResult<ChannelLayout>.FromError(await ReplyDictionary.ReplaceStringInvariantCase(ReplyDictionary.CHANNEL_X_IS_NOT_IN_TEMPLATE, "{x}", channelId.ToString()));
        }
        
        public async Task<CustomRuntimeResult> RemoveChannelLayout(Server server, ulong channelId, bool revertIfActive = true)
        {
            try
            {
                CustomRuntimeResult<ChannelLayout> getResult = await GetChannelLayout(channelId);

                if (getResult.IsSuccess)
                {
                    if (revertIfActive && server.Services.GetService<LayoutTemplateHandler>().IsActiveLayout(this))
                        await getResult.ResultValue.RevertChannelLayoutAsync();

                    ChannelLayouts.Remove(getResult.ResultValue);
                }

                return getResult;
            }
            catch (Exception e)
            {
                return CustomRuntimeResult.FromError(e.ToString());
            }
        }


        public void RefreshChannelLayoutReference()
        {
            foreach (ChannelLayout c in ChannelLayouts)
                c.TemplateReference = this;
        }
        public void EnsureFormat()
        {
            foreach (ChannelLayout c in ChannelLayouts)
                c.EnsureFormat();
        }


        public string DetailsToString()
        {
            StringBuilder result = new StringBuilder($"{TemplateName} => Channel Layouts:\n");

            foreach (ChannelLayout layout in ChannelLayouts)
                result.Append($" [{layout.DetailsToString()}]");

            return result.ToString();
        }
    }
}
