using Discord;
using GeistDesWaldes.Attributes;
using GeistDesWaldes.Dictionaries;
using GeistDesWaldes.Misc;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace GeistDesWaldes.Decoration
{
    public class LayoutTemplateHandler : BaseHandler
    {
        public LayoutTemplateDictionary TemplateDictionary;

        private const string LAYOUT_TEMPLATES_FILE_NAME = "LayoutTemplates";
        private bool _inProgress = false;


        public LayoutTemplateHandler(Server server) : base(server)
        {
            TemplateDictionary = new LayoutTemplateDictionary();
        }
        
        internal override void OnServerStart(object source, EventArgs e)
        {
            base.OnServerStart(source, e);

            InitializeLayoutTemplateHandler().SafeAsync<LayoutTemplateHandler>(_Server.LogHandler);
        }
        internal override void OnCheckIntegrity(object source, EventArgs e)
        {
            base.OnCheckIntegrity(source, e);

            CheckIntegrity().SafeAsync<LayoutTemplateHandler>(_Server.LogHandler);
        }

        private async Task InitializeLayoutTemplateHandler()
        {
            await GenericXmlSerializer.EnsurePathExistance(_Server.LogHandler, _Server.ServerFilesDirectoryPath, LAYOUT_TEMPLATES_FILE_NAME, TemplateDictionary);

            await LoadTemplatesFromFile();
        }
        private async Task CheckIntegrity()
        {
            bool issues = false;
            var builder = new StringBuilder();

            foreach (var template in TemplateDictionary.Templates)
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
                await _Server.LogHandler.Log(new LogMessage(LogSeverity.Warning, nameof(CheckIntegrity), builder.ToString()));
            else
                await _Server.LogHandler.Log(new LogMessage(LogSeverity.Info, nameof(CheckIntegrity), "Layout Templates OK."), (int)ConsoleColor.DarkGreen);
        }


        public async Task<CustomRuntimeResult<LayoutTemplate>> GetTemplate(string templateName)
        {
            var result = TemplateDictionary.Templates.Find(t => t.TemplateNameHash == templateName.ToLower().GetHashCode());

            if (result != null)
                return CustomRuntimeResult<LayoutTemplate>.FromSuccess(value: result);

            return CustomRuntimeResult<LayoutTemplate>.FromError(await ReplyDictionary.ReplaceStringInvariantCase(ReplyDictionary.TEMPLATE_NAMED_X_DOES_NOT_EXISTS, "{x}", templateName));
        }
        public async Task<CustomRuntimeResult> AddTemplate(LayoutTemplate template)
        {
            try
            {
                if (template == null)
                    throw new Exception(ReplyDictionary.PARAMETER_MUST_NOT_BE_EMPTY);

                if ((await GetTemplate(template.TemplateName)).IsSuccess)
                    return CustomRuntimeResult.FromError(await ReplyDictionary.ReplaceStringInvariantCase(ReplyDictionary.TEMPLATE_NAMED_X_ALREADY_EXISTS, "{x}", template.TemplateName));

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
                var getResult = await GetTemplate(templateName);

                if (getResult.IsSuccess)
                {
                    if (revertIfActive && IsActiveLayout(getResult.ResultValue))
                    {
                        var revertResult = await RevertActiveTemplate();

                        if (!revertResult.IsSuccess)
                            return revertResult;
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
                    return CustomRuntimeResult.FromError(await ReplyDictionary.ReplaceStringInvariantCase(ReplyDictionary.TEMPLATE_NAMED_X_ALREADY_EXISTS, "{x}", templateName));

                var result = new LayoutTemplate(templateName);

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
                    return CustomRuntimeResult.FromError(ReplyDictionary.SUCH_PROCESS_IS_ALREADY_ACTIVE_TRY_AGAIN_LATER);

                _inProgress = true;

                var getResult = await GetTemplate(templateName);

                if (getResult.IsSuccess)
                {
                    var revertResult = await RevertActiveTemplate(true);

                    if (revertResult.IsSuccess)
                    {
                        var applyResult = await getResult.ResultValue.ApplyAsync(_Server.LogHandler);

                        TemplateDictionary.ActiveTemplate = getResult.ResultValue.TemplateName;

                        result = applyResult;
                    }
                    else
                        result = revertResult;
                }

                result = getResult;
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
                if (ignoreActive == false)
                {
                    if (_inProgress)
                        return CustomRuntimeResult.FromError(ReplyDictionary.SUCH_PROCESS_IS_ALREADY_ACTIVE_TRY_AGAIN_LATER);

                    _inProgress = true;
                }

                if (string.IsNullOrWhiteSpace(TemplateDictionary.ActiveTemplate))
                    result = CustomRuntimeResult.FromSuccess();
                else
                {
                    var getResult = await GetTemplate(TemplateDictionary.ActiveTemplate);

                    if (getResult.IsSuccess)
                    {
                        var revertResult = await getResult.ResultValue.RevertAsync(_Server.LogHandler);

                        TemplateDictionary.ActiveTemplate = null;

                        result = revertResult;
                    }
                    else
                        result = getResult;
                }
            }
            catch (Exception e)
            {
                result = CustomRuntimeResult.FromError(e.ToString());
            }
            finally
            {
                if (ignoreActive == false)
                    _inProgress = false;
            }

            return result;
        }

        public bool IsActiveLayout(LayoutTemplate template)
        {
            return (string.IsNullOrWhiteSpace(TemplateDictionary.ActiveTemplate) == false && template.TemplateNameHash == TemplateDictionary.ActiveTemplate.ToLower().GetHashCode());
        }

        public Task SaveTemplatesToFile()
        {
            return GenericXmlSerializer.SaveAsync<LayoutTemplateDictionary>(_Server.LogHandler, TemplateDictionary, LAYOUT_TEMPLATES_FILE_NAME, _Server.ServerFilesDirectoryPath);
        }
        public async Task LoadTemplatesFromFile()
        {
            LayoutTemplateDictionary loadedDictionary = null;

            loadedDictionary = await GenericXmlSerializer.LoadAsync<LayoutTemplateDictionary>(_Server.LogHandler, LAYOUT_TEMPLATES_FILE_NAME, _Server.ServerFilesDirectoryPath);

            if (loadedDictionary == default)
                await _Server.LogHandler.Log(new LogMessage(LogSeverity.Warning, nameof(LoadTemplatesFromFile), $"Loaded {nameof(LayoutTemplateDictionary)} == DEFAULT"));
            else
                TemplateDictionary = loadedDictionary;


            // Generate Hashes
            foreach (var template in TemplateDictionary.Templates)
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
}
