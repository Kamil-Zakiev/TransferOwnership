using Google.Apis.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        Task<IConfigurableHttpClientInitializer> Authorize();
    }

}
