using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using Tomboy;
using Tomboy.OAuth;
using Tomboy.Sync;
using Tomboy.Sync.Filesystem;
using Tomboy.Sync.Web;

namespace tomboysyncexample
{
	class MainClass
	{
		public static DiskStorage localStorage;
		public static Engine localEngine;

		public static void Main (string[] args)
		{
			// ----- PREPARATION STEPS ------
			
			// setup a sample note storage that represent the notes on the client (i.e. Tomboy)
			string temporaryPath = "client_notes";
			if (Directory.Exists (temporaryPath))
				Directory.Delete (temporaryPath, true);
			localStorage = new DiskStorage (temporaryPath);
			Console.WriteLine ("using {0} as local note location", temporaryPath);

			// add some sample notes to the client
			localEngine = new Engine (localStorage);
			List<Note> notes = GetSomeSampleNotes ();
			notes.ForEach (note => localEngine.SaveNote (note));
			
			// setup a manifest that we use to keep track of changes of the notes
			// a client must maintain a manifest all the time!
			SyncManifest localManifest = new SyncManifest ();


			// ---- PERFORM THE ONE TIME TOKEN EXCHANGE FOR AUTHENTICATION ----
			
			// we need a localhost http server that is the target of the OAuth redirection
			// NOTE: Android and iOS have a custom API to register callback urls, i.e. tomdroid uses a tomboy:// uri
			// see the Android/iOS API docs
			HttpListener listener = new HttpListener ();
			string callbackUrl = "http://localhost:9001/";
			listener.Prefixes.Add (callbackUrl);
			listener.Start ();

			// create the delegate that is called upon connecting to the remote server
			var callback_delegate = new OAuthAuthorizationCallback (url => {
				// open a browser for the user so he can authenticate with his user/password
				Process.Start (url);
				
				// wait (block) until the HttpListener has received a request 
				var context = listener.GetContext ();
				
				// if we reach here the authentication has most likely been successfull and we have the
				// oauth_identifier in the request url query as a query parameter
				var request_url = context.Request.Url;
				string oauth_verifier = System.Web.HttpUtility.ParseQueryString (request_url.Query).Get("oauth_verifier");

				if (string.IsNullOrEmpty (oauth_verifier)) {
					// authentication failed or error
					context.Response.StatusCode = 500;
					context.Response.StatusDescription = "Error";
					context.Response.Close();
					throw new ArgumentException ("oauth_verifier");
				} else {
					// authentication successfull
					context.Response.StatusCode = 200;
					using (var writer = new StreamWriter (context.Response.OutputStream)) {
						writer.WriteLine("<h1>Authorization successfull!</h1>Go back to the Tomboy application window.");
					}
					context.Response.Close();
					return oauth_verifier;
				}
			});
		
			// nasty: ignore any SSL warnings by creating this DummyCertificateManager
			// see http://mono-project.com/UsingTrustedRootsRespectfully for how to handle it right
			ServicePointManager.CertificatePolicy = new DummyCertificateManager ();

			// Rainy public demo server, see http://dynalon.github.io/Rainy/#!PUBLIC_SERVER.md for list of demo accounts
			string serverUrl = "https://rainy-demoserver.latecrew.de/";

			// connect to the server and obtain the token - this only has to be done ONCE
			IOAuthToken access_token = WebSyncServer.PerformTokenExchange (serverUrl, callbackUrl, callback_delegate);
			Console.WriteLine ("received access token {0} with secret key {1}", access_token.Token, access_token.Secret);
			// thats it - we can now save the access_token into permanent storage for future use (we need to store both,
			// the token and the secret!

			// Do NOT perform token exchange everytime you do a sync but always reuse token, i.e. read from disk!
			// Creating a token instance as an example here
			OAuthToken reused_access_token = new OAuthToken { Token = access_token.Token, Secret = access_token.Secret };

			// ---- CLEAN OUT ANY PREVIOUS NOTES ----
			// for this example we want to delete all notes present for the user already to be removed to get a clean
			// environment. The DeveloperServiceClient should not be used in production code!
			var developerService = new Tomboy.Sync.Web.Developer.DeveloperServiceClient (serverUrl, reused_access_token);
			developerService.ClearAllNotes ("testuser");

			// ---- PERFORM A SYNC WITH A REMOTE SERVER ---- 
			ISyncClient client = new FilesystemSyncClient (localEngine, localManifest);
			ISyncServer server = new WebSyncServer (serverUrl, reused_access_token);

			new SyncManager (client, server).DoSync ();
			Console.WriteLine ("SUCCESS: we have successfully synced the notes with the server!");

			// TODO: we still miss GUI hooks in tomboy-library to prompt for conflicts, show progress etc. :(
	
			// ---- CLEAN UP ----
			listener.Stop ();
			Console.WriteLine ("EXITING");
		}

		public static List<Note> GetSomeSampleNotes ()
		{
			var notes = new List<Note> ();
			
			Enumerable.Range (1, 5).ToList ().ForEach (i => {
				notes.Add (new Note () {
					CreateDate = DateTime.Now - new TimeSpan (i * 24, 0, 0),
					Title = "Sample Note " + i.ToString () + " Title",
					Text = "Sample Note " + i.ToString () + " Text"
				});
			});
			return notes;
		}
	}
}
