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
using System.Collections.Generic;
using Android.App;
using Android.OS;
using Xamarin.Utilities.Android;
using Android.Widget;
using Android.Views;
using Android.Graphics;

namespace Xamarin.Auth
{
	[Activity (Label = "Web Authenticator")]
#if XAMARIN_AUTH_INTERNAL
	internal class WebAuthenticatorActivity : Activity
#else
	public class WebAuthenticatorActivity : Activity
#endif
	{
		internal class State : Java.Lang.Object
		{
			public WebAuthenticator Authenticator;
		}
		internal static readonly ActivityStateRepository<State> StateRepo = new ActivityStateRepository<State> ();

		State state;

		protected override void OnCreate (Bundle savedInstanceState)
		{
			base.OnCreate (savedInstanceState);
            
			//
			// Load the state either from a configuration change or from the intent.
			//
			state = LastNonConfigurationInstance as State;
			if (state == null && Intent.HasExtra ("StateKey")) {
				var stateKey = Intent.GetStringExtra ("StateKey");
				state = StateRepo.Remove (stateKey);
			}
			if (state == null) {
				Finish ();
				return;
			}

			Title = state.Authenticator.Title;

			//
			// Watch for completion
			//
			state.Authenticator.Completed += (s, e) => {
				SetResult (e.IsAuthenticated ? Result.Ok : Result.Canceled);
				Finish ();
			};
			state.Authenticator.Error += (s, e) => {
				if (e.Exception != null) {
					this.ShowError ("Authentication Error", e.Exception);
				}
				else {
					this.ShowError ("Authentication Error", e.Message);
				}
			};

			//
			// Build the UI
			//
            var relativeLayout = new RelativeLayout (this);
            relativeLayout.SetBackgroundColor(Color.White);

            var linearLayout = new LinearLayout (this)
            {
                Orientation = Orientation.Vertical,
                LayoutParameters = new LinearLayout.LayoutParams (
                    LinearLayout.LayoutParams.FillParent,
                    LinearLayout.LayoutParams.MatchParent)
            };
            linearLayout.SetGravity (GravityFlags.Bottom);

            var progressBar = new ProgressBar (this)
            {
                LayoutParameters = new LinearLayout.LayoutParams (
                    LinearLayout.LayoutParams.FillParent,
                    dpToPx(40))
            };
            linearLayout.AddView (progressBar);

            var textView = new TextView(this)
            {
                LayoutParameters = new LinearLayout.LayoutParams (
                    LinearLayout.LayoutParams.WrapContent,
                    LinearLayout.LayoutParams.WrapContent) { Gravity = GravityFlags.CenterHorizontal },
                Text = "Logging in..."                               
            };
            ((ViewGroup.MarginLayoutParams)textView.LayoutParameters).BottomMargin = dpToPx(75);
            linearLayout.AddView (textView);

            relativeLayout.AddView (linearLayout);

            AddContentView (relativeLayout, new LinearLayout.LayoutParams (
                Android.Views.ViewGroup.LayoutParams.FillParent,
                Android.Views.ViewGroup.LayoutParams.MatchParent) { Gravity = GravityFlags.Bottom } );

            var details = WebAuthenticatorFragment.NewInstance(state.Authenticator);
            var fragmentTransaction = this.FragmentManager.BeginTransaction();
            fragmentTransaction.Add(Android.Resource.Id.Content, details);
            fragmentTransaction.Commit();

            details.BeginLoadingInitialUrl ();
		}

		public override void OnBackPressed ()
		{
			if (state.Authenticator.AllowCancel)
			{
				state.Authenticator.OnCancelled ();
			}
		}

		public override Java.Lang.Object OnRetainNonConfigurationInstance ()
		{
			return state;
		}

        public static int dpToPx(int dp)
        {
            return (int) (dp * Android.Content.Res.Resources.System.DisplayMetrics.Density);
        }
	}
}

