using Google.Apis.Http;

namespace UI
{
    /// <summary>
    /// Сервис авторизаци в Google Drive
    /// </summary>
    public interface IGoogleAuthorizeService
    {
        /// <summary>
        /// Авторизоваться в Google Drive
        /// </summary>
        IConfigurableHttpClientInitializer Authorize();

        void Clear();
    }

}
