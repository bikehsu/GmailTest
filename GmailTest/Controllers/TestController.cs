using Google.Apis.Gmail.v1;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Web;
using Google.Apis.Util.Store;
using Microsoft.AspNetCore.Mvc;

namespace GmailTest.Controllers
{
    public class TestController : Controller
    {
        /// <summary>
        /// 取得授權的項目
        /// </summary>
        static string[] Scopes = { GmailService.Scope.GmailSend };//GmailService.Scope.GmailReadonly

        /// <summary>
        /// 這個值看來是可以任意填寫
        /// </summary>
        static string ApplicationName = "Gmail Sender";

        // 和登入 google 的帳號無關
        // 任意值，若未來有使用者認証，可使用使用者編號或登入帳號。
        string Username = "ABC";

        /// <summary>
        /// 存放 client_secret 和 credential 的地方
        /// </summary>
        string SecretPath = @"D:\project\GmailTest\Data\Secrets";

        public string Index()
        {
            return "OK";
        }

        public async Task<string> GetAuthUrl()
        {
            string[] scopes = new[] { GmailService.Scope.GmailSend };

            using (var stream = new FileStream(Path.Combine(SecretPath, "client_secret.json"), FileMode.Open, FileAccess.Read))
            {
                FileDataStore dataStore = null;
                var credentialRoot = Path.Combine(SecretPath, "Credentials");
                if (!Directory.Exists(credentialRoot))
                {
                    Directory.CreateDirectory(credentialRoot);
                }

                //存放 credential 的地方，每個 username 會建立一個目錄。
                string filePath = Path.Combine(credentialRoot, Username);                
                dataStore = new FileDataStore(filePath);

                IAuthorizationCodeFlow flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
                {
                    ClientSecrets = GoogleClientSecrets.Load(stream).Secrets,
                    Scopes = scopes,
                    DataStore = dataStore
                });

                //認証完成後回傳的網址, 必需和 OAuth 2.0 Client Id 中填寫的 "已授權的重新導向 URI" 相同。
                var redirectUri = $"https://localhost:44340/Home/AuthReturn";
                var authResult = await new AuthorizationCodeWebApp(flow, redirectUri, Username)
                .AuthorizeAsync(Username, CancellationToken.None);

                return authResult.RedirectUri;
            }
        }
    }
}
