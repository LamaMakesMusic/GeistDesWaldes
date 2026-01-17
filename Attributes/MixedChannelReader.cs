using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;

namespace GeistDesWaldes.Attributes;

public class MixedChannelReader<T> : TypeReader where T : class, IChannel
{
    public override async Task<TypeReaderResult> ReadAsync(ICommandContext context, string input, IServiceProvider services)
    {
        Server server = (Server)services.GetService(typeof(Server));

        return await ParseChannel(input, server.Guild);
    }

    public static async Task<TypeReaderResult> ParseChannel(string input, IGuild guild)
    {
        try
        {
            input = input.TrimStart().TrimEnd();

            if (string.IsNullOrWhiteSpace(input))
            {
                return TypeReaderResult.FromSuccess(null);
            }

            T result = null;

            if (ulong.TryParse(input, out ulong parsedChannel))
            {
                result = await Launcher.Instance.GetChannel<T>(parsedChannel);
            }

            if (result == null)
            {
                result = await Launcher.Instance.GetChannel<T>(input, guild.Id);
            }

            if (result == null)
            {
                throw new Exception($"Could not find Channel '{input}' for guild '{guild.Name}'");
            }

            return TypeReaderResult.FromSuccess(result);
        }
        catch (Exception e)
        {
            return TypeReaderResult.FromError(e);
        }
    }
}

public class MixedChannelArrayReader<T> : TypeReader where T : class, IChannel
{
    public override async Task<TypeReaderResult> ReadAsync(ICommandContext context, string input, IServiceProvider services)
    {
        try
        {
            input = input.TrimStart().TrimEnd();

            if (string.IsNullOrWhiteSpace(input))
            {
                return TypeReaderResult.FromSuccess(null);
            }


            TypeReaderResult splitResult = await ArrayReader.SplitToArray(input);

            if (splitResult.IsSuccess && (string[])splitResult.BestMatch is string[] stringArray)
            {
                var result = new List<T>();

                for (int i = 0; i < stringArray?.Length; i++)
                {
                    TypeReaderResult parseResult = await MixedChannelReader<T>.ParseChannel(input, context.Guild);

                    if (parseResult.IsSuccess && (T)parseResult.BestMatch is T channel && channel != null)
                    {
                        result.Add(channel);
                    }
                }

                return TypeReaderResult.FromSuccess(result.ToArray());
            }

            return splitResult;
        }
        catch (Exception e)
        {
            return TypeReaderResult.FromError(e);
        }
    }
}