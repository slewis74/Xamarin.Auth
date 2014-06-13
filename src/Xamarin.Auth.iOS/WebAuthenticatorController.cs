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
using System.Diagnostics;
using System.Drawing;

namespace Xamarin.Auth
{

	/// <summary>
	/// The ViewController that the WebAuthenticator presents to the user.
	/// </summary>
	internal class WebAuthenticatorController : UIViewController
	{
		protected WebAuthenticator authenticator;
		protected WebAuthenticatorView view;

		public WebAuthenticatorController (WebAuthenticator authenticator, UIView loadingView = null)
		{
			this.authenticator = authenticator;

			Title = authenticator.Title;

			if (authenticator.AllowCancel)
			{
				NavigationItem.LeftBarButtonItem = new UIBarButtonItem (
					UIBarButtonSystemItem.Cancel,
					delegate {
					view.Cancel ();
				});				
			}

			if(loadingView == null)
			{
				loadingView = new UIView(View.Bounds);
				loadingView.BackgroundColor = UIColor.White;

				float labelHeight = 22;
				float labelWidth = loadingView.Frame.Width - 20;

				// derive the center x and y
				float centerX = loadingView.Frame.Width / 2;
				float centerY = loadingView.Frame.Height / 2;

				var activitySpinner = new UIActivityIndicatorView(UIActivityIndicatorViewStyle.Gray);

				activitySpinner.Frame = new RectangleF (
					centerX - (activitySpinner.Frame.Width / 2) ,
					centerY - activitySpinner.Frame.Height - 20 ,
					activitySpinner.Frame.Width ,
					activitySpinner.Frame.Height);
				activitySpinner.AutoresizingMask = UIViewAutoresizing.FlexibleMargins;
				loadingView.AddSubview (activitySpinner);
				activitySpinner.StartAnimating ();

				var loadingLabel = new UILabel(new RectangleF (
					centerX - (labelWidth / 2),
					centerY + 20 ,
					labelWidth ,
					labelHeight
				));
				loadingLabel.BackgroundColor = UIColor.Clear;
				loadingLabel.TextColor = UIColor.Black;
				loadingLabel.Text = "Logging in...";
				loadingLabel.TextAlignment = UITextAlignment.Center;
				loadingLabel.AutoresizingMask = UIViewAutoresizing.FlexibleMargins;

				loadingView.AddSubview (loadingLabel);
			}
			view = new WebAuthenticatorView(authenticator, this, loadingView);

			view.Frame = View.Bounds;

			View.Add(view);
		}
	}
}

