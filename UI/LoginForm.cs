using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Responses;
using System;
using System.Windows.Forms;

namespace UI
{
    public partial class LoginForm : Form
    {
        /// <summary>
        /// The response from the authorization service when the user clicks Accept or Deny.
        /// </summary>
        /// <remarks>Will be null if the user has not clicked Accept or Deny.</remarks>
        public AuthorizationCodeResponseUrl ResponseUrl { get; protected set; }

        /// <summary>Constructs a new authentication broker dialog and navigates to
        /// the authorization page. From there, the user has control to choose Accept or Deny
        /// via the dialog's embedded web browser.</summary>
        public LoginForm(Uri authUri)
        {
            InitializeComponent();

            webBrowser1.ScriptErrorsSuppressed = true;

            // Set the browser to the initial uri.
            webBrowser1.Navigate(authUri);
            // Need to track when the results has been received.
            // Installed applications use an out-of-band redirect URI.
            // When the user clicks Accept or Deny, the redirected response will
            // be in the title of the web page.
            webBrowser1.DocumentTitleChanged += authWebBrowser_TitleChanged;
        }

        private void authWebBrowser_TitleChanged(object sender, EventArgs e)
        {
            // The result always comes back 
            var browser = (WebBrowser)sender;
            if (browser.Url.AbsolutePath == new Uri(GoogleAuthConsts.ApprovalUrl).AbsolutePath)
            {
                // Per API documentation, the reponse is after the first space in the title.
                var query = browser.DocumentTitle.Substring(browser.DocumentTitle.IndexOf(" ") + 1);
                ResponseUrl = new AuthorizationCodeResponseUrl(query);
                
                // Dialog is no longer neeed.
                this.Close();
            }
            else
                System.Diagnostics.Trace.TraceInformation(browser.Url.AbsolutePath);
        }
    }
}
