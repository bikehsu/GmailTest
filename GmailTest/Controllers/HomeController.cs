using Google.Apis.Gmail.v1;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Auth.OAuth2.Web;
using Google.Apis.Util.Store;

using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using GmailTest.Models;
using Google.Apis.Services;
using Google.Apis.Auth.OAuth2.Requests;
using System.Net.Mail;
using MimeKit;

namespace GmailTest.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }


        /// <summary>
        /// 取得授權的項目
        /// </summary>
        static string[] Scopes = { GmailService.Scope.GmailSend };//GmailService.Scope.GmailReadonly

        // 和登入 google 的帳號無關
        // 任意值，若未來有使用者認証，可使用使用者編號或登入帳號。
        string Username = "DEF";

        /// <summary>
        /// 存放 client_secret 和 credential 的地方
        /// </summary>
        string SecretPath = @"D:\project\GmailTest\Data\Secrets";

        /// <summary>
        /// 認証完成後回傳的網址, 必需和 OAuth 2.0 Client Id 中填寫的 "已授權的重新導向 URI" 相同。
        /// </summary>
        string RedirectUri = $"https://localhost:44340/Home/AuthReturn";

        /// <summary>
        /// 取得認証用的網址
        /// </summary>
        /// <returns></returns>
        public async Task<string> GetAuthUrl()
        {
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
                    Scopes = Scopes,
                    DataStore = dataStore
                });
                
                var authResult = await new AuthorizationCodeWebApp(flow, RedirectUri, Username)
                .AuthorizeAsync(Username, CancellationToken.None);

                return authResult.RedirectUri;
            }
        }


        public async Task<string> AuthReturn(AuthorizationCodeResponseUrl authorizationCode)
        {
            using (var stream = new FileStream(Path.Combine(SecretPath, "client_secret.json"), FileMode.Open, FileAccess.Read))
            {
                //確認 credential 的目錄已建立.
                var credentialRoot = Path.Combine(SecretPath, "Credentials");
                if (!Directory.Exists(credentialRoot))
                {
                    Directory.CreateDirectory(credentialRoot);
                }

                if (string.IsNullOrEmpty(authorizationCode.State))
                {
                    authorizationCode.State = Username + DateTime.Now.ToString("yyyyMMddHHmmssfff");
                }

                //暫存憑証用目錄
                string tempPath = Path.Combine(credentialRoot, authorizationCode.State);             

                IAuthorizationCodeFlow flow = new GoogleAuthorizationCodeFlow(
                new GoogleAuthorizationCodeFlow.Initializer
                {
                    ClientSecrets = GoogleClientSecrets.Load(stream).Secrets,
                    Scopes = Scopes,
                    DataStore = new FileDataStore(tempPath)
                });

                //這個動作應該是要把 code 換成 token
                await flow.ExchangeCodeForTokenAsync(Username, authorizationCode.Code, RedirectUri, CancellationToken.None).ConfigureAwait(false);

                if (!string.IsNullOrWhiteSpace(authorizationCode.State))
                {                   
                    string newPath = Path.Combine(credentialRoot, Username);
                    if (tempPath.ToLower() != newPath.ToLower())
                    {
                        if (Directory.Exists(newPath))
                            Directory.Delete(newPath, true);

                        Directory.Move(tempPath, newPath);
                    }
                }

                return "OK";
            }
        }

        public async Task<bool> SendTestMail()
        {
            var service = await GetGmailService();

            GmailMessage message = new GmailMessage();
            message.Subject = "標題";
            message.Body = $"<h1>內容</h1>";
            message.FromAddress = "bikehsu@gmail.com";
            message.IsHtml = true;
            message.ToRecipients = "bikehsu@gmail.com";
            message.Attachments = new List<Attachment>();

            string filePath = @"C:\Users\bike\Pictures\Vegetable_pumpkin.jpg";    //要附加的檔案
            Attachment attachment1 = new Attachment(filePath);
            message.Attachments.Add(attachment1);

            SendEmail(message, service);
            Console.WriteLine("OK");

            return true;
        }

        async Task<GmailService> GetGmailService()
        {
            UserCredential credential = null;

            var credentialRoot = Path.Combine(SecretPath, "Credentials");
            if (!Directory.Exists(credentialRoot))
            {
                Directory.CreateDirectory(credentialRoot);
            }

            string filePath = Path.Combine(credentialRoot, Username);

            using (var stream = new FileStream(Path.Combine(SecretPath, "client_secret.json"), FileMode.Open, FileAccess.Read))
            {
                credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                GoogleClientSecrets.Load(stream).Secrets,
                Scopes,
                Username,
                CancellationToken.None,
                new FileDataStore(filePath));
            }

            var service = new GmailService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = "Send Mail",
            });

            return service;
        }


        public class GmailMessage
        {
            public string FromAddress { get; set; }
            public string ToRecipients { get; set; }

            public string Subject { get; set; }
            public string Body { get; set; }
            public bool IsHtml { get; set; }

            public List<System.Net.Mail.Attachment> Attachments { get; set; }
        }


        public static void SendEmail(GmailMessage email, GmailService service)
        {
            var mailMessage = new System.Net.Mail.MailMessage();
            mailMessage.From = new System.Net.Mail.MailAddress(email.FromAddress);
            mailMessage.To.Add(email.ToRecipients);
            mailMessage.ReplyToList.Add(email.FromAddress);
            mailMessage.Subject = email.Subject;
            mailMessage.Body = email.Body;
            mailMessage.IsBodyHtml = email.IsHtml;

            if (email.Attachments != null)
            {
                foreach (System.Net.Mail.Attachment attachment in email.Attachments)
                {
                    mailMessage.Attachments.Add(attachment);
                }
            }

            var mimeMessage = MimeKit.MimeMessage.CreateFromMailMessage(mailMessage);

            var gmailMessage = new Google.Apis.Gmail.v1.Data.Message
            {
                Raw = Encode(mimeMessage)
            };

            Google.Apis.Gmail.v1.UsersResource.MessagesResource.SendRequest request = service.Users.Messages.Send(gmailMessage, "me");

            request.Execute();
        }

        public static string Encode(MimeMessage mimeMessage)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                mimeMessage.WriteTo(ms);
                return Convert.ToBase64String(ms.GetBuffer())
                    .TrimEnd('=')
                    .Replace('+', '-')
                    .Replace('/', '_');
            }
        }

        /// <summary>
        /// 這個是測試用一個 Action 取得 Credential, 不存在時可以直接重導到 google 取得授權。
        /// </summary>
        /// <returns></returns>
        public async Task<UserCredential> GetCredential()
        {
            UserCredential credential = null;

            var credentialRoot = Path.Combine(SecretPath, "Credentials");
            if (!Directory.Exists(credentialRoot))
            {
                Directory.CreateDirectory(credentialRoot);
            }

            string filePath = Path.Combine(credentialRoot, Username);

            using (var stream = new FileStream(Path.Combine(SecretPath, "client_secret.json"), FileMode.Open, FileAccess.Read))
            {
                dsAuthorizationBroker.RedirectUri = RedirectUri;

                credential = await dsAuthorizationBroker.AuthorizeAsync(
                GoogleClientSecrets.Load(stream).Secrets,
                Scopes,
                Username,
                CancellationToken.None,
                new FileDataStore(filePath));
            }

            return credential;
        }

        public class dsAuthorizationBroker : GoogleWebAuthorizationBroker
        {
            public static string RedirectUri;

            public new static async Task<UserCredential> AuthorizeAsync(
                ClientSecrets clientSecrets,
                IEnumerable<string> scopes,
                string user,
                CancellationToken taskCancellationToken,
                IDataStore dataStore = null)
            {
                var initializer = new GoogleAuthorizationCodeFlow.Initializer
                {
                    ClientSecrets = clientSecrets,
                };
                return await AuthorizeAsyncCore(initializer, scopes, user,
                    taskCancellationToken, dataStore).ConfigureAwait(false);
            }

            private static async Task<UserCredential> AuthorizeAsyncCore(
                GoogleAuthorizationCodeFlow.Initializer initializer,
                IEnumerable<string> scopes,
                string user,
                CancellationToken taskCancellationToken,
                IDataStore dataStore)
            {
                initializer.Scopes = scopes;
                initializer.DataStore = dataStore ?? new FileDataStore(Folder);
                var flow = new dsAuthorizationCodeFlow(initializer);
                return await new AuthorizationCodeInstalledApp(flow,
                    new LocalServerCodeReceiver())
                    .AuthorizeAsync(user, taskCancellationToken).ConfigureAwait(false);
            }
        }


        public class dsAuthorizationCodeFlow : GoogleAuthorizationCodeFlow
        {
            public dsAuthorizationCodeFlow(Initializer initializer)
                : base(initializer) { }

            public override AuthorizationCodeRequestUrl
                           CreateAuthorizationCodeRequest(string redirectUri)
            {
                return base.CreateAuthorizationCodeRequest(dsAuthorizationBroker.RedirectUri);
            }
        }
    }
}