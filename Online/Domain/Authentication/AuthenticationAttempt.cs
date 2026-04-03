

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using RWCustom;
using Steamworks;
using Sodium;

namespace RainMeadow
{
    [JsonObject(MemberSerialization.Fields)]
    record struct OAuthTokenResponse
    {
        public string access_token;
        public string id_token;
        public string refresh_token;
        public string token_type;
        public int expires_in;
    }
    
    [JsonObject(MemberSerialization.Fields)]
    class OAuthAccess
    {
        public string? accessToken;
        public string? refreshToken;
        public DateTime expiresIn;
    }
    
    [JsonObject(MemberSerialization.Fields)]
    record struct OAuthErrorResponse
    {
        public string error;
        public string error_description;
        public string error_uri;
    }


    static class WebAuthentication  
    {
        static readonly char[] nonce_chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890".ToCharArray(); 
        public static string GenerateNonce(int len)
        {
            var bytes = new byte[len];
            using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create()) rng.GetBytes(bytes);
            return LibSodium.BinToB64(bytes);
        }

        public static void OpenWebPage(string URI)
        {
            if (SteamManager.Initialized && SteamUtils.IsOverlayEnabled()) SteamFriends.ActivateGameOverlayToWebPage(URI);
            else System.Diagnostics.Process.Start(URI);
        }

        public static async Task<(string code, string from_redirect_uri)> Attempt(string authURI, string clientID, string scope, string responseType, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string nonce = GenerateNonce(16);
            using (HttpListener listener = new HttpListener())
            {
                Exception lastexcept = new Exception();

                const int MinPort = 49215;
                const int MaxPort = ushort.MaxValue;
                for (int port = MinPort; port < MaxPort; port++)
                {
                    listener.Prefixes.Add($"http://localhost:{port}/");
                    try
                    {
                        listener.Start();
                        break;
                    }
                    catch (Exception except)
                    {
                        lastexcept = except;
                        // nothing to do here -- the listener disposes itself when Start throws
                        listener.Prefixes.Clear();
                        continue;
                    }
                }

                if (!listener.IsListening) throw lastexcept;
                var URI = authURI + $"?response_type={responseType}&redirect_uri={Uri.EscapeDataString(listener.Prefixes.First())}&scope={Uri.EscapeDataString(scope)}&state={nonce}&client_id={clientID}&prompt=login";
                RainMeadow.Debug(URI);
                OpenWebPage(URI);

                HttpListenerContext context = await GetContextCancellable(listener, cancellationToken);
                string language = Custom.rainWorld.inGameTranslator.currentLanguage.value;
                string htmlfile = AssetManager.ResolveFilePath($"html/rtg-{language.ToString()}.html");
                if (!File.Exists(htmlfile))
                {
                    htmlfile = AssetManager.ResolveFilePath($"html/rtg-english.html");
                }

                using (HttpListenerResponse response = context.Response)
                {
                    response.ContentEncoding = System.Text.Encoding.UTF8;
                    response.ContentType = "text/html";
                    using (FileStream fs = new(htmlfile, FileMode.Open, FileAccess.Read))
                    {
                        fs.CopyTo(response.OutputStream);
                    }
                }

                if (context.Request.QueryString.Get("error") is string error) throw new Exception(error + context.Request.QueryString.Get("error"));
                string code = context.Request.QueryString.Get("code") ?? throw new Exception("Didn't Recieve Code");
                if (context.Request.QueryString.Get("state") != nonce) throw new Exception("Incorrect Nonce");
                return (code, listener.Prefixes.First());
            }
        }

        public enum GrantType
        {
            AuthorizationCode,
            RefreshToken
        }

        public static async Task<OAuthAccess> RetrieveToken(string authURI, string clientID, GrantType grantType, string scope, string authparam, string? redirect_uri, CancellationToken cancellationToken)
        {
            HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create($"{authURI}");
            request.Credentials = CredentialCache.DefaultCredentials;
            request.Method = "POST";
            request.ContentType = "application/x-www-form-urlencoded";

            (string grant_type, string grant_value) = grantType switch {
                GrantType.AuthorizationCode => ("authorization_code", "code"),
                GrantType.RefreshToken => ("refresh_token", "refresh_token"),
                _ => throw new Exception("Invalid grant type") 
            };
            string postData = $"grant_type={Uri.EscapeDataString(grant_type)}&client_id={Uri.EscapeDataString(clientID)}&{Uri.EscapeDataString(grant_value)}={Uri.EscapeDataString(authparam)}"; 
            if (grantType != GrantType.RefreshToken)
            {
                postData += $"&scope={Uri.EscapeDataString(scope)}";
            }
            if (redirect_uri is not null) postData += $"&redirect_uri={Uri.EscapeDataString(redirect_uri)}";
            var data = System.Text.Encoding.ASCII.GetBytes(postData);
            request.ContentLength = data.Length;
            request.Accept = "application/json";
            using (Stream stream = request.GetRequestStream()) stream.Write(data, 0, data.Length);

            using (HttpWebResponse response = await GetResponseNoException(request, cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (response.StatusCode != HttpStatusCode.OK && response.StatusCode != HttpStatusCode.BadRequest) throw new Exception($"Authentication Error: {(int)response.StatusCode} {response.StatusCode} {response.StatusDescription}");
                using (StreamReader reader = new(response.GetResponseStream()))
                using (JsonTextReader jsonreader = new(reader))
                {
                    var serializer = new JsonSerializer();
                    if (response.StatusCode == HttpStatusCode.OK)
                    {        
                        var token_response = serializer.Deserialize<OAuthTokenResponse>(jsonreader);
                        RainMeadow.Debug(token_response);
                        return new OAuthAccess() { 
                            accessToken = token_response.access_token,
                            refreshToken = token_response.refresh_token,
                            expiresIn = DateTime.Now.AddSeconds(token_response.expires_in)
                        };
                    }
                    else
                    {
                        var error_response = serializer.Deserialize<OAuthErrorResponse>(jsonreader);
                        throw new Exception($"Authentication Error: {error_response.error} {error_response.error_description}") { HelpLink = error_response.error_uri }; 
                    }
                }
            }
        }

        // https://stackoverflow.com/questions/10081726/why-does-httpwebrequest-throw-an-exception-instead-returning-httpstatuscode-notf
        public static async Task<HttpListenerContext> GetContextCancellable(HttpListener listener, CancellationToken token)
        {
            IAsyncResult result = listener.BeginGetContext(new AsyncCallback(delegate {}), null);
            while (!token.IsCancellationRequested && !result.IsCompleted) await Task.Yield();
            if (!result.IsCompleted) token.ThrowIfCancellationRequested();
            return listener.EndGetContext(result);
        }

        // https://stackoverflow.com/questions/10081726/why-does-httpwebrequest-throw-an-exception-instead-returning-httpstatuscode-notf
        public static async Task<HttpWebResponse> GetResponseNoException(HttpWebRequest req, CancellationToken token)
        {
            try
            {
                IAsyncResult result = req.BeginGetResponse(new AsyncCallback(delegate {}), null);
                while (!token.IsCancellationRequested && !result.IsCompleted) await Task.Yield();
                if (!result.IsCompleted) token.ThrowIfCancellationRequested();
                return (HttpWebResponse)req.EndGetResponse(result);
            }
            catch (WebException we)
            {
                var resp = we.Response as HttpWebResponse;
                if (resp == null)
                    throw;
                return resp;
            }
        }
    }
}
