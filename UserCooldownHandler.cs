using Discord;
using GeistDesWaldes.TwitchIntegration;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GeistDesWaldes
{
    public class UserCooldownHandler : BaseHandler
    {
        private readonly ConcurrentDictionary<ulong, long> _discordCooldown = new ConcurrentDictionary<ulong, long>();
        private readonly ConcurrentDictionary<string, long> _twitchCooldown = new ConcurrentDictionary<string, long>();

        private readonly List<string> _toRemoveTwitch = new List<string>();
        private readonly List<ulong> _toRemoveDiscord = new List<ulong>();


        public UserCooldownHandler(Server server) : base(server)
        {
            
        }

        public async Task AddToCooldown(IUser user)
        {
            long cooldownUpdate = DateTime.UtcNow.AddSeconds(_Server.Config.UserSettings.UserCooldownInSeconds).Ticks;

            if (user is TwitchUser tUser)
            {
                if (_twitchCooldown.TryGetValue(tUser.TwitchId, out long existing))
                    existing = cooldownUpdate;
                else if (_twitchCooldown.TryAdd(tUser.TwitchId, cooldownUpdate) == false)
                    await _Server.LogHandler.Log(new LogMessage(LogSeverity.Warning, nameof(AddToCooldown), "Could not add user to cooldown!"));
            }
            else
            {
                if (_discordCooldown.TryGetValue(user.Id, out long existing))
                    existing = cooldownUpdate;
                else if (_discordCooldown.TryAdd(user.Id, cooldownUpdate) == false)
                    await _Server.LogHandler.Log(new LogMessage(LogSeverity.Warning, nameof(AddToCooldown), "Could not add user to cooldown!"));
            }
        }

        public async Task UpdateCooldown()
        {
            try
            {
                lock (_discordCooldown)
                {
                    foreach (var dUser in _discordCooldown)
                    {
                        if (DateTime.UtcNow.Ticks < dUser.Value)
                            continue;

                        _toRemoveDiscord.Add(dUser.Key);
                    }

                    for (int i = _toRemoveDiscord.Count - 1; i >= 0; i--)
                    {
                        if (_discordCooldown.TryRemove(_toRemoveDiscord[i], out long user))
                            _toRemoveDiscord.RemoveAt(i);
                    }
                }

                _toRemoveDiscord.Clear();


                lock (_twitchCooldown)
                {
                    foreach (var tUser in _twitchCooldown)
                    {
                        if (DateTime.UtcNow.Ticks < tUser.Value)
                            continue;

                        _toRemoveTwitch.Add(tUser.Key);
                    }

                    for (int i = _toRemoveTwitch.Count - 1; i >= 0; i--)
                    {
                        if (_twitchCooldown.TryRemove(_toRemoveTwitch[i], out long user))
                            _toRemoveTwitch.RemoveAt(i);
                    }
                }

                _toRemoveTwitch.Clear();
            }
            catch (Exception e)
            {
                await _Server.LogHandler.Log(new LogMessage(LogSeverity.Error, nameof(UpdateCooldown), "", e));
            }

        }

        public async Task<bool> IsOnCooldown(IUser user, bool skipUpdate = false)
        {
            if (!skipUpdate)
                await UpdateCooldown();

            if (user is TwitchUser twitchUser)
                return _twitchCooldown.ContainsKey(twitchUser.TwitchId);
            else
                return _discordCooldown.ContainsKey(user.Id);
        }

    }
}
