using System.Net;
using ServiceStack.Common;
using ServiceStack.Common.ServiceClient.Web;
using ServiceStack.ServiceClient.Web;

namespace LeafToAutomationImport
{
    public class Api : JsonServiceClient
    {
        private string _apikey;

        /// <summary>
        /// The current apikey to use for authentication
        /// </summary>
        public string Apikey
        {
            get { return _apikey; }
            set
            {
                _apikey = value;
                SwipeId = "";
            }
        }

        private string SwipeId { get; set; }

        public bool AutoHandleAuthErrors { get; set; }

        public Api()
        {
            StoreCookies = true;
            //AlwaysSendBasicAuthHeader = true;

            LocalHttpWebRequestFilter = request =>
            {
                if (!string.IsNullOrEmpty(Apikey))
                {
                    request.Headers.Add("pos-server-apikey", Apikey);
                }
                if (!string.IsNullOrEmpty(SwipeId))
                {
                    request.Headers.Add("pos-server-swipeid", SwipeId);
                }
            };
        }

        public new void SetCredentials(string userName, string password)
        {
            if (!userName.IsNullOrEmpty() && password.IsNullOrEmpty())
            {
                // Attempt to use username as a swipe ID
                SwipeId = userName;
            }
            else
            {
                SwipeId = "";
                base.SetCredentials(userName, password);
            }
        }

        private void ClearCredentials()
        {
            SetCredentials("", "");
            Apikey = "";
            SwipeId = "";
            Headers.Clear();
            CookieContainer = new CookieContainer();
        }

        public bool HasCredentials()
        {
            return !UserName.IsNullOrEmpty() || !Password.IsNullOrEmpty() || !Apikey.IsNullOrEmpty() ||
                   !SwipeId.IsNullOrEmpty();
        }

        public void Logout()
        {
            if (!HasCredentials()) return;
            Get<AuthResponse>("/auth/logout");
            ClearCredentials();
        }
    }
}