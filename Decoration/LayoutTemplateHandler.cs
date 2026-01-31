using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Discord;
using GeistDesWaldes.Attributes;
using GeistDesWaldes.Dictionaries;
using GeistDesWaldes.Misc;

namespace GeistDesWaldes.Decoration;

public class LayoutTemplateHandler : BaseHandler
{
    public override int Priority => -8;
    
    private const string LAYOUT_TEMPLATES_FILE_NAME = "LayoutTemplates";
    private bool _inProgress;
    public LayoutTemplateDictionary TemplateDictionary;


    public LayoutTemplateHandler(Server server) : base(server)
    {
        TemplateDictionary = new LayoutTemplateDictionary();
    }

    public override async Task OnServerStartUp()
    {
        await base.OnServerStartUp();

        await InitializeLayoutTemplateHandler();
    }

    public override async Task OnCheckIntegrity()
    {
        await base.OnCheckIntegrity();

        await CheckIntegrity();
    }

    private async Task InitializeLayoutTemplateHandler()
    {
        await GenericXmlSerializer.EnsurePathExistance(Server.LogHandler, Server.ServerFilesDirectoryPath, LAYOUT_TEMPLATES_FILE_NAME, TemplateDictionary);

        await LoadTemplatesFromFile();
    }

    private async Task CheckIntegrity()
    {
        bool issues = false;
        StringBuilder builder = new();

        foreach (LayoutTemplate template in TemplateDictionary.Templates)
        {
            CustomRuntimeResult checkResult = await template.PerformSelfCheck();

            if (!checkResult.IsSuccess)
            {
                issues = true;
                builder.AppendLine($"...{template.TemplateName}");
                builder.AppendLine(checkResult.Reason);
            }
        }

        if (issues)
        {
            await Server.LogHandler.Log(new LogMessage(LogSeverity.Warning, nameof(CheckIntegrity), builder.ToString()));
        }
        else
        {
            await Server.LogHandler.Log(new LogMessage(LogSeverity.Info, nameof(CheckIntegrity), "Layout Templates OK."), (int)ConsoleColor.DarkGreen);
        }
    }


    public async Task<CustomRuntimeResult<LayoutTemplate>> GetTemplate(string templateName)
    {
        LayoutTemplate result = TemplateDictionary.Templates.Find(t => t.TemplateNameHash == templateName.ToLower().GetHashCode());

        if (result != null)
        {
            return CustomRuntimeResult<LayoutTemplate>.FromSuccess(value: result);
        }

        return CustomRuntimeResult<LayoutTemplate>.FromError(await ReplyDictionary.ReplaceStringInvariantCase(ReplyDictionary.TEMPLATE_NAMED_X_DOES_NOT_EXISTS, "{x}", templateName));
    }

    public async Task<CustomRuntimeResult> AddTemplate(LayoutTemplate template)
    {
        try
        {
            if (template == null)
            {
                throw new Exception(ReplyDictionary.PARAMETER_MUST_NOT_BE_EMPTY);
            }

            if ((await GetTemplate(template.TemplateName)).IsSuccess)
            {
                return CustomRuntimeResult.FromError(await ReplyDictionary.ReplaceStringInvariantCase(ReplyDictionary.TEMPLATE_NAMED_X_ALREADY_EXISTS, "{x}", template.TemplateName));
            }

            TemplateDictionary.Templates.Add(template);

            return CustomRuntimeResult.FromSuccess();
        }
        catch (Exception e)
        {
            return CustomRuntimeResult.FromError(e.ToString());
        }
    }

    public async Task<CustomRuntimeResult> RemoveTemplate(string templateName, bool revertIfActive = true)
    {
        try
        {
            CustomRuntimeResult<LayoutTemplate> getResult = await GetTemplate(templateName);

            if (getResult.IsSuccess)
            {
                if (revertIfActive && IsActiveLayout(getResult.ResultValue))
                {
                    CustomRuntimeResult revertResult = await RevertActiveTemplate();

                    if (!revertResult.IsSuccess)
                    {
                        return revertResult;
                    }
                }

                TemplateDictionary.Templates.Remove(getResult.ResultValue);
                return CustomRuntimeResult.FromSuccess();
            }

            return getResult;
        }
        catch (Exception e)
        {
            return CustomRuntimeResult.FromError(e.ToString());
        }
    }

    public async Task<CustomRuntimeResult> CreateTemplate(string templateName)
    {
        try
        {
            if ((await GetTemplate(templateName)).IsSuccess)
            {
                return CustomRuntimeResult.FromError(await ReplyDictionary.ReplaceStringInvariantCase(ReplyDictionary.TEMPLATE_NAMED_X_ALREADY_EXISTS, "{x}", templateName));
            }

            LayoutTemplate result = new(templateName);

            return await AddTemplate(result);
        }
        catch (Exception e)
        {
            return CustomRuntimeResult.FromError(e.ToString());
        }
    }


    public async Task<CustomRuntimeResult> ApplyTemplate(string templateName)
    {
        CustomRuntimeResult result;

        try
        {
            if (_inProgress)
            {
                return CustomRuntimeResult.FromError(ReplyDictionary.SUCH_PROCESS_IS_ALREADY_ACTIVE_TRY_AGAIN_LATER);
            }

            _inProgress = true;

            CustomRuntimeResult<LayoutTemplate> getResult = await GetTemplate(templateName);

            if (getResult.IsSuccess)
            {
                CustomRuntimeResult revertResult = await RevertActiveTemplate(true);

                if (revertResult.IsSuccess)
                {
                    CustomRuntimeResult applyResult = await getResult.ResultValue.ApplyAsync(Server.LogHandler);

                    TemplateDictionary.ActiveTemplate = getResult.ResultValue.TemplateName;

                    result = applyResult;
                }
                else
                {
                    result = revertResult;
                }
            }
            else
            {
                result = getResult;
            }
        }
        catch (Exception e)
        {
            result = CustomRuntimeResult.FromError(e.ToString());
        }
        finally
        {
            _inProgress = false;
        }

        return result;
    }

    public async Task<CustomRuntimeResult> RevertActiveTemplate(bool ignoreActive = false)
    {
        CustomRuntimeResult result;
        try
        {
            if (!ignoreActive)
            {
                if (_inProgress)
                {
                    return CustomRuntimeResult.FromError(ReplyDictionary.SUCH_PROCESS_IS_ALREADY_ACTIVE_TRY_AGAIN_LATER);
                }

                _inProgress = true;
            }

            if (string.IsNullOrWhiteSpace(TemplateDictionary.ActiveTemplate))
            {
                result = CustomRuntimeResult.FromSuccess();
            }
            else
            {
                CustomRuntimeResult<LayoutTemplate> getResult = await GetTemplate(TemplateDictionary.ActiveTemplate);

                if (getResult.IsSuccess)
                {
                    CustomRuntimeResult revertResult = await getResult.ResultValue.RevertAsync(Server.LogHandler);

                    TemplateDictionary.ActiveTemplate = null;

                    result = revertResult;
                }
                else
                {
                    result = getResult;
                }
            }
        }
        catch (Exception e)
        {
            result = CustomRuntimeResult.FromError(e.ToString());
        }
        finally
        {
            if (!ignoreActive)
            {
                _inProgress = false;
            }
        }

        return result;
    }

    public bool IsActiveLayout(LayoutTemplate template)
    {
        return !string.IsNullOrWhiteSpace(TemplateDictionary.ActiveTemplate) && template.TemplateNameHash == TemplateDictionary.ActiveTemplate.ToLower().GetHashCode();
    }

    public Task SaveTemplatesToFile()
    {
        return GenericXmlSerializer.SaveAsync<LayoutTemplateDictionary>(Server.LogHandler, TemplateDictionary, LAYOUT_TEMPLATES_FILE_NAME, Server.ServerFilesDirectoryPath);
    }

    public async Task LoadTemplatesFromFile()
    {
        LayoutTemplateDictionary loadedDictionary = await GenericXmlSerializer.LoadAsync<LayoutTemplateDictionary>(Server.LogHandler, LAYOUT_TEMPLATES_FILE_NAME, Server.ServerFilesDirectoryPath);

        if (loadedDictionary == null)
        {
            await Server.LogHandler.Log(new LogMessage(LogSeverity.Warning, nameof(LoadTemplatesFromFile), $"Loaded {nameof(LayoutTemplateDictionary)} == DEFAULT"));
        }
        else
        {
            TemplateDictionary = loadedDictionary;
        }


        // Generate Hashes
        foreach (LayoutTemplate template in TemplateDictionary.Templates)
        {
            template.SetName(template.TemplateName);
            template.RefreshChannelLayoutReference();
            template.EnsureFormat();
        }
    }
}

[Serializable]
public class LayoutTemplateDictionary
{
    public string ActiveTemplate;
    public List<LayoutTemplate> Templates;

    public LayoutTemplateDictionary()
    {
        ActiveTemplate = string.Empty;
        Templates = new List<LayoutTemplate>();
    }
}