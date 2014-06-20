
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Net.Http;
using Android.OS;
using Android.Runtime;
using Android.Support.V4.App;
using Android.Util;
using Android.Views;
using Android.Webkit;
using Android.Widget;
using Xamarin.Utilities.Android;

namespace Xamarin.Auth
{
    public class WebAuthenticatorFragment : Android.App.Fragment
    {
        WebView webView;

        public WebAuthenticator Authenticator { get; set; }

        public static WebAuthenticatorFragment NewInstance(WebAuthenticator authenticator)
        {
            var frag = new WebAuthenticatorFragment 
            {
                Arguments = new Bundle(), 
                Authenticator = authenticator
            };
            return frag;
        }

        public override void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);
        }

        private FrameLayout layout;
        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            layout = new FrameLayout (Activity);
            layout.SetBackgroundColor(Color.White);

            //
            // Build the UI
            //
            webView = new WebView (Activity) {
                Id = 42
            };
            webView.Settings.JavaScriptEnabled = true;
            webView.SetWebViewClient (new Client (this));

            layout.AddView (webView);

            //
            // Restore the UI state or start over
            //
            if (savedInstanceState != null) {
                webView.RestoreState (savedInstanceState);
            }
            else {
                if (Activity.Intent.GetBooleanExtra ("ClearCookies", false))
                    WebAuthenticator.ClearCookies();
            }

            return layout;
        }

        public void BeginLoadingInitialUrl ()
        {
            Authenticator.GetInitialUrlAsync ().ContinueWith (t => {
                if (t.IsFaulted) {
                    Activity.ShowError ("Authentication Error", t.Exception);
                }
                else {
                    webView.LoadUrl (t.Result.AbsoluteUri);
                }
            }, TaskScheduler.FromCurrentSynchronizationContext ());
        }

        public override void OnSaveInstanceState (Bundle outState)
        {
            base.OnSaveInstanceState (outState);
            webView.SaveState (outState);
        }

        private bool _continueWithShowingBrowser;

        /// <summary>
        /// Shows the progress bar and hides the browser control.
        /// </summary>
        void BeginProgress (string message)
        {
            _continueWithShowingBrowser = false;

            webView.Visibility = ViewStates.Gone;
            layout.Visibility = ViewStates.Gone;
        }

        /// <summary>
        /// Hides the progress bar and shows the browser control.
        /// </summary>
        void EndProgress()
        {
            if (!Authenticator.HasCompleted)
            {
                _continueWithShowingBrowser = true;
                StartTimer (webView);
            }
        }

        private async void StartTimer(WebView webView)
        {
            await Task.Delay (150);

            if (!_continueWithShowingBrowser)
            {
                return;
            }

            _continueWithShowingBrowser = false;

            if (!Authenticator.HasCompleted)
            {
                webView.Visibility = ViewStates.Visible;
                layout.Visibility = ViewStates.Visible;
            }
        }


        class Client : WebViewClient
        {
            WebAuthenticatorFragment fragment;
            HashSet<SslCertificate> sslContinue;
            Dictionary<SslCertificate, List<SslErrorHandler>> inProgress;

            public Client (WebAuthenticatorFragment fragment)
            {
                this.fragment = fragment;
            }

            public override bool ShouldOverrideUrlLoading (WebView view, string url)
            {
                return false;
            }

            public override void OnPageStarted (WebView view, string url, Bitmap favicon)
            {
                var uri = new Uri (url);
                fragment.Authenticator.OnPageLoading (uri);
                fragment.BeginProgress (uri.Authority);
            }

            public override void OnPageFinished (WebView view, string url)
            {
                var uri = new Uri (url);
                fragment.Authenticator.OnPageLoaded (uri);
                fragment.EndProgress ();
            }

            class SslCertificateEqualityComparer
                : IEqualityComparer<SslCertificate>
            {
                public bool Equals (SslCertificate x, SslCertificate y)
                {
                    return Equals (x.IssuedTo, y.IssuedTo) && Equals (x.IssuedBy, y.IssuedBy) && x.ValidNotBeforeDate.Equals (y.ValidNotBeforeDate) && x.ValidNotAfterDate.Equals (y.ValidNotAfterDate);
                }

                bool Equals (SslCertificate.DName x, SslCertificate.DName y)
                {
                    if (ReferenceEquals (x, y))
                        return true;
                    if (ReferenceEquals (x, y) || ReferenceEquals (null, y))
                        return false;
                    return x.GetDName().Equals (y.GetDName());
                }

                public int GetHashCode (SslCertificate obj)
                {
                    unchecked {
                        int hashCode = GetHashCode (obj.IssuedTo);
                        hashCode = (hashCode * 397) ^ GetHashCode (obj.IssuedBy);
                        hashCode = (hashCode * 397) ^ obj.ValidNotBeforeDate.GetHashCode();
                        hashCode = (hashCode * 397) ^ obj.ValidNotAfterDate.GetHashCode();
                        return hashCode;
                    }
                }

                int GetHashCode (SslCertificate.DName dname)
                {
                    return dname.GetDName().GetHashCode();
                }
            }

            public override void OnReceivedSslError (WebView view, SslErrorHandler handler, SslError error)
            {
                if (sslContinue == null) {
                    var certComparer = new SslCertificateEqualityComparer();
                    sslContinue = new HashSet<SslCertificate> (certComparer);
                    inProgress = new Dictionary<SslCertificate, List<SslErrorHandler>> (certComparer);
                }

                List<SslErrorHandler> handlers;
                if (inProgress.TryGetValue (error.Certificate, out handlers)) {
                    handlers.Add (handler);
                    return;
                }

                if (sslContinue.Contains (error.Certificate)) {
                    handler.Proceed();
                    return;
                }

                inProgress[error.Certificate] = new List<SslErrorHandler>();

                AlertDialog.Builder builder = new AlertDialog.Builder (this.fragment.Activity);
                builder.SetTitle ("Security warning");
                builder.SetIcon (Android.Resource.Drawable.IcDialogAlert);
                builder.SetMessage ("There are problems with the security certificate for this site.");

                builder.SetNegativeButton ("Go back", (sender, args) => {
                    UpdateInProgressHandlers (error.Certificate, h => h.Cancel());
                    handler.Cancel();
                });

                builder.SetPositiveButton ("Continue", (sender, args) => {
                    sslContinue.Add (error.Certificate);
                    UpdateInProgressHandlers (error.Certificate, h => h.Proceed());
                    handler.Proceed();
                });

                builder.Create().Show();
            }

            void UpdateInProgressHandlers (SslCertificate certificate, Action<SslErrorHandler> update)
            {
                List<SslErrorHandler> inProgressHandlers;
                if (!this.inProgress.TryGetValue (certificate, out inProgressHandlers))
                    return;

                foreach (SslErrorHandler sslErrorHandler in inProgressHandlers)
                    update (sslErrorHandler);

                inProgressHandlers.Clear();
            }
        }
    }
}

