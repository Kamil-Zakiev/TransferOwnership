using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.WinForms;
using Google.Apis.Http;
using Google.Apis.Util.Store;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace UI
{
    public class GoogleAuthorizeService : IGoogleAuthorizeService
    {
        private string _configFile;

        string[] _scopes;

        string _credPath;

        FileDataStore _store;

        public GoogleAuthorizeService(string configFile, string[] scopes, string credPath)
        {
            if (string.IsNullOrWhiteSpace(configFile))
            {
                throw new ArgumentNullException(nameof(configFile));
            }

            if (!File.Exists(configFile))
            {
                throw new FileNotFoundException("Не найден файл с конфигурациями", configFile);
            }

            _configFile = configFile;
            _scopes = scopes;
            _credPath = credPath;
            _store = new FileDataStore(_credPath, true);
        }

        public async Task<IConfigurableHttpClientInitializer> Authorize()
        {
            using (var stream = new FileStream(_configFile, FileMode.Open, FileAccess.Read))
            {
                var creds = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                    GoogleClientSecrets.Load(stream).Secrets,
                    _scopes,
                    "user",
                    CancellationToken.None,
                    _store
                    );

                return creds;
            }
        }

        public void Clear()
        {
            var t = _store.ClearAsync();
            t.Wait();
        }
    }
}
