using Grimoire.Networking;
using Grimoire.UI;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Windows.Forms;

namespace Grimoire
{
	internal static class Program
	{
		public static readonly string Version = "Li 1.9.1";
		public static readonly string ReleaseDate = "02-12-2025";
		public static string PluginsPath { get; private set; }
		public static Tools.Plugins.PluginManager PluginsManager { get; private set; }
		
		// Store command-line arguments for spawned instances
		public static string[] StartupArgs { get; private set; }
		
		// Flag to indicate if this is a spawned instance
		public static bool IsSpawnedInstance { get; private set; }

		[STAThread]
		private static void Main(string[] args)
		{
			// Store arguments immediately
			StartupArgs = args;
			
			// Check if this is a spawned instance
			IsSpawnedInstance = (args != null && args.Length > 0);
			
			// Process command-line arguments if provided
			if (args != null && args.Length > 0)
			{
				ParseAndSetupAutoLogin(args);
			}
			
			try
			{
				Program.TryCreateDirectory(Program.PluginsPath = Path.Combine(Application.StartupPath, "Plugins"));
				if (FindAvailablePort(out int port))
				{
					Proxy.Instance.ListenerPort = port;
					PluginsManager = new Tools.Plugins.PluginManager();
					Application.EnableVisualStyles();
					Application.SetCompatibleTextRenderingDefault(defaultValue: false);
					Application.Run(new Root());
					Program.PluginsManager.UnloadAll();
					Proxy.Instance.Stop(appClosing: false);
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show(
					"A fatal error occurred while starting Grimlite Li:\n\n" + ex,
					"Grimlite Li",
					MessageBoxButtons.OK,
					MessageBoxIcon.Error);
			}
		}
		
		private static void ParseAndSetupAutoLogin(string[] args)
		{
			try
			{
				string username = null;
				string password = null;
				string server = null;
				string script = null;

				// Parse command-line arguments
				for (int i = 0; i < args.Length; i++)
				{
					if (args[i].StartsWith("--username="))
						username = args[i].Substring("--username=".Length).Trim('"');
					else if (args[i].StartsWith("--password="))
						password = args[i].Substring("--password=".Length).Trim('"');
					else if (args[i].StartsWith("--server="))
						server = args[i].Substring("--server=".Length).Trim('"');
					else if (args[i].StartsWith("--script="))
						script = args[i].Substring("--script=".Length).Trim('"');
				}

				// Log what we received for debugging
				System.Diagnostics.Debug.WriteLine($"[ParseAndSetupAutoLogin] Received arguments:");
				System.Diagnostics.Debug.WriteLine($"[ParseAndSetupAutoLogin]   Username: {username ?? "(null)"}");
				System.Diagnostics.Debug.WriteLine($"[ParseAndSetupAutoLogin]   Password: {(string.IsNullOrEmpty(password) ? "(null)" : new string('*', password.Length))}");
				System.Diagnostics.Debug.WriteLine($"[ParseAndSetupAutoLogin]   Server: {server ?? "(null)"}");
				System.Diagnostics.Debug.WriteLine($"[ParseAndSetupAutoLogin]   Script: {script ?? "(null)"}");

				// Set credentials in OptionsManager (static properties)
				if (!string.IsNullOrEmpty(username))
				{
					Botting.OptionsManager.LoginUsername = username;
				}

				if (!string.IsNullOrEmpty(password))
				{
					Botting.OptionsManager.LoginPassword = password;
				}

				if (!string.IsNullOrEmpty(script))
				{
					Botting.OptionsManager.AutoLoadScriptPath = script;
				}

				// Set server override if specified
				if (!string.IsNullOrEmpty(server))
				{
					// Create a Server object with the name
					// The IP will be resolved by the game when connecting
					Networking.Proxy.Instance.DestinationServerOverride = new Game.Data.Server
					{
						Name = server,
						Ip = "game.aq.com",
						Port = 443,
						IsOnline = true,
						IsMemberOnly = false,
						PlayerCount = 0
					};
				}
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"[ParseAndSetupAutoLogin] Error: {ex.Message}");
			}
		}

		private static void TryCreateDirectory(string dir)
		{
			try
			{
				Directory.CreateDirectory(dir);
			}
			catch (UnauthorizedAccessException)
			{
				MessageBox.Show(string.Format("Failed to create directory: {0}\nAccess denied", dir));
			}
			catch (PathTooLongException)
			{
				MessageBox.Show(string.Format("Failed to create directory: {0}\nThe specified path is too long.", dir) + "Try moving the Grimoire directory out of the current directory");
			}
			catch (Exception ex)
			{
				MessageBox.Show(string.Format("Failed to create directory {0}\n{1}", dir, ex.Message));
			}
		}

		private static bool FindAvailablePort(out int port)
		{
			Random random = new Random();
			IPGlobalProperties iPGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();
			TcpConnectionInformation[] activeTcpConnections;
			IPEndPoint[] activeTcpListeners;
			try
			{
				activeTcpConnections = iPGlobalProperties.GetActiveTcpConnections();
				activeTcpListeners = iPGlobalProperties.GetActiveTcpListeners();
			}
			catch (NetworkInformationException)
			{
				// On some systems, querying active TCP connections/listeners can fail.
				// Instead of aborting startup, fall back to a random port and continue.
				port = random.Next(1001, 65535);
				return true;
			}
			int randPort;
			TcpConnectionInformation tcpConnectionInformation;
			IPEndPoint iPEndPoint;
			do
			{
				randPort = random.Next(1001, 65535);
				tcpConnectionInformation = activeTcpConnections.FirstOrDefault((TcpConnectionInformation c) => c.LocalEndPoint.Port == randPort);
				iPEndPoint = activeTcpListeners.FirstOrDefault((IPEndPoint l) => l.Port == randPort);
			}
			while (tcpConnectionInformation != null || iPEndPoint != null);
			port = randPort;
			return true;
		}
	}
}