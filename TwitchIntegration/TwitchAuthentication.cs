using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace GeistDesWaldes.TwitchIntegration
{
    public class TwitchAuthentication
    {
        public static HttpClient AuthorizationClient = new HttpClient();
        public static Random RandomInstance = new Random();

        public static EventHandler<LogEventArgs> OnLog;

        public static async Task<ValidationResult> ValidateBotUserAuthentication(string scope, string oAuthToken, string oAuthRefreshToken, string clientId, string clientSecret, string redirectURL, bool allowTokenRequest = true)
        {
            // https://dev.twitch.tv/docs/authentication
            try
            {
                HttpResponseMessage response;
                var parameters = new List<KeyValuePair<string, string>>();

                #region Check existing Token
                if (string.IsNullOrWhiteSpace(oAuthToken))
                {
                    response = new HttpResponseMessage(HttpStatusCode.Unauthorized) { ReasonPhrase = "OAuth Token is null or whitespace!" };
                }
                else
                {
                    using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, new Uri(@"https://id.twitch.tv/oauth2/validate")))
                    {
                        request.Headers.TryAddWithoutValidation("Authorization", $"OAuth {oAuthToken}");

                        response = await AuthorizationClient.SendAsync(request);
                    }
                }
                #endregion

                if (response.IsSuccessStatusCode)
                {
                    Log(LogEventArgs.LogSeverity.Info, nameof(ValidateBotUserAuthentication), $"Authentification: Successful! {response.StatusCode} | {response.ReasonPhrase}", color: (int)ConsoleColor.Green);
                    return new ValidationResult(true, oAuthToken, oAuthRefreshToken);
                }
                else
                {
                    Log(LogEventArgs.LogSeverity.Warning, nameof(ValidateBotUserAuthentication), $"Authentification: Failed! {response.StatusCode} | {response.ReasonPhrase}");
                }


                #region Try refreshing Token
                if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
                {
                    response = new HttpResponseMessage(HttpStatusCode.NotFound) { ReasonPhrase = "ClientId or ClientSecret is null or whitespace!" };
                    throw new Exception($"Refreshing Token: Failed! {response.StatusCode} | {response.ReasonPhrase}");
                }
                else if (string.IsNullOrWhiteSpace(oAuthRefreshToken))
                {
                    response = new HttpResponseMessage(HttpStatusCode.NotFound) { ReasonPhrase = "OAuth Refresh Token is null or whitespace!" };
                }
                else
                {
                    parameters = new List<KeyValuePair<string, string>>()
                    {
                        new KeyValuePair<string, string>("grant_type", "refresh_token"),
                        new KeyValuePair<string, string>("refresh_token", oAuthRefreshToken),
                        new KeyValuePair<string, string>("client_id", clientId),
                        new KeyValuePair<string, string>("client_secret", clientSecret)
                    };


                    response = await AuthorizationClient.PostAsync(new Uri(@"https://id.twitch.tv/oauth2/token"), new FormUrlEncodedContent(parameters));
                }

                if (response.IsSuccessStatusCode)
                {
                    if (await RetrieveTokensFromResponse(response) is string[] tokens && tokens != null)
                    {
                        Log(LogEventArgs.LogSeverity.Info, nameof(ValidateBotUserAuthentication), $"Refreshing Token: Successful! {response.StatusCode} | {response.ReasonPhrase}", color: (int)ConsoleColor.Green);

                        return new ValidationResult(true, tokens[0], tokens[1], true);
                    }

                }
                else
                {
                    Log(LogEventArgs.LogSeverity.Warning, nameof(ValidateBotUserAuthentication), $"Refreshing Token: Failed! {response.StatusCode} | {response.ReasonPhrase}");
                }
                #endregion


                if (allowTokenRequest == false)
                    throw new Exception("Refreshing Token: Failed! - Not allowed to request new Token.");


                #region Try requesting new Token
                Log(LogEventArgs.LogSeverity.Verbose, nameof(ValidateBotUserAuthentication), $"Requesting token with scope: {scope}");

                if (string.IsNullOrWhiteSpace(redirectURL))
                {
                    response = new HttpResponseMessage(HttpStatusCode.NotFound) { ReasonPhrase = "Redirect URL is null or whitespace!" };
                }
                else
                {
                    // Request User Authentication
                    string generatedState = $"{clientId.GetHashCode()}{RandomInstance.Next()}";

                    string URL = @"https://id.twitch.tv/oauth2/authorize" +
                                    "?response_type=code" +
                                    $"&client_id={clientId}" +
                                    $"&redirect_uri={redirectURL}" +
                                    $"&scope={scope}" +
                                    $"&state={generatedState}";

                    response = await RequestUserAuthenticationAsync(URL, generatedState, redirectURL);

                    if (response.IsSuccessStatusCode)
                    {
                        Log(LogEventArgs.LogSeverity.Info, nameof(ValidateBotUserAuthentication), $"Requesting User Authentification: Successful! {response.StatusCode}");

                        // Trade Authorization Code for Access Token
                        parameters = new List<KeyValuePair<string, string>>()
                        {
                            new KeyValuePair<string, string>("client_id", clientId),
                            new KeyValuePair<string, string>("client_secret", clientSecret),
                            new KeyValuePair<string, string>("code", response.ReasonPhrase),
                            new KeyValuePair<string, string>("grant_type", "authorization_code"),
                            new KeyValuePair<string, string>("redirect_uri", redirectURL)
                        };

                        response = await AuthorizationClient.PostAsync(new Uri(@"https://id.twitch.tv/oauth2/token"), new FormUrlEncodedContent(parameters));


                        if (response.IsSuccessStatusCode && await RetrieveTokensFromResponse(response) is string[] tokens && tokens != null)
                        {
                            Log(LogEventArgs.LogSeverity.Info, nameof(ValidateBotUserAuthentication), $"Requesting Token: Successful! {response.StatusCode} | {response.ReasonPhrase}", color: (int)ConsoleColor.Green);

                            return new ValidationResult(true, tokens[0], tokens[1], true);
                        }
                        else
                            throw new Exception($"Requesting Token: Failed! {response.StatusCode} | {response.ReasonPhrase}");
                    }
                    else
                        throw new Exception($"Requesting User Authentification: Failed! {response.StatusCode} | {response.ReasonPhrase}");
                }
                #endregion
            }
            catch (Exception e)
            {
                Log(LogEventArgs.LogSeverity.Error, nameof(ValidateBotUserAuthentication), "", e);
            }

            return new ValidationResult(false, oAuthToken, oAuthRefreshToken);
        }

        public static async Task<HttpResponseMessage> RevokeBotUserAuthentification(string token, string clientId)
        {
            HttpResponseMessage response;

            if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(clientId))
            {
                response = new HttpResponseMessage(HttpStatusCode.BadRequest) { ReasonPhrase = "ClientId or Token is empty!" };

                Log(LogEventArgs.LogSeverity.Error, nameof(RevokeBotUserAuthentification), response.ReasonPhrase);
            }
            else
            {
                var parameters = new List<KeyValuePair<string, string>>()
                {
                    new KeyValuePair<string, string>("client_id", clientId),
                    new KeyValuePair<string, string>("token", token)
                };


                response = await AuthorizationClient.PostAsync(new Uri(@"https://id.twitch.tv/oauth2/revoke"), new FormUrlEncodedContent(parameters));
            }


            return response;
        }


        private static async Task<string[]> RetrieveTokensFromResponse(HttpResponseMessage response)
        {
            string[] responseContent = (await response.Content?.ReadAsStringAsync())?.Trim()?.Trim('{', '}')?.Split(",");

            string accessTokenIdentifier = "\"access_token\":";
            string refreshTokenIdentifier = "\"refresh_token\":";
            string expireTimeIdentifier = "\"expires_in\":";

            string accessToken = responseContent.FirstOrDefault(p => p.StartsWith(accessTokenIdentifier))?.Remove(0, accessTokenIdentifier.Length)?.Trim('\"');
            string refreshToken = responseContent.FirstOrDefault(p => p.StartsWith(refreshTokenIdentifier))?.Remove(0, refreshTokenIdentifier.Length)?.Trim('\"');
            string expirationTime = responseContent.FirstOrDefault(p => p.StartsWith(expireTimeIdentifier))?.Remove(0, expireTimeIdentifier.Length)?.Trim('\"');

            string error = string.Empty;

            if (string.IsNullOrWhiteSpace(accessToken))
                error = "Retrieved OAuth Token is null or whitespace!";
            if (string.IsNullOrWhiteSpace(refreshToken))
                error = $"{error} | Retrieved Refresh Token is null or whitespace!";
            //if (string.IsNullOrWhiteSpace(tokenResult[2]))
            //    error = $"{error} | Retrieved Expiration Time is null or whitespace!";

            if (error.Length == 0)
            {
                Log(LogEventArgs.LogSeverity.Info, nameof(RetrieveTokensFromResponse), $"Retrieved Token expires in {expirationTime} seconds!");

                return new string[] { accessToken, refreshToken, expirationTime };
            }

            Log(LogEventArgs.LogSeverity.Error, nameof(RetrieveTokensFromResponse), error);
            return null;
        }
        private static async Task<HttpResponseMessage> RequestUserAuthenticationAsync(string url, string generatedState, string redirectURL)
        {
            Process browserProcess = null;

            #region Open Web Browser
            // https://brockallen.com/2016/09/24/process-start-for-urls-on-net-core/
            try
            {
                browserProcess = Process.Start(url);
            }
            catch (Exception e)
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    url = url.Replace("&", "^&");
                    browserProcess = Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    browserProcess = Process.Start("xdg-open", url);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    browserProcess = Process.Start("open", url);
                }
                else
                {
                    Log(LogEventArgs.LogSeverity.Error, nameof(RequestUserAuthenticationAsync), "Could not open Web Browser!", e);

                    return new HttpResponseMessage(HttpStatusCode.InternalServerError) { ReasonPhrase = e.GetType().ToString() };
                }
            }

            await Task.Delay(1000);

            if (browserProcess == null || (browserProcess.HasExited && browserProcess.ExitCode != 0))
                return new HttpResponseMessage(HttpStatusCode.InternalServerError) { ReasonPhrase = "Process is null or exited with error!" };
            #endregion


            return await WaitForRedirectedUser(generatedState, redirectURL);
        }
        private static async Task<HttpResponseMessage> WaitForRedirectedUser(string expectedState, string redirectURL)
        {
            string prefix = redirectURL;

            using (HttpListener listener = new HttpListener() { Prefixes = { prefix } })
            {
                try
                {
                    listener.Start();

                    int timeout = 20;

                    while (listener.IsListening)
                    {
                        if (timeout < 1)
                            break;
                        timeout--;

                        var asyncResult = listener.BeginGetContext(new AsyncCallback((IAsyncResult) => { }), listener);
                        asyncResult.AsyncWaitHandle.WaitOne(5000);

                        HttpListenerContext context = ((HttpListener)asyncResult.AsyncState)?.EndGetContext(asyncResult);

                        #region check received data
                        if (context == null || context.Request == null)
                            continue;

                        Log(LogEventArgs.LogSeverity.Verbose, nameof(WaitForRedirectedUser), $"Received Request: [Local: {context.Request.IsLocal}] | [URL: {context.Request.Url}]");

                        if (context.Request.IsLocal == false)
                            continue;

                        string[] parameters = context.Request.Url.ToString()?.Replace(prefix, "")?.Split('?', StringSplitOptions.RemoveEmptyEntries);

                        parameters = parameters?.Length > 0 ? parameters[0].Split('&', StringSplitOptions.RemoveEmptyEntries) : null;

                        if (parameters == null)
                            continue;

                        if (parameters.Contains($"state={expectedState}"))
                        {
                            Log(LogEventArgs.LogSeverity.Verbose, nameof(WaitForRedirectedUser), "Found expected State!");

                            string code = string.Empty;
                            string errors = string.Empty;

                            for (int i = 0; i < parameters.Length; i++)
                            {
                                if (parameters[i].IndexOf("error=") is int errorIdx && errorIdx > -1)
                                    errors = $"{errors} | {parameters[i].Substring(errorIdx)}";
                                else if (parameters[i].IndexOf("code=") is int codeIdx && codeIdx > -1)
                                    code = parameters[i].Substring(codeIdx + "code=".Length);
                            }

                            if (string.IsNullOrWhiteSpace(code) == false)
                                return new HttpResponseMessage(HttpStatusCode.OK) { ReasonPhrase = code };

                            if (string.IsNullOrWhiteSpace(errors) == false)
                                return new HttpResponseMessage(HttpStatusCode.Unauthorized) { ReasonPhrase = errors };

                            return new HttpResponseMessage(HttpStatusCode.Unauthorized) { ReasonPhrase = $"Could not get authorization code from parameters! {context.Request.Url}" };
                        }
                        #endregion

                    }

                    return new HttpResponseMessage(HttpStatusCode.RequestTimeout) { ReasonPhrase = "Timeout!" };
                }
                catch (Exception e)
                {
                    Log(LogEventArgs.LogSeverity.Error, nameof(WaitForRedirectedUser), $"Failed listening to: {prefix}!", e);
                    return new HttpResponseMessage(HttpStatusCode.InternalServerError) { ReasonPhrase = "Could not start listener!" };
                }
            }
        }


        private static void Log(LogEventArgs.LogSeverity severity, string source, string message, Exception exception = null, int color = -1)
        {
            // Make a temporary copy of the event to avoid possibility of
            // a race condition if the last subscriber unsubscribes
            // immediately after the null check and before the event is raised.
            EventHandler<LogEventArgs> raiseEvent = OnLog;

            // Event will be null if there are no subscribers
            if (raiseEvent != null)
            {
                var log = new LogEventArgs(severity, source, message, exception, color);

                // Call to raise the event.
                raiseEvent(null, log);
            }
        }

    }

    public class ValidationResult
    {
        public bool Successful;
        public bool TokensUpdated;

        public readonly string OAuthToken;
        public readonly string OAuthRefreshToken;

        public ValidationResult()
        {

        }
        public ValidationResult(bool success, string oauth, string oauthRefresh, bool tokensUpdated = false)
        {
            Successful = success;
            OAuthToken = oauth;
            OAuthRefreshToken = oauthRefresh;
            TokensUpdated = tokensUpdated;
        }
    }

    public class LogEventArgs : EventArgs
    {
        public enum LogSeverity
        {
            Critical = 0,
            Error = 1,
            Warning = 2,
            Info = 3,
            Verbose = 4,
            Debug = 5
        }

        public LogSeverity Severity { get; set; }
        public string Source { get; set; }
        public string Message { get; set; }
        public Exception Exception { get; set; }
        public int Color { get; set; }

        public LogEventArgs(LogSeverity severity, string source, string message, Exception exception = null, int color = -1)
        {
            Severity = severity;
            Source = source;
            Message = message;
            Exception = exception;
            Color = color;
        }
    }

}
