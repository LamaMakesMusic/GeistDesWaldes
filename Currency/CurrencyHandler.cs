using Discord;
using GeistDesWaldes.Attributes;
using GeistDesWaldes.Dictionaries;
using GeistDesWaldes.Misc;
using GeistDesWaldes.Users;
using System;
using System.Threading.Tasks;

namespace GeistDesWaldes.Currency
{
    public class CurrencyHandler : BaseHandler
    {
        public CurrencyCustomization CustomizationData;

        private const string CUSTOMIZATIONDATA_FILE_NAME = "CurrencyCustomization";

        
        public CurrencyHandler(Server server) : base(server)
        {
            CustomizationData = new CurrencyCustomization();
        }

        internal override void OnServerStart(object source, EventArgs e)
        {
            base.OnServerStart(source, e);

            InitializeCurrencyHandler().SafeAsync<CurrencyHandler>(_Server.LogHandler);
        }

        private async Task InitializeCurrencyHandler()
        {
            await GenericXmlSerializer.EnsurePathExistance(_Server.LogHandler, _Server.ServerFilesDirectoryPath, CUSTOMIZATIONDATA_FILE_NAME, CustomizationData);
            await LoadCurrencyCustomizationFromFile();
        }

        public async Task<CustomRuntimeResult> AddCurrencyToUser(IUser user, int amount)
        {
            try
            {
                var getUserResult = await _Server.ForestUserHandler.GetUser(user);

                if (getUserResult.IsSuccess)
                    return await AddCurrencyToUser(getUserResult.ResultValue, amount);

                return getUserResult;
            }
            catch (Exception e)
            {
                return CustomRuntimeResult.FromError(e.ToString());
            }
        }
        public async Task<CustomRuntimeResult> AddCurrencyToUser(ForestUser forestUser, int amount)
        {
            try
            {
                forestUser.AddToWallet(amount);

                await _Server.LogHandler.Log(new LogMessage(LogSeverity.Verbose, nameof(AddCurrencyToUser), $"Added {amount} to wallet of {forestUser.Name}!"));

                return CustomRuntimeResult.FromSuccess();
            }
            catch (Exception e)
            {
                return CustomRuntimeResult.FromError(e.ToString());
            }
        }

        public Task<CustomRuntimeResult> TransferCurrencyBetweenUsers(ForestUser sender, ForestUser receiver, int amount)
        {
            return Task.Run(() =>
            {
                try
                {
                    lock (sender)
                    {
                        lock (receiver)
                        {
                            if (sender.ForestUserId == receiver.ForestUserId)
                                return CustomRuntimeResult.FromError(ReplyDictionary.USER_CAN_NOT_BE_YOURSELF);

                            if (!sender.CanAfford(amount))
                                return CustomRuntimeResult.FromError(CustomizationData.NotEnoughPointsMessage);

                            sender.AddToWallet(-amount);
                            receiver.AddToWallet(amount);
                        }
                    }

                    return CustomRuntimeResult.FromSuccess();
                }
                catch (Exception e)
                {
                    return CustomRuntimeResult.FromError(e.ToString());
                }
            });
        }

        public async Task<CustomRuntimeResult<int>> GetPointsAsync(string twitchId)
        {
            var userResult = await _Server.ForestUserHandler.GetUser(twitchId: twitchId);

            if (userResult.IsSuccess)
                return CustomRuntimeResult<int>.FromSuccess(value: userResult.ResultValue.Wallet);

            return CustomRuntimeResult<int>.FromError(userResult.Reason);
        }
        public async Task<CustomRuntimeResult<int>> GetPointsAsync(IUser user)
        {
            var userResult = await _Server.ForestUserHandler.GetUser(user);

            if (userResult.IsSuccess)
                return CustomRuntimeResult<int>.FromSuccess(value: userResult.ResultValue.Wallet);

            return CustomRuntimeResult<int>.FromError(userResult.Reason);
        }


        public async Task SaveCurrencyCustomizationToFile()
        {
            await GenericXmlSerializer.SaveAsync<CurrencyCustomization>(_Server.LogHandler, CustomizationData, CUSTOMIZATIONDATA_FILE_NAME, _Server.ServerFilesDirectoryPath);
        }
        public async Task LoadCurrencyCustomizationFromFile()
        {
            CurrencyCustomization loadedData = null;

            loadedData = await GenericXmlSerializer.LoadAsync<CurrencyCustomization>(_Server.LogHandler, CUSTOMIZATIONDATA_FILE_NAME, _Server.ServerFilesDirectoryPath);

            if (loadedData == default)
                await _Server.LogHandler.Log(new LogMessage(LogSeverity.Warning, nameof(LoadCurrencyCustomizationFromFile), $"Loaded {nameof(CustomizationData)} == DEFAULT"));
            else
                CustomizationData = loadedData;
        }

    }
}
