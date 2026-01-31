using System;
using System.Threading.Tasks;
using Discord;
using GeistDesWaldes.Attributes;
using GeistDesWaldes.Dictionaries;
using GeistDesWaldes.Misc;
using GeistDesWaldes.Users;

namespace GeistDesWaldes.Currency;

public class CurrencyHandler : BaseHandler
{
    public override int Priority => -11;
    
    private const string CUSTOMIZATIONDATA_FILE_NAME = "CurrencyCustomization";

    private readonly ForestUserHandler _userHandler;
    public CurrencyCustomization CustomizationData = new();


    public CurrencyHandler(Server server, ForestUserHandler userHandler) : base(server)
    {
        _userHandler = userHandler;
    }

    public override async Task OnServerStartUp()
    {
        await base.OnServerStartUp();
        await InitializeCurrencyHandler();
    }

    private async Task InitializeCurrencyHandler()
    {
        await GenericXmlSerializer.EnsurePathExistance(Server.LogHandler, Server.ServerFilesDirectoryPath, CUSTOMIZATIONDATA_FILE_NAME, CustomizationData);
        await LoadCurrencyCustomizationFromFile();
    }

    public async Task<CustomRuntimeResult> AddCurrencyToUser(IUser user, int amount)
    {
        try
        {
            CustomRuntimeResult<ForestUser> getUserResult = await _userHandler.GetUser(user);

            if (getUserResult.IsSuccess)
            {
                return await AddCurrencyToUser(getUserResult.ResultValue, amount);
            }

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

            await Server.LogHandler.Log(new LogMessage(LogSeverity.Verbose, nameof(AddCurrencyToUser), $"Added {amount} to wallet of {forestUser.Name}!"));

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
                        {
                            return CustomRuntimeResult.FromError(ReplyDictionary.USER_CAN_NOT_BE_YOURSELF);
                        }

                        if (!sender.CanAfford(amount))
                        {
                            return CustomRuntimeResult.FromError(CustomizationData.NotEnoughPointsMessage);
                        }

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
        CustomRuntimeResult<ForestUser> userResult = await _userHandler.GetUser(twitchId: twitchId);

        if (userResult.IsSuccess)
        {
            return CustomRuntimeResult<int>.FromSuccess(value: userResult.ResultValue.Wallet);
        }

        return CustomRuntimeResult<int>.FromError(userResult.Reason);
    }

    public async Task<CustomRuntimeResult<int>> GetPointsAsync(IUser user)
    {
        CustomRuntimeResult<ForestUser> userResult = await _userHandler.GetUser(user);

        if (userResult.IsSuccess)
        {
            return CustomRuntimeResult<int>.FromSuccess(value: userResult.ResultValue.Wallet);
        }

        return CustomRuntimeResult<int>.FromError(userResult.Reason);
    }


    public async Task SaveCurrencyCustomizationToFile()
    {
        await GenericXmlSerializer.SaveAsync<CurrencyCustomization>(Server.LogHandler, CustomizationData, CUSTOMIZATIONDATA_FILE_NAME, Server.ServerFilesDirectoryPath);
    }

    public async Task LoadCurrencyCustomizationFromFile()
    {
        CurrencyCustomization loadedData;

        loadedData = await GenericXmlSerializer.LoadAsync<CurrencyCustomization>(Server.LogHandler, CUSTOMIZATIONDATA_FILE_NAME, Server.ServerFilesDirectoryPath);

        if (loadedData == default)
        {
            await Server.LogHandler.Log(new LogMessage(LogSeverity.Warning, nameof(LoadCurrencyCustomizationFromFile), $"Loaded {nameof(CustomizationData)} == DEFAULT"));
        }
        else
        {
            CustomizationData = loadedData;
        }
    }
}