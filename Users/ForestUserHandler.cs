using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using GeistDesWaldes.Attributes;
using GeistDesWaldes.Configuration;
using GeistDesWaldes.Dictionaries;
using GeistDesWaldes.Misc;
using GeistDesWaldes.TwitchIntegration;
using TwitchLib.Api.Helix.Models.Users.GetUsers;
using DUser = Discord.Rest.RestUser;
using TUser = TwitchLib.Api.Helix.Models.Users.GetUsers.User;

namespace GeistDesWaldes.Users;

public class ForestUserHandler : BaseHandler
{
    public override int Priority => -20;

    private readonly ConcurrentDictionary<Guid, ForestUser> _users = new();

    private readonly string _usersDirectoryPath;
    private CancellationTokenSource _cancelUpdateSource;

    private Task _userUpdateRoutine;

    public ForestUserHandler(Server server) : base(server)
    {
        _usersDirectoryPath = Path.Combine(Server.ServerFilesDirectoryPath, "users");
    }

    public override async Task OnServerStartUp()
    {
        await base.OnServerStartUp();

        await InitializeForestUserHandler();
        await EnsureBotUser();

        StartRoutines();
    }

    private async Task InitializeForestUserHandler()
    {
        await GenericXmlSerializer.EnsurePathExistence<object>(Server.LogHandler, _usersDirectoryPath);

        await LoadUsersFromFile();
        await FixMissingUserData();
    }

    private void StartRoutines()
    {
        if (_userUpdateRoutine == null)
        {
            _userUpdateRoutine = Task.Run(UserUpdateLoop);
        }
    }


    public override async Task OnServerShutdown()
    {
        await base.OnServerShutdown();

        _cancelUpdateSource?.Cancel();

        await SaveDirtyUsers();
    }

    public override async Task OnCheckIntegrity()
    {
        await base.OnCheckIntegrity();

        await CheckIntegrity();
    }

    private async Task CheckIntegrity()
    {
        if (_userUpdateRoutine == null)
        {
            await Server.LogHandler.Log(new LogMessage(LogSeverity.Error, nameof(CheckIntegrity), "Forest User Handler ERROR: User Update Routine not running!"));
        }

        await Server.LogHandler.Log(new LogMessage(LogSeverity.Info, nameof(CheckIntegrity), "Forest User Handler OK."), (int)ConsoleColor.DarkGreen);

        await Server.LogHandler.Log(new LogMessage(LogSeverity.Info, nameof(CheckIntegrity), "Checking for duplicate users."));

        foreach (KeyValuePair<Guid, ForestUser> entry in _users.ToArray())
        {
            if (!_users.TryGetValue(entry.Key, out ForestUser user))
            {
                continue;
            }

            await MergeDuplicateUsers(user);
        }
    }


    private async Task UserUpdateLoop()
    {
        _cancelUpdateSource = new CancellationTokenSource();

        await Server.LogHandler.Log(new LogMessage(LogSeverity.Verbose, nameof(UserUpdateLoop), "Started."));

        try
        {
            while (!_cancelUpdateSource.IsCancellationRequested)
            {
                await UpdateRequestedUserInfos();

                await Task.Delay(TimeSpan.FromMinutes(ConfigurationHandler.Shared.DownloadUserDataIntervalInMinutes), _cancelUpdateSource.Token);
            }
        }
        catch (TaskCanceledException)
        {
        }
        finally
        {
            _userUpdateRoutine = null;
            _cancelUpdateSource = null;

            await Server.LogHandler.Log(new LogMessage(LogSeverity.Warning, nameof(UserUpdateLoop), "Stopped."));
        }
    }

    public async Task UpdateRequestedUserInfos()
    {
        try
        {
            var toUpdate = new List<(Guid forestId, ulong discordId, string twitchId)>();


            lock (_users)
            {
                foreach (KeyValuePair<Guid, ForestUser> user in _users)
                {
                    if (user.Value.RequestUpdate)
                    {
                        toUpdate.Add((user.Key, user.Value.DiscordUserId, user.Value.TwitchUserId));
                    }
                }
            }

            if (toUpdate.Count > 0)
            {
                var updateResults = new (DUser discordResponse, TUser twitchResponse)[toUpdate.Count];

                await Server.LogHandler.Log(new LogMessage(LogSeverity.Verbose, nameof(UpdateRequestedUserInfos), $"Requesting updates for {toUpdate.Count} {nameof(ForestUser)}s..."));

                var twitchIdUserMap = new List<(int resultIndex, string twitchId)>();

                for (int i = 0; i < toUpdate.Count; i++)
                {
                    // Discord Update
                    if (toUpdate[i].discordId != default)
                    {
                        updateResults[i].discordResponse = await Launcher.Instance.DiscordClient.Rest.GetUserAsync(toUpdate[i].discordId);
                    }

                    // Prepare Bulk Twitch Update
                    if (!string.IsNullOrWhiteSpace(toUpdate[i].twitchId))
                    {
                        twitchIdUserMap.Add((i, toUpdate[i].twitchId));
                    }
                }

                // Bulk Twitch Update
                if (TwitchIntegrationHandler.Instance?.Api != null && twitchIdUserMap.Count > 0)
                {
                    GetUsersResponse userResponse = await TwitchIntegrationHandler.ValidatedApiCall(TwitchIntegrationHandler.Instance.Api.Helix.Users.GetUsersAsync(twitchIdUserMap.Select(e => e.twitchId).ToList()));
                    if (userResponse.Users.Length == twitchIdUserMap.Count)
                    {
                        for (int i = 0; i < twitchIdUserMap.Count; i++)
                        {
                            updateResults[twitchIdUserMap[i].resultIndex].twitchResponse = userResponse.Users[i];
                        }
                    }
                    else
                    {
                        await Server.LogHandler.Log(new LogMessage(LogSeverity.Error, nameof(UpdateRequestedUserInfos), $"Requested user count ({twitchIdUserMap.Count}) does not match twitch response ({userResponse.Users.Length})!"));
                    }
                }


                // Apply update results
                lock (_users)
                {
                    for (int i = 0; i < toUpdate.Count; i++)
                    {
                        if (_users.TryGetValue(toUpdate[i].forestId, out ForestUser forestUser))
                        {
                            forestUser.ApplyUserData(updateResults[i].discordResponse, updateResults[i].twitchResponse);
                        }
                        else
                        {
                            Server.LogHandler.Log(new LogMessage(LogSeverity.Error, nameof(UpdateRequestedUserInfos), $"Could not get {nameof(ForestUser)} info! ({toUpdate[i].forestId})"));
                        }
                    }
                }
            }


            await SaveDirtyUsers();
        }
        catch (Exception e)
        {
            await Server.LogHandler.Log(new LogMessage(LogSeverity.Error, nameof(UpdateRequestedUserInfos), "", e));
        }
    }


    public Task SaveUserToFile(Guid userToSave)
    {
        return SaveUserToFile([userToSave]);
    }

    public async Task SaveUserToFile(Guid[] usersToSave)
    {
        await Server.LogHandler.Log(new LogMessage(LogSeverity.Info, nameof(SaveUserToFile), $"Saving {usersToSave?.Length ?? 0} users..."));

        for (int i = 0; i < usersToSave?.Length; i++)
        {
            if (_users.TryGetValue(usersToSave[i], out ForestUser user))
            {
                user.IsDirty = false;

                ForestUser copy = new(user);
                await GenericXmlSerializer.SaveAsync<ForestUser>(Server.LogHandler, copy, copy.ForestUserId.ToString(), _usersDirectoryPath);
            }
            else
            {
                await Server.LogHandler.Log(new LogMessage(LogSeverity.Error, nameof(SaveUserToFile), $"Failed Saving User {usersToSave[i]}! Could not get user from Dictionary!"));
            }
        }
    }

    public async Task LoadUsersFromFile()
    {
        ForestUser[] loadedUsers = await GenericXmlSerializer.LoadAllAsync<ForestUser>(Server.LogHandler, _usersDirectoryPath);

        if (loadedUsers == null)
        {
            await Server.LogHandler.Log(new LogMessage(LogSeverity.Warning, nameof(LoadUsersFromFile), "Loaded Users == DEFAULT"));
        }
        else
        {
            StringBuilder builder = new();

            lock (_users)
            {
                _users.Clear();
            }

            if (loadedUsers.Length > 0)
            {
                foreach (ForestUser user in loadedUsers)
                {
                    if (user == null || !_users.TryAdd(user.ForestUserId, user))
                    {
                        builder.Append($"Could not add loaded user: {user}!");
                    }
                }
            }

            if (builder.Length > 0)
            {
                await Server.LogHandler.Log(new LogMessage(LogSeverity.Error, nameof(LoadUsersFromFile), builder.ToString()));
            }
        }
    }

    public async Task FixMissingUserData()
    {
        foreach (KeyValuePair<Guid, ForestUser> fUser in _users)
        {
            if ((fUser.Value.DiscordUserId != 0 && string.IsNullOrWhiteSpace(fUser.Value.DiscordName)) ||
                (fUser.Value.TwitchUserId != null && string.IsNullOrWhiteSpace(fUser.Value.TwitchName)))
            {
                await Server.LogHandler.Log(new LogMessage(LogSeverity.Verbose, nameof(FixMissingUserData), $"Force Updating Data of ({fUser.Value.ForestUserId})..."));

                fUser.Value.UpdateUserData(true);
            }
        }
    }

    private async Task SaveDirtyUsers()
    {
        List<Guid> dirty = [];

        lock (_users)
        {
            dirty.AddRange(_users.Where(u => u.Value.IsDirty).Select(u => u.Key));
        }

        if (dirty.Count > 0)
        {
            await SaveUserToFile(dirty.ToArray());
        }
    }

    private Task<Guid> CreateGuid()
    {
        Guid result;

        do
        {
            result = Guid.NewGuid();
        } while (_users.ContainsKey(result));

        return Task.FromResult(result);
    }

    public async Task<ForestUser> GetOrCreateUser(IUser user)
    {
        ForestUser forestUser = null;
        CustomRuntimeResult<ForestUser> getUserResult = await GetUser(user);

        if (getUserResult.IsSuccess)
        {
            forestUser = getUserResult.ResultValue;
        }
        else if (await RegisterUser(user) is { IsSuccess: true } result)
        {
            forestUser = result.ResultValue;
        }

        forestUser?.UpdateUserData();
        return forestUser;
    }

    public async Task<ForestUser> GetOrCreateUser(string twitchId)
    {
        ForestUser forestUser = null;
        CustomRuntimeResult<ForestUser> getUserResult = await GetUser(twitchId: twitchId);

        if (getUserResult.IsSuccess)
        {
            forestUser = getUserResult.ResultValue;
        }
        else if (await RegisterUser(new TwitchUser(twitchId, null)) is CustomRuntimeResult<ForestUser> result && result.IsSuccess)
        {
            forestUser = result.ResultValue;
        }

        forestUser?.UpdateUserData();
        return forestUser;
    }


    public async Task<ForestUser[]> GetUsers(string[] twitchIds)
    {
        var result = new List<ForestUser>();

        for (int i = 0; i < twitchIds?.Length; i++)
        {
            CustomRuntimeResult<ForestUser> getResult = await GetUser(twitchId: twitchIds[i]);

            if (getResult.IsSuccess)
            {
                result.Add(getResult.ResultValue);
            }
        }

        return result.ToArray();
    }


    public Task<CustomRuntimeResult<ForestUser>> GetUser(IUser user)
    {
        if (user is TwitchUser twitchUser)
        {
            return GetUser(twitchId: twitchUser.TwitchId);
        }

        return GetUser(user.Id);
    }

    public Task<CustomRuntimeResult<ForestUser>> GetUser(ulong discordId = 0, string twitchId = null)
    {
        try
        {
            if (discordId == 0 && twitchId == null)
            {
                throw new Exception("You must specify at least one of the required IDs!");
            }

            ForestUser result;

            lock (_users)
            {
                result = _users.FirstOrDefault(u => (discordId == 0 || u.Value.DiscordUserId == discordId)
                                                    && (twitchId == null || u.Value.TwitchUserId == twitchId)).Value;
            }

            if (result == null)
            {
                return Task.FromResult(CustomRuntimeResult<ForestUser>.FromError(ReplyDictionary.USER_NOT_FOUND));
            }

            return Task.FromResult(CustomRuntimeResult<ForestUser>.FromSuccess(value: result));
        }
        catch (Exception e)
        {
            return Task.FromResult(CustomRuntimeResult<ForestUser>.FromError(e.ToString()));
        }
    }

    public Task<CustomRuntimeResult<ForestUser>> GetUser(Guid forestUserId)
    {
        try
        {
            if (forestUserId == Guid.Empty)
            {
                throw new Exception("You must specify a Guid!");
            }

            if (_users.TryGetValue(forestUserId, out ForestUser result))
            {
                return Task.FromResult(CustomRuntimeResult<ForestUser>.FromSuccess(value: result));
            }

            throw new Exception($"User with id '{forestUserId}' does not exist!");
        }
        catch (Exception e)
        {
            return Task.FromResult(CustomRuntimeResult<ForestUser>.FromError(e.ToString()));
        }
    }

    private async Task<CustomRuntimeResult<ForestUser>> RegisterUser(IUser user)
    {
        try
        {
            ForestUser forestUser = new(await CreateGuid());

            if (user is TwitchUser tUser)
            {
                forestUser.TwitchUserId = tUser.TwitchId;
            }
            else
            {
                forestUser.DiscordUserId = user.Id;
            }

            if (!_users.TryAdd(forestUser.ForestUserId, forestUser))
            {
                throw new Exception($"Could not register user: {forestUser.ForestUserId}!");
            }

            forestUser.UpdateUserData(true);

            await MergeDuplicateUsers(forestUser);

            return CustomRuntimeResult<ForestUser>.FromSuccess(value: forestUser);
        }
        catch (Exception e)
        {
            return CustomRuntimeResult<ForestUser>.FromError(e.ToString());
        }
    }

    private async Task ConnectUser(ForestUser existingUser, ForestUser userToConnect)
    {
        try
        {
            if (existingUser.DiscordUserId == 0 && userToConnect.DiscordUserId != 0)
            {
                existingUser.DiscordUserId = userToConnect.DiscordUserId;
            }
            else if (existingUser.TwitchUserId == null && userToConnect.TwitchUserId != null)
            {
                existingUser.TwitchUserId = userToConnect.TwitchUserId;
            }

            existingUser.UpdateUserData(true);

            await MergeDuplicateUsers(existingUser);
        }
        catch (Exception e)
        {
            await Server.LogHandler.Log(new LogMessage(LogSeverity.Error, nameof(ConnectUser), string.Empty, e));
        }
    }

    private async Task<CustomRuntimeResult> MergeDuplicateUsers(ForestUser user)
    {
        try
        {
            ForestUser[] duplicateUserCopies = _users.ToList().FindAll(u => u.Key != user.ForestUserId &&
                                                                            ((u.Value.DiscordUserId != 0 && user.DiscordUserId != 0 && u.Value.DiscordUserId == user.DiscordUserId)
                                                                             || (u.Value.TwitchUserId != null && user.TwitchUserId != null && u.Value.TwitchUserId == user.TwitchUserId))
            ).Select(u => u.Value).ToArray();

            if (duplicateUserCopies.Length > 0)
            {
                for (int i = 0; i < duplicateUserCopies.Length; i++)
                {
                    user.MergeWith(duplicateUserCopies[i]);
                    await Server.LogHandler.Log(new LogMessage(LogSeverity.Verbose, nameof(MergeDuplicateUsers), $"{user.Name} merged with {duplicateUserCopies[i].Name}"));
                }

                user.UpdateUserData(true);

                for (int i = 0; i < duplicateUserCopies.Length; i++)
                {
                    await DeleteUser(duplicateUserCopies[i].ForestUserId);
                }
            }

            return CustomRuntimeResult.FromSuccess();
        }
        catch (Exception e)
        {
            return CustomRuntimeResult.FromError(e.ToString());
        }
    }

    private async Task DeleteUser(Guid forestUserId)
    {
        try
        {
            CustomRuntimeResult<ForestUser> getUserResult = await GetUser(forestUserId);

            if (!getUserResult.IsSuccess)
            {
                return;
            }

            if (_users.TryRemove(forestUserId, out ForestUser _))
            {
                if (await GenericXmlSerializer.DeleteAsync(Server.LogHandler, forestUserId.ToString(), _usersDirectoryPath))
                {
                    return;
                }
            }

            throw new Exception($"Could not remove user with id {forestUserId}!");
        }
        catch (Exception e)
        {
            await Server.LogHandler.Log(new LogMessage(LogSeverity.Error, nameof(DeleteUser), string.Empty, e));
        }
    }


    public async Task EnsureBotUser()
    {
        ForestUser discordUser = await GetOrCreateUser(Launcher.Instance.GetBotUserDiscord(Server));

        if (discordUser == null)
        {
            await Server.LogHandler.Log(new LogMessage(LogSeverity.Error, nameof(EnsureBotUser), $"Could not create {nameof(ForestUser)} for Bot Account [DISCORD] (Guild: {Server.GuildId})"));
        }


        ForestUser twitchUser = await GetOrCreateUser(Launcher.Instance.GetBotUserTwitch(Server));

        if (twitchUser == null)
        {
            await Server.LogHandler.Log(new LogMessage(LogSeverity.Error, nameof(EnsureBotUser), $"Could not create {nameof(ForestUser)} for Bot Account [TWITCH] (Guild: {Server.GuildId})"));
        }

        // i.a. connect Twitch / Discord user for bot
        if (discordUser != null && twitchUser != null && discordUser.ForestUserId != twitchUser.ForestUserId)
        {
            await ConnectUser(discordUser, twitchUser);
        }
    }
}