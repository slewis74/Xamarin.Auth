//
//  Copyright 2012-2013, Xamarin Inc.
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.
//
using System;
using System.Threading.Tasks;
using MonoTouch.UIKit;
using MonoTouch.Foundation;
using Xamarin.Utilities.iOS;
using Xamarin.Controls;
using System.Drawing;

namespace Xamarin.Auth
{
	public class WebAuthenticatorView : UIView
	{
		UIWebView webView;
		UIViewController parent;
		protected WebAuthenticator authenticator;

		UIView authenticatingView;
		ProgressLabel progress;

		bool keepTryingAfterError = true;

		public WebAuthenticatorView (WebAuthenticator authenticator, UIViewController parent, UIView loadingView = null)
		{
			this.authenticator = authenticator;
			this.parent = parent;

            authenticator.Error += HandleError;
			authenticator.BrowsingCompleted += HandleBrowsingCompleted;

            var bounds = new RectangleF(0, 60, parent.View.Bounds.Width, parent.View.Bounds.Height - 60);

            if (!(parent is WebAuthenticatorController))
			{
                bounds = new RectangleF(0, 20, parent.View.Bounds.Width, parent.View.Bounds.Height - 20);
			}

            webView = new UIWebView (bounds) {
				Delegate = new WebViewDelegate (this)
			};

            if (loadingView != null)
            {
                Add (loadingView);
            }

            Add(webView);

            BeginLoadingInitialUrl ();
		}

		public void Cancel ()
		{
			authenticator.OnCancelled ();
		}

		void BeginLoadingInitialUrl ()
		{
			authenticator.GetInitialUrlAsync ().ContinueWith (t => {
				if (t.IsFaulted) {
					keepTryingAfterError = false;
					authenticator.OnError (t.Exception);
				}
				else {
					// Delete cookies so we can work with multiple accounts
					if (this.authenticator.ClearCookiesBeforeLogin)
						WebAuthenticator.ClearCookies();

					//
					// Begin displaying the page
					//
					LoadInitialUrl (t.Result);
				}
			}, TaskScheduler.FromCurrentSynchronizationContext ());
		}

		void LoadInitialUrl (Uri url)
		{
			if (url != null) {
				var request = new NSUrlRequest (new NSUrl (url.AbsoluteUri));
				NSUrlCache.SharedCache.RemoveCachedResponse (request); // Always try
				webView.LoadRequest (request);
			}
		}

		void HandleBrowsingCompleted (object sender, EventArgs e)
		{
			if (authenticatingView == null) {
				authenticatingView = new UIView (Bounds) {
					AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight,
					BackgroundColor = UIColor.FromRGB (0x33, 0x33, 0x33),
				};
				progress = new ProgressLabel ("Authenticating...");
				var f = progress.Frame;
				var b = authenticatingView.Bounds;
				f.X = (b.Width - f.Width) / 2;
				f.Y = (b.Height - f.Height) / 2;
				progress.Frame = f;
				authenticatingView.Add (progress);
			}
			else {
				authenticatingView.Frame = Bounds;
			}
			progress.StartAnimating ();
		}

		void HandleError (object sender, AuthenticatorErrorEventArgs e)
		{
			var after = keepTryingAfterError ?
				(Action)BeginLoadingInitialUrl :
				(Action)Cancel;

			if (e.Exception != null) {
				parent.ShowError ("Authentication Error", e.Exception, after);
			}
			else {
				parent.ShowError ("Authentication Error", e.Message, after);
			}
		}

		protected class WebViewDelegate : UIWebViewDelegate
		{
			protected WebAuthenticatorView view;
			Uri lastUrl;

			public WebViewDelegate (WebAuthenticatorView view)
			{
				this.view = view;
			}

			public override bool ShouldStartLoad (UIWebView webView, NSUrlRequest request, UIWebViewNavigationType navigationType)
			{
				var nsUrl = request.Url;

				if (nsUrl != null && !view.authenticator.HasCompleted) {
					Uri url;
					if (Uri.TryCreate (nsUrl.AbsoluteString, UriKind.Absolute, out url)) {
						view.authenticator.OnPageLoading (url);
					}
				}

				return true;
			}

			public override void LoadStarted (UIWebView webView)
			{
				webView.Hidden = true;

				webView.UserInteractionEnabled = false;
			}

			public override void LoadFailed (UIWebView webView, NSError error)
			{
				if (error.Domain == "NSURLErrorDomain" && error.Code == -999)
					return;

				webView.UserInteractionEnabled = true;

				view.authenticator.OnError (error.LocalizedDescription);
			}

			public override void LoadingFinished (UIWebView webView)
			{
				if(!view.authenticator.HasCompleted) webView.Hidden = false;

				webView.UserInteractionEnabled = true;

				var url = new Uri (webView.Request.Url.AbsoluteString);
				if (url != lastUrl && !view.authenticator.HasCompleted) {
					lastUrl = url;
					view.authenticator.OnPageLoaded (url);
				}
			}
		}
	}
	
}
