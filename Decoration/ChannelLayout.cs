using Discord;
using Discord.WebSocket;
using GeistDesWaldes.Attributes;
using GeistDesWaldes.Dictionaries;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using static GeistDesWaldes.Decoration.ChannelLayoutMap;

namespace GeistDesWaldes.Decoration
{
    [Serializable]
    public class ChannelLayout
    {
        /* Settings for Channel Layouts
         * e.g.:
         *      ChannelId Points to Channel "Fotos"
         *      ChannelNameLayout has two entries: (0,{a}), (4,{b})
         *      When applying the template, this mapping belongs to,
         *      the channel name gets two deco-elements (as long as they are defined in the template)
         *      -> {a}Fotos{b}
         *      
         *      The logic for the ChannelTopicLayout works the same way, but with indices pointing to whole lines
         */

        public ulong ChannelId;

        public List<ChannelLayoutMap> LayoutMaps;

        internal LayoutTemplate TemplateReference;


        public ChannelLayout()
        {
            LayoutMaps = new List<ChannelLayoutMap>();
        }

        public CustomRuntimeResult PerformSelfCheck()
        {
            bool issues = false;
            var builder = new StringBuilder($".........");

            var channel = Launcher.Instance.DiscordClient.GetChannel(ChannelId);

            if (TemplateReference == null)
            {
                issues = true;
                builder.Append($"{nameof(TemplateReference)} == NULL | ");
            }

            if (channel == null)
            {
                issues = true;
                builder.Append($"Channel == NULL | ");
            }
            else if ((channel is SocketTextChannel || channel is SocketVoiceChannel) == false)
            {
                issues = true;
                builder.Append($"Channel is NOT {nameof(SocketTextChannel)} nor {nameof(SocketVoiceChannel)} | ");
            }

            if (LayoutMaps == null || LayoutMaps.Count == 0)
            {
                issues = true;
                builder.Append($"No Layout Maps defined! | ");
            }

            if (issues)
                return CustomRuntimeResult.FromError(builder.ToString());
            else
                return CustomRuntimeResult.FromSuccess();
        }


        private (string name, string topic) GetChannelInfo()
        {
            string convName = null;
            string convTopic = null;

            IChannel channel = Launcher.Instance.DiscordClient.GetChannel(ChannelId);

            if (channel is ITextChannel textChannel)
            {
                convName = textChannel.Name;
                convTopic = textChannel.Topic;
            }
            else if (channel is IVoiceChannel voiceChannel)
            {
                convName = voiceChannel.Name;
                convTopic = null;
            }

            byte[] nameData = Encoding.Unicode.GetBytes(string.IsNullOrWhiteSpace(convName) ? "" : convName.Trim());
            convName = nameData != null ? Encoding.Unicode.GetString(nameData) : "";

            byte[] topicData = Encoding.Unicode.GetBytes(string.IsNullOrWhiteSpace(convTopic) ? "" : convTopic);
            convTopic = topicData != null ? Encoding.Unicode.GetString(topicData) : "";

            return (convName, convTopic);
        }


        private async Task<CustomRuntimeResult> ModifyChannel(string newChannelName, string newChannelTopic)
        {
            try
            {
                SocketChannel cachedChannel = Launcher.Instance.DiscordClient.GetChannel(ChannelId);

                if (cachedChannel == null)
                    return CustomRuntimeResult.FromError($"{ReplyDictionary.COULD_NOT_FIND_CHANNEL_WITH_ID} -> {ChannelId}");

                RequestOptions requestOptions = new RequestOptions()
                {
                    AuditLogReason = "Applying Bot Template",
                    RetryMode = RetryMode.AlwaysFail,
                    Timeout = 30
                };


                if (cachedChannel is SocketTextChannel textChannel)
                {
                    await textChannel.ModifyAsync(t =>
                    {
                        t.Name = newChannelName;
                        t.Topic = newChannelTopic;
                    },
                    requestOptions);


                    // Go easy on the API Calls
                    await Task.Delay(10000);


                    var checkInfo = GetChannelInfo();

                    bool changedName = checkInfo.name.Equals(newChannelName, StringComparison.Ordinal);
                    bool changedTopic = checkInfo.topic.Equals(newChannelTopic, StringComparison.Ordinal);

                    if (changedName && changedTopic)
                        return CustomRuntimeResult.FromSuccess();
                    else
                    {
                        string errName = changedName ? "-" : $"Error changing name ('{checkInfo.name}' != '{newChannelName}')";
                        string errTopic = changedTopic ? "-" : $"Error changing name ('{checkInfo.topic}' != '{newChannelTopic}')";
                        return CustomRuntimeResult.FromError($"{errName} | {errTopic}");
                    }
                }
                else if (cachedChannel is SocketVoiceChannel voiceChannel)
                {
                    await voiceChannel.ModifyAsync(v =>
                    {
                        v.Name = newChannelName;
                    },
                    requestOptions);


                    // Go easy on the API Calls
                    await Task.Delay(10000);


                    var checkInfo = GetChannelInfo();

                    bool changedName = checkInfo.name.Equals(newChannelName, StringComparison.Ordinal);

                    if (changedName)
                        return CustomRuntimeResult.FromSuccess();
                    else
                        return CustomRuntimeResult.FromError(changedName ? "-" : $"Error changing name ({checkInfo.name} != {newChannelName})");
                }
                else
                    return CustomRuntimeResult.FromError($"{ReplyDictionary.CHANNEL_IS_NOT_TEXT_NOR_VOICE_CHANNEL} -> {ChannelId}");
            }
            catch (Exception e)
            {
                return CustomRuntimeResult.FromError(e.ToString());
            }
        }


        public async Task<CustomRuntimeResult> AddLayoutMap(Server server, ChannelLayoutMap map)
        {
            var match = LayoutMaps.Find(l => l.Index == map.Index && l.LayoutTarget == map.LayoutTarget && l.Value.Equals(map.Value, StringComparison.Ordinal));

            if (match != default)
                return CustomRuntimeResult.FromError($"{ReplyDictionary.MAP_OF_THIS_KIND_ALREADY_EXISTS_FOR_THIS_CONSTELLATION} (channel: {ChannelId} | target: {match.LayoutTarget})");

            bool isActive = server.LayoutTemplateHandler.IsActiveLayout(TemplateReference);
            if (isActive)
                await RevertChannelLayoutAsync();

            LayoutMaps.Add(map);

            if (isActive)
                await ApplyChannelLayoutAsync();

            return CustomRuntimeResult.FromSuccess();
        }
        public async Task<CustomRuntimeResult> RemoveLayoutMap(Server server, ChannelLayoutMap map)
        {
            var match = LayoutMaps.Find(l => l.Index == map.Index && l.LayoutTarget == map.LayoutTarget && l.Value.Equals(map.Value, StringComparison.Ordinal));

            if (match == default)
                return CustomRuntimeResult.FromError($"{ReplyDictionary.MAP_OF_THIS_KIND_DOES_NOT_EXISTS_FOR_THIS_CONSTELLATION} (channel: {ChannelId} | target: {match.LayoutTarget})");

            bool isActive = server.LayoutTemplateHandler.IsActiveLayout(TemplateReference);
            if (isActive)
                await RevertChannelLayoutAsync();

            LayoutMaps.Remove(match);

            if (isActive)
                await ApplyChannelLayoutAsync();

            return CustomRuntimeResult.FromSuccess();
        }

        public async Task<CustomRuntimeResult> ApplyChannelLayoutAsync()
        {
            try
            {
                var (name, topic) = GetChannelInfo();

                // e.g. "channelname" >>> [0,(°-°)] ==> "(°-°)channelname"
                foreach (var map in LayoutMaps)
                {
                    switch (map.LayoutTarget)
                    {
                        case LayoutTargetOption.ChannelName:
                            name = name.Insert((map.Index < name.Length ? (map.Index < 0 ? 0 : map.Index) : name.Length), map.Value);
                            break;
                        case LayoutTargetOption.ChannelTopic:
                            if (topic != null)
                                topic = topic.Insert((map.Index < topic.Length ? (map.Index < 0 ? 0 : map.Index) : topic.Length), map.Value);
                            break;
                        default:
                            break;
                    }
                }

                return await ModifyChannel(name, topic);
            }
            catch (Exception e)
            {
                return CustomRuntimeResult.FromError(e.ToString());
            }
        }
        public async Task<CustomRuntimeResult> RevertChannelLayoutAsync()
        {
            try
            {
                // Try reversing the process
                var (name, topic) = GetChannelInfo();

                foreach (var map in LayoutMaps)
                {
                    switch (map.LayoutTarget)
                    {
                        case LayoutTargetOption.ChannelName:
                            name = name.Replace(map.Value, string.Empty, StringComparison.Ordinal);
                            break;
                        case LayoutTargetOption.ChannelTopic:
                            if (topic != null)
                                topic = topic.Replace(map.Value, string.Empty, StringComparison.Ordinal);
                            break;
                        default:
                            break;
                    }
                }

                return await ModifyChannel(name, topic);
            }
            catch (Exception e)
            {
                return CustomRuntimeResult.FromError(e.ToString());
            }
        }

        public void EnsureFormat()
        {
            foreach (var m in LayoutMaps)
                m.EnsureFormat();
        }

        public string DetailsToString()
        {
            var result = new StringBuilder();
            var channel = Launcher.Instance.DiscordClient.GetChannel(ChannelId);

            string channelName = ChannelId.ToString();
            if (channel is SocketTextChannel tc)
                channelName = tc.Name;
            else if (channel is SocketVoiceChannel vc)
                channelName = vc.Name;


            result.Append($"{channelName}: \n");


            foreach (LayoutTargetOption option in Enum.GetValues(typeof(LayoutTargetOption)))
            {
                result.Append($"{option}(");

                var maps = LayoutMaps.FindAll(m => m.LayoutTarget == option);
                foreach (var map in maps)
                    result.Append($"{map} | ");

                if (maps.Count > 0)
                    result.Remove(result.Length - 2, 2);

                result.Append(") \n");
            }


            return result.ToString();
        }

    }
}
