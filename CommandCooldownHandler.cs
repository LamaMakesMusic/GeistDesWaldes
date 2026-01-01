using Discord;
using Discord.Commands;
using GeistDesWaldes.UserCommands;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace GeistDesWaldes
{
    public class CommandCooldownHandler : BaseHandler
    {
        private ConcurrentDictionary<string, long> _commandsOnCooldown; // ID - CooldownEndInTicks
        private ConcurrentDictionary<string, long> _categoriesOnCooldown;

        public CommandCooldownHandler(Server server) : base(server)
        {
            _commandsOnCooldown = new ConcurrentDictionary<string, long>();
            _categoriesOnCooldown = new ConcurrentDictionary<string, long>();
        }


        public Task<double> IsOnCooldown(CommandInfo command)
        {
            return Task.Run(async () =>
            {
                await UpdateCommandCooldown();

                if (command != null && _commandsOnCooldown.TryGetValue(command.Name, out long ticks))
                {
                    // Is there still time that needs to pass?
                    if (DateTime.Now.Ticks < ticks)
                        return new TimeSpan(ticks - DateTime.Now.Ticks).TotalSeconds;
                }

                return 0;
            });
        }
        public Task<double> IsOnCooldown(CustomCommandCategory category)
        {
            return Task.Run(async () =>
            {
                await UpdateCategoryCooldown();

                if (category != null && _categoriesOnCooldown.TryGetValue(category.Name, out long ticks))
                {
                    // Is there still time that needs to pass?
                    if (DateTime.Now.Ticks < ticks)
                        return new TimeSpan(ticks - DateTime.Now.Ticks).TotalSeconds;
                }

                return 0;
            });
        }

        public async Task UpdateCommandCooldown()
        {
            foreach (var idTicksPair in _commandsOnCooldown)
            {
                // Still on Cooldown?
                if (DateTime.Now.Ticks < idTicksPair.Value)
                    continue;

                // Remove From Cooldown
                if (_commandsOnCooldown.TryRemove(idTicksPair.Key, out long value))
                    await _Server.LogHandler.Log(new LogMessage(LogSeverity.Verbose, nameof(UpdateCommandCooldown), $"Removed '{idTicksPair.Key}' from Cooldown."));
                else
                    await _Server.LogHandler.Log(new LogMessage(LogSeverity.Warning, nameof(UpdateCommandCooldown), $"Failed removing '{idTicksPair.Key}' from Cooldown."));
            }
        }
        public async Task UpdateCategoryCooldown()
        {
            foreach (var idTicksPair in _categoriesOnCooldown)
            {
                // Still on Cooldown?
                if (DateTime.Now.Ticks < idTicksPair.Value)
                    continue;

                // Remove From Cooldown
                if (_categoriesOnCooldown.TryRemove(idTicksPair.Key, out long value))
                    await _Server.LogHandler.Log(new LogMessage(LogSeverity.Verbose, nameof(UpdateCategoryCooldown), $"Removed '{idTicksPair.Key}' from Cooldown."));
                else
                    await _Server.LogHandler.Log(new LogMessage(LogSeverity.Warning, nameof(UpdateCategoryCooldown), $"Failed removing '{idTicksPair.Key}' from Cooldown."));
            }
        }

        public async Task AddToCooldown(CommandInfo command, float durationInSeconds)
        {
            if (_commandsOnCooldown.TryAdd(command.Name, DateTime.Now.AddSeconds(durationInSeconds).Ticks))
                await _Server.LogHandler.Log(new LogMessage(LogSeverity.Verbose, nameof(AddToCooldown), $"Added '{command.Name}' to Cooldown."));
            else
                await _Server.LogHandler.Log(new LogMessage(LogSeverity.Warning, nameof(AddToCooldown), $"Failed adding '{command.Name}' to Cooldown."));
        }
        public async Task AddToCooldown(CustomCommandCategory category, float durationInSeconds)
        {
            if (category != null && _categoriesOnCooldown.TryAdd(category.Name, DateTime.Now.AddSeconds(durationInSeconds).Ticks))
                await _Server.LogHandler.Log(new LogMessage(LogSeverity.Verbose, nameof(AddToCooldown), $"Added '{category.Name}' to Cooldown."));
            else
                await _Server.LogHandler.Log(new LogMessage(LogSeverity.Warning, nameof(AddToCooldown), $"Failed adding '{category.Name}' to Cooldown."));
        }

    }
}
