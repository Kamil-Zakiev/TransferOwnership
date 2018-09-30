using System;
using System.Threading;
using System.Threading.Tasks;

using Google.Apis.Auth.OAuth2.Requests;
using Google.Apis.Auth.OAuth2.Responses;
using UI;

namespace Google.Apis.Auth.OAuth2.WinForms
{
    /// <summary>
    /// OAuth 2.0 verification code receiver for Windows Forms that opens an embedded Google account dialog to enter the
    /// user's credentials and accepts the application access to its token.
    /// </summary>
    public class EmbeddedBrowserCodeReceiver : ICodeReceiver
    {
        #region ICodeReceiver Members

        public string RedirectUri
        {
            get { return GoogleAuthConsts.InstalledAppRedirectUri; }
        }

        public Task<AuthorizationCodeResponseUrl> ReceiveCodeAsync(AuthorizationCodeRequestUrl url,
            CancellationToken taskCancellationToken)
        {
            var tcs = new TaskCompletionSource<AuthorizationCodeResponseUrl>();

            try
            {
                var webAuthDialog = new LoginForm(url.Build());
                webAuthDialog.ShowDialog();
                if (webAuthDialog.ResponseUrl == null)
                {
                    tcs.SetCanceled();
                }
                else
                {
                    tcs.SetResult(webAuthDialog.ResponseUrl);
                }
                return tcs.Task;
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
                return tcs.Task;
            }
        }

        #endregion
    }
}