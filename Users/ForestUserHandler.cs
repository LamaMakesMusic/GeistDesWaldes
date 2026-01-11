using Discord;
using Discord.Commands;
using GeistDesWaldes.Attributes;
using GeistDesWaldes.Configuration;
using GeistDesWaldes.Dictionaries;
using GeistDesWaldes.Misc;
using GeistDesWaldes.TwitchIntegration;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DUser = Discord.Rest.RestUser;
using TUser = TwitchLib.Api.Helix.Models.Users.GetUsers.User;

namespace GeistDesWaldes.Users
{
    public class ForestUserHandler : BaseHandler
    {
        private readonly ConcurrentDictionary<Guid, ForestUser> _users = new ConcurrentDictionary<Guid, ForestUser>();

        private readonly string USERS_DIRECTORY_PATH;

        private Task _userUpdateRoutine;
        private CancellationTokenSource _cancelUpdateSource;

        public ForestUserHandler(Server server) : base(server)
        {
            USERS_DIRECTORY_PATH = Path.Combine(_Server.ServerFilesDirectoryPath, "Users");
        }

        internal override void OnServerStart(object source, EventArgs e)
        {
            base.OnServerStart(source, e);

            StartUpAsync().SafeAsync<ForestUserHandler>(_Server.LogHandler);
        }

        private async Task StartUpAsync()
        {
            await InitializeForestUserHandler();
            await EnsureBotUser();
            
            StartRoutines();
        }
        
        internal override void OnServerShutdown(object source, EventArgs e)
        {
            base.OnServerShutdown(source, e);

            _cancelUpdateSource?.Cancel();

            Task.Run(SaveDirtyUsers).GetAwaiter().GetResult();
        }
        internal override void OnCheckIntegrity(object source, EventArgs e)
        {
            base.OnCheckIntegrity(source, e);

            CheckIntegrity().SafeAsync<ForestUserHandler>(_Server.LogHandler);
        }

        private async Task CheckIntegrity()
        {
            if (_userUpdateRoutine == null)
                await _Server.LogHandler.Log(new LogMessage(LogSeverity.Error, nameof(CheckIntegrity), "Forest User Handler ERROR: User Update Routine not running!"));

            await _Server.LogHandler.Log(new LogMessage(LogSeverity.Info, nameof(CheckIntegrity), "Forest User Handler OK."), (int)ConsoleColor.DarkGreen);

            await _Server.LogHandler.Log(new LogMessage(LogSeverity.Info, nameof(CheckIntegrity), "Checking for duplicate users."));

            foreach (var user in _users.ToArray())
            {
                if (!_users.ContainsKey(user.Key))
                    continue;

                Task.Run(() => MergeDuplicateUsers(_users[user.Key])).GetAwaiter().GetResult();
            }
        }

        private async Task InitializeForestUserHandler()
        {
            await GenericXmlSerializer.EnsurePathExistance<object>(_Server.LogHandler, USERS_DIRECTORY_PATH, null, null);

            await LoadUsersFromFile();            
            await FixMissingUserData();
        }
        private void StartRoutines()
        {
            if (_userUpdateRoutine == null)
                _userUpdateRoutine = Task.Run(UserUpdateLoop);
        }

        private async Task UserUpdateLoop()
        {
            _cancelUpdateSource = new CancellationTokenSource();

            await _Server.LogHandler.Log(new LogMessage(LogSeverity.Verbose, nameof(UserUpdateLoop), "Started."));

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

                await _Server.LogHandler.Log(new LogMessage(LogSeverity.Warning, nameof(UserUpdateLoop), "Stopped."));
            }
        }
        public async Task UpdateRequestedUserInfos()
        {
            try
            {
                var toUpdate = new List<(Guid forestId, ulong discordId, string twitchId)>();


                lock (_users)
                {
                    foreach (var user in _users)
                    {
                        if (user.Value.RequestUpdate)
                            toUpdate.Add((user.Key, user.Value.DiscordUserId, user.Value.TwitchUserId));
                    }
                }

                if (toUpdate.Count > 0)
                {
                    var updateResults = new (DUser discordResponse, TUser twitchResponse)[toUpdate.Count];

                    await _Server.LogHandler.Log(new LogMessage(LogSeverity.Verbose, nameof(UpdateRequestedUserInfos), $"Requesting updates for {toUpdate.Count} {nameof(ForestUser)}s..."));
                
                    var twitchIdUserMap = new List<(int resultIndex, string twitchId)>();
                
                    for (int i = 0; i < toUpdate.Count; i++)
                    {
                        // Discord Update
                        if (toUpdate[i].discordId != default)
                            updateResults[i].discordResponse = await Launcher.Instance.DiscordClient.Rest.GetUserAsync(toUpdate[i].discordId);

                        // Prepare Bulk Twitch Update
                        if (!string.IsNullOrWhiteSpace(toUpdate[i].twitchId))
                            twitchIdUserMap.Add((i, toUpdate[i].twitchId));
                    }
                
                    // Bulk Twitch Update
                    if (TwitchIntegrationHandler.Instance?.API != null && twitchIdUserMap.Count > 0)
                    {
                        var userResponse = await TwitchIntegrationHandler.ValidatedAPICall(TwitchIntegrationHandler.Instance.API.Helix.Users.GetUsersAsync(ids: twitchIdUserMap.Select(e => e.twitchId).ToList()));
                        if (userResponse.Users.Length == twitchIdUserMap.Count)
                        {
                            for (int i = 0; i < twitchIdUserMap.Count; i++)
                            {
                                updateResults[twitchIdUserMap[i].resultIndex].twitchResponse = userResponse.Users[i];
                            }
                        }
                        else
                            await _Server.LogHandler.Log(new LogMessage(LogSeverity.Error, nameof(UpdateRequestedUserInfos), $"Requested user count ({twitchIdUserMap.Count}) does not match twitch response ({userResponse.Users.Length})!"));
                    }


                    // Apply update results
                    lock (_users)
                    {
                        for (int i = 0; i < toUpdate.Count; i++)
                        {
                            if (_users.TryGetValue(toUpdate[i].forestId, out ForestUser forestUser))
                                forestUser.ApplyUserData(updateResults[i].discordResponse, updateResults[i].twitchResponse);
                            else
                                _Server.LogHandler.Log(new LogMessage(LogSeverity.Error, nameof(UpdateRequestedUserInfos), $"Could not get {nameof(ForestUser)} info! ({toUpdate[i].forestId})"));
                        }
                    }
                }


                await SaveDirtyUsers();
            }
            catch (Exception e)
            {
                await _Server.LogHandler.Log(new LogMessage(LogSeverity.Error, nameof(UpdateRequestedUserInfos), "", e));
            }
        }


        public Task SaveUserToFile(Guid userToSave)
        {
            return SaveUserToFile(new Guid[] { userToSave });
        }
        public async Task SaveUserToFile(Guid[] usersToSave)
        {
            await _Server.LogHandler.Log(new LogMessage(LogSeverity.Info, nameof(SaveUserToFile), $"Saving {(usersToSave != null ? usersToSave.Length : 0)} users..."));

            ForestUser copy;

            for (int i = 0; i < usersToSave?.Length; i++)
            {
                if (_users.TryGetValue(usersToSave[i], out ForestUser user))
                {
                    user.IsDirty = false;

                    copy = new ForestUser(user);
                    await GenericXmlSerializer.SaveAsync<ForestUser>(_Server.LogHandler, copy, copy.ForestUserId.ToString(), USERS_DIRECTORY_PATH);
                }
                else
                    await _Server.LogHandler.Log(new LogMessage(LogSeverity.Error, nameof(SaveUserToFile), $"Failed Saving User {usersToSave[i]}! Could not get user from Dictionary!"));
            }
        }
        public async Task LoadUsersFromFile()
        {
            var loadedUsers = await GenericXmlSerializer.LoadAllAsync<ForestUser>(_Server.LogHandler, USERS_DIRECTORY_PATH);

            if (loadedUsers == default)
                await _Server.LogHandler.Log(new LogMessage(LogSeverity.Warning, nameof(LoadUsersFromFile), "Loaded Users == DEFAULT"));
            else
            {
                StringBuilder builder = new StringBuilder();

                lock (_users)
                    _users.Clear();

                if (loadedUsers?.Length > 0)
                {
                    foreach (var user in loadedUsers)
                    {
                        if (user == null || !_users.TryAdd(user.ForestUserId, user))
                            builder.Append($"Could not add loaded user: {user}!");
                    }
                }

                if (builder.Length > 0)
                    await _Server.LogHandler.Log(new LogMessage(LogSeverity.Error, nameof(LoadUsersFromFile), builder.ToString()));
            }
        }
        public async Task FixMissingUserData()
        {
            foreach (KeyValuePair<Guid, ForestUser> fUser in _users)
            {
                if ((fUser.Value.DiscordUserId != default && string.IsNullOrWhiteSpace(fUser.Value.DiscordName)) || (fUser.Value.TwitchUserId != default && string.IsNullOrWhiteSpace(fUser.Value.TwitchName)))
                {
                    await _Server.LogHandler.Log(new LogMessage(LogSeverity.Verbose, nameof(FixMissingUserData), $"Force Updating Data of ({fUser.Value.ForestUserId})..."));
                    
                    fUser.Value.UpdateUserData(true);
                }
            }
        }
        
        public async Task SaveDirtyUsers()
        {
            List<Guid> dirty = new List<Guid>();
            
            lock (_users)
                dirty.AddRange(_users.Where(u => u.Value.IsDirty).Select(u => u.Key));

            if (dirty.Count > 0)
                await SaveUserToFile(dirty.ToArray());
        }

        public Task<Guid> CreateGuid()
        {
            return Task.Run(() =>
            {
                Guid result;

                do
                    result = Guid.NewGuid();
                while (_users.ContainsKey(result));

                return result;
            });
        }

        public async Task<ForestUser> GetOrCreateUser(IUser user)
        {
            ForestUser forestUser = null;
            var getUserResult = await GetUser(user);

            if (getUserResult.IsSuccess)
                forestUser = getUserResult.ResultValue;
            else if (await RegisterUser(user) is CustomRuntimeResult<ForestUser> result && result.IsSuccess)
                forestUser = result.ResultValue;

            forestUser?.UpdateUserData();
            return forestUser;
        }
        public async Task<ForestUser> GetOrCreateUser(string twitchId)
        {
            ForestUser forestUser = null;
            var getUserResult = await GetUser(twitchId: twitchId);

            if (getUserResult.IsSuccess)
                forestUser = getUserResult.ResultValue;
            else if (await RegisterUser(new TwitchUser(twitchId, null)) is CustomRuntimeResult<ForestUser> result && result.IsSuccess)
                forestUser = result.ResultValue;

            forestUser?.UpdateUserData();
            return forestUser;
        }


        public async Task<ForestUser[]> GetUsers(string[] twitchIds)
        {
            List<ForestUser> result = new List<ForestUser>();

            for (int i = 0; i < twitchIds?.Length; i++)
            {
                var getResult = await GetUser(twitchId: twitchIds[i]);

                if (getResult.IsSuccess)
                    result.Add(getResult.ResultValue);
            }

            return result.ToArray();
        }


        public Task<CustomRuntimeResult<ForestUser>> GetUser(IUser user)
        {
            if (user is TwitchUser twitchUser)
                return GetUser(twitchId: twitchUser.TwitchId);
            else
                return GetUser(discordId: user.Id);
        }
        public Task<CustomRuntimeResult<ForestUser>> GetUser(ulong discordId = default, string twitchId = default)
        {
            return Task.Run((() =>
            {
                try
                {
                    if (discordId == default && twitchId == default)
                        throw new Exception("You must specify at least one of the required IDs!");

                    ForestUser result = default;

                    lock (_users)
                        result = _users.FirstOrDefault((u => (discordId == default || u.Value.DiscordUserId == discordId)
                                                            && (twitchId == default || u.Value.TwitchUserId == twitchId))).Value;

                    if (result == default)
                        return CustomRuntimeResult<ForestUser>.FromError(ReplyDictionary.USER_NOT_FOUND);
                    else
                        return CustomRuntimeResult<ForestUser>.FromSuccess(value: result);
                }
                catch (Exception e)
                {
                    return CustomRuntimeResult<ForestUser>.FromError(e.ToString());
                }
            }));
        }
        public Task<CustomRuntimeResult<ForestUser>> GetUser(Guid forestUserId)
        {
            return Task.Run(() =>
            {
                try
                {
                    if (forestUserId == default)
                        throw new Exception("You must specify a Guid!");

                    if (_users.TryGetValue(forestUserId, out ForestUser result))
                        return CustomRuntimeResult<ForestUser>.FromSuccess(value: result);
                    else
                        throw new Exception($"User with id '{forestUserId}' does not exist!");
                }
                catch (Exception e)
                {
                    return CustomRuntimeResult<ForestUser>.FromError(e.ToString());
                }
            });
        }

        private async Task<CustomRuntimeResult<ForestUser>> RegisterUser(IUser user)
        {
            try
            {
                ForestUser forestUser = new ForestUser(await CreateGuid());

                if (user is TwitchUser tUser)
                    forestUser.TwitchUserId = tUser.TwitchId;
                else
                    forestUser.DiscordUserId = user.Id;

                if (!_users.TryAdd(forestUser.ForestUserId, forestUser))
                    throw new Exception($"Could not register user: {forestUser.ForestUserId}!");

                forestUser.UpdateUserData(true);

                await MergeDuplicateUsers(forestUser);

                return CustomRuntimeResult<ForestUser>.FromSuccess(value: forestUser);
            }
            catch (Exception e)
            {
                return CustomRuntimeResult<ForestUser>.FromError(e.ToString());
            }
        }
        private async Task<RuntimeResult> DeleteUser(Guid forestUserId)
        {
            try
            {
                var getUserResult = await GetUser(forestUserId);

                if (getUserResult.IsSuccess)
                {
                    if (_users.TryRemove(forestUserId, out ForestUser user))
                    {
                        if (await GenericXmlSerializer.DeleteAsync(_Server.LogHandler, forestUserId.ToString(), USERS_DIRECTORY_PATH))
                            return CustomRuntimeResult.FromSuccess();
                    }

                    throw new Exception($"Could not remove user with id {forestUserId}!");
                }

                return getUserResult;
            }
            catch (Exception e)
            {
                return CustomRuntimeResult.FromError(e.ToString());
            }
        }
        private async Task<RuntimeResult> ConnectUser(ForestUser existingUser, ForestUser userToConnect)
        {
            try
            {
                if (existingUser.DiscordUserId == default && userToConnect.DiscordUserId != default)
                    existingUser.DiscordUserId = userToConnect.DiscordUserId;
                else if (existingUser.TwitchUserId == default && userToConnect.TwitchUserId != default)
                    existingUser.TwitchUserId = userToConnect.TwitchUserId;

                existingUser.UpdateUserData(true);

                await MergeDuplicateUsers(existingUser);

                return CustomRuntimeResult.FromSuccess();
            }
            catch (Exception e)
            {
                return CustomRuntimeResult.FromError(e.ToString());
            }
        }

        private async Task<CustomRuntimeResult> MergeDuplicateUsers(ForestUser user)
        {
            try
            {
                var duplicateUserCopies = _users.ToList().FindAll(u => u.Key != user.ForestUserId &&
                                                            ((u.Value.DiscordUserId != default && user.DiscordUserId != default && u.Value.DiscordUserId == user.DiscordUserId)
                                                            || (u.Value.TwitchUserId != default && user.TwitchUserId != default && u.Value.TwitchUserId == user.TwitchUserId))
                ).Select(u => u.Value).ToArray();

                if (duplicateUserCopies?.Length > 0)
                {
                    for (int i = 0; i < duplicateUserCopies?.Length; i++)
                    {
                        user.MergeWith(duplicateUserCopies[i]);
                        await _Server.LogHandler.Log(new LogMessage(LogSeverity.Verbose, nameof(MergeDuplicateUsers), $"{user.Name} merged with {duplicateUserCopies[i].Name}"));
                    }

                    user.UpdateUserData(true);

                    for (int i = 0; i < duplicateUserCopies?.Length; i++)
                        await DeleteUser(duplicateUserCopies[i].ForestUserId);
                }

                return CustomRuntimeResult.FromSuccess();
            }
            catch (Exception e)
            {
                return CustomRuntimeResult.FromError(e.ToString());
            }
        }

        public async Task EnsureBotUser()
        {
            ForestUser discordUser = await GetOrCreateUser(Launcher.Instance.GetBotUserDiscord(_Server));

            if (discordUser == null)
                await _Server.LogHandler.Log(new LogMessage(LogSeverity.Error, nameof(EnsureBotUser), $"Could not create {nameof(ForestUser)} for Bot Account [DISCORD] (Guild: {_Server.GuildId})"));


            ForestUser twitchUser = await GetOrCreateUser(Launcher.Instance.GetBotUserTwitch(_Server));

            if (twitchUser == null)
                await _Server.LogHandler.Log(new LogMessage(LogSeverity.Error, nameof(EnsureBotUser), $"Could not create {nameof(ForestUser)} for Bot Account [TWITCH] (Guild: {_Server.GuildId})"));

            // i.a. connect Twitch / Discord user for bot
            if (discordUser != null && twitchUser != null && discordUser.ForestUserId != twitchUser.ForestUserId)
                await ConnectUser(discordUser, twitchUser);
        }

    }
}
