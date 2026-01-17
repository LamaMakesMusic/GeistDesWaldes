using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using GeistDesWaldes.Attributes;
using GeistDesWaldes.Audio;
using GeistDesWaldes.Dictionaries;

namespace GeistDesWaldes.Modules;

[RequireUserPermission(GuildPermission.Administrator, Group = "AudioPermissions")]
[RequireUserPermission(GuildPermission.ManageChannels, Group = "AudioPermissions")]
[RequireTwitchBadge(BadgeTypeOption.Broadcaster | BadgeTypeOption.Moderator, Group = "AudioPermissions")]
[RequireIsBot(Group = "AudioPermissions")]
[Group("audio")]
public class AudioModule : ModuleBase<CommandContext>, ICommandModule
{
    public Server Server { get; set; }

    [Command("join")]
    [Summary("Lets the bot join a voice channel.")]
    public async Task<RuntimeResult> JoinChannel(IVoiceChannel channel = null)
    {
        try
        {
            if (channel == null)
            {
                channel = (Context.User as IGuildUser)?.VoiceChannel;
            }

            await channel.ConnectAsync();

            return CustomRuntimeResult.FromSuccess();
        }
        catch (Exception e)
        {
            return CustomRuntimeResult.FromError(e.ToString());
        }
    }

    [Command("leave")]
    [Summary("Lets the bot leave the current voice channel.")]
    public async Task<RuntimeResult> LeaveChannel()
    {
        return await Server.GetModule<AudioHandler>().LeaveVoiceChannel();
    }

    [Command("play")]
    [Summary("Play an audio file in the joined voice channel.")]
    public async Task<RuntimeResult> PlayFile(string urlOrLocalPath)
    {
        return await Server.GetModule<AudioHandler>().QueueAudioFileAtPath(urlOrLocalPath, Context);
    }

    [Command("playRandom")]
    [Alias("play random")]
    [Summary("Plays a random audiofile in the joined voice channel.")]
    public async Task<RuntimeResult> PlayFileRandom(string[] audioFiles)
    {
        if (audioFiles == null || audioFiles.Length < 1)
        {
            return CustomRuntimeResult.FromError(ReplyDictionary.PARAMETER_MUST_NOT_BE_EMPTY);
        }

        audioFiles = await Server.GetModule<AudioHandler>().GetAllFilesInPaths(audioFiles, Server.GetModule<AudioHandler>().AudioDirectoryPath);

        if (audioFiles == null || audioFiles.Length == 0)
        {
            return CustomRuntimeResult.FromError(ReplyDictionary.COULD_NOT_FIND_FILES);
        }

        return await PlayFile(audioFiles[Launcher.Random.Next(audioFiles.Length)]);
    }
}