using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using RainMeadow.Shared.Models;
using System.Security.Cryptography;
using System.Text;
namespace RainMeadow
{
    class Authentication
    {
        public const string authLoginEndpoint = "https://us-east-17k8wphcnn.auth.us-east-1.amazoncognito.com/login";
        public const string authTokenEndpoint = "https://us-east-17k8wphcnn.auth.us-east-1.amazoncognito.com/oauth2/token";
        public const string userInfoEndpoint = "https://us-east-17k8wphcnn.auth.us-east-1.amazoncognito.com/oauth2/userInfo";
        public const string revokendpoint = "https://us-east-17k8wphcnn.auth.us-east-1.amazoncognito.com/oauth2/revoke";
        public const string scope = "aws.cognito.signin.user.admin openid";
        public const string clientId = "7au3v8q6q1mnikd0ja5dk8a7m";
        public static async Task<Authentication> LoginFromWebView(TimeSpan expiresIn, IProgress<string> progress, CancellationToken cancellationtoken)
        {
            string code, from_redirect_uri;
            using (CancellationTokenSource ct = CancellationTokenSource.CreateLinkedTokenSource(cancellationtoken))
            {
                await Task.Yield();
                DateTime timeout = DateTime.Now.Add(expiresIn);
                Task<(string code, string from_redirect_uri)> webAuth = WebAuthentication.Attempt(authLoginEndpoint, clientId, scope, "code", ct.Token);
                if (expiresIn == Timeout.InfiniteTimeSpan)
                {
                    progress.Report($"Continue in the browser.");
                    webAuth.Wait(ct.Token);
                }
                else while (!webAuth.IsCompleted) 
                {
                    progress.Report($"Continue in the browser.{Environment.NewLine}Time left: {timeout - DateTime.Now}");
                    if (timeout < DateTime.Now)
                    { 
                        ct.Cancel();
                        throw new TimeoutException("Timed out");
                    }

                    await Task.Yield();
                }
                
                (code, from_redirect_uri) = await webAuth;
            }

            // create new request for token
            progress.Report($"Retrieving Token");
            OAuthAccess response = await WebAuthentication.RetrieveToken(
                authTokenEndpoint, clientId, WebAuthentication.GrantType.AuthorizationCode, scope, code, from_redirect_uri, cancellationtoken);


            progress.Report($"Verifying Token in");
            return await LoginWithAccess(response, cancellationtoken);
        }
        

            // catch (Exception except)
            // {
            //     RainMeadow.Error(except);
            //     if (Custom.rainWorld.processManager.dialog != asyncWaitBox) return; // cancelled;
            //     Custom.rainWorld.processManager.StopSideProcess(asyncWaitBox);
            //     Custom.rainWorld.processManager.ShowDialog(new DialogNotify($"{except.Message}{Environment.NewLine}Please Try Again",
            //         Custom.rainWorld.processManager, delegate { }));
            //     throw;
            // }

            //     Custom.rainWorld.processManager.StopSideProcess(asyncWaitBox);
            //     Custom.rainWorld.processManager.ShowDialog(new DialogNotify($"You've successfully logged in as {auth._playerInfo.username}",
            //         Custom.rainWorld.processManager, delegate { }));

            //  += () =>
            // {
            //     tokenSource.Cancel();
            // };

            // Custom.rainWorld.processManager.ShowDialog(asyncWaitBox);
            // return attempt.task;

        public static async Task<Authentication> LoginWithSavedToken()
        {
            RainMeadow.DebugMe();
            byte[] encrypted_data = File.ReadAllBytes(AuthSaveLocation);
            string data = Encoding.ASCII.GetString(ProtectedData.Unprotect(encrypted_data, ADDITIONAL_ENTROPY, DataProtectionScope.LocalMachine));
            OAuthAccess? access = JsonConvert.DeserializeObject<OAuthAccess>(data);
            if (access is null) 
            {
                RainMeadow.Error(data);
                throw new Exception("Failed to deserialize OAuthAccess");
            }

            using (CancellationTokenSource source = new()) return await LoginWithAccess(access, source.Token);
        }

        [JsonObject(MemberSerialization.Fields)]
        struct OAuthUserInfoResponse
        {
            public string sub;
            public string username;
        }


        public static async Task<Authentication> LoginWithAccess(OAuthAccess access, CancellationToken cancellationToken)
        {
            // Check if the token is expired
            if ((DateTime.Now - access.expiresIn) > TimeSpan.FromMinutes(1))
            {
                RainMeadow.Debug("refreshing token");
                //expired refresh
                if (access.refreshToken is null) throw new Exception("null refresh token");
                access = await WebAuthentication.RetrieveToken(
                    authTokenEndpoint, clientId, WebAuthentication.GrantType.RefreshToken, scope, access.refreshToken!, null, cancellationToken);
            }
            
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(userInfoEndpoint);
            request.Headers.Add("Authorization", "Bearer " + access.accessToken);
            request.Method = "GET";
            using (HttpWebResponse response = await WebAuthentication.GetResponseNoException(request, cancellationToken))
            {
                // if (response.StatusCode != HttpStatusCode.OK && response.StatusCode != HttpStatusCode.BadRequest) throw new Exception($"Authetication Error: {(int)response.StatusCode} {response.StatusCode} {response.StatusDescription}");
                using (StreamReader reader = new(response.GetResponseStream()))
                {
                    using (JsonTextReader jsonreader = new(reader))
                    {
                        var serializer = JsonSerializer.CreateDefault();
                        if (response.StatusCode == HttpStatusCode.OK)
                        {        
                            
                            var info_response = serializer.Deserialize<OAuthUserInfoResponse>(jsonreader);
                            RainMeadow.Debug($"Logged in as {info_response.username}:{info_response.sub}");
                            currentAuthentication = new Authentication(access, new PlayerInfo { sub = info_response.sub, username = info_response.username });
                            SaveAuthentication(access);
                            return currentAuthentication;
                        }
                        else
                        {
                            var errror_response = serializer.Deserialize<OAuthErrorResponse>(jsonreader);
                            RainMeadow.Error(errror_response);
                            throw new Exception($"Authentication Error{Environment.NewLine}{errror_response.error_description}");    
                        }
                    }
                }
            }
        }

        static readonly byte[] ADDITIONAL_ENTROPY = { 0x52, 0x41, 0x49, 0x4e, 0x4d, 0x45, 0x41, 0x44, 0x4f, 0x57 };
        static string AuthSaveLocation => Path.Combine(Path.GetFullPath(Kittehface.Framework20.UserData.GetPersistentDataPath()), "inconspicuousfile.dat");

        public static void SaveAuthentication(OAuthAccess access)
        {
            RainMeadow.DebugMe();
            try
            {
                var serializer = JsonSerializer.CreateDefault();
                using (FileStream file = File.OpenWrite(AuthSaveLocation))
                {
                    byte[] unencrypted_data = Encoding.ASCII.GetBytes(JsonConvert.SerializeObject(access));
                    byte[] data = ProtectedData.Protect(unencrypted_data, ADDITIONAL_ENTROPY, DataProtectionScope.LocalMachine);
                    file.Write(data, 0, data.Length);
                }
            }
            catch (Exception except)
            {
                RainMeadow.Error(except);
            }
        }

        private Authentication(OAuthAccess access, PlayerInfo info)
        {
            _access = access;
            _playerInfo = info;
        }
        
        public static Authentication? currentAuthentication { get; private set; } = null;
        public static PlayerInfo? Info => currentAuthentication?._playerInfo;
        public static OAuthAccess? currentAccess => currentAuthentication?._access;
        public readonly OAuthAccess _access;
        public readonly PlayerInfo _playerInfo;
    }
}