using Grimoire.Networking;
using Grimoire.UI;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Windows.Forms;
using DarkUI.Forms;
using Microsoft.Win32;
using System.Threading.Tasks;
using System.Net.Http;
using System.Diagnostics;

namespace Grimoire
{
	internal static class Program
	{
		public static readonly string Version = "1.9.4b";
		public static readonly string ReleaseDate = "28-10-2026";
		public static string PluginsPath { get; private set; }
		public static Tools.Plugins.PluginManager PluginsManager { get; private set; }

		[STAThread]
		private static void Main()
		{
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
		
		/*
		Below are copied code from Ganku's GrimRev 
		version checking
		*/
		public static async Task CheckForLatestBuild()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            bool isOutdated = false;

            try
            {
                using (HttpClient client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("user-agent", "Mozilla/5.0 (Windows; U; MSIE 9.0; Windows NT 9.0; en-US)");

                    var result = await client.GetStringAsync("https://github.com/jib-akbr/Grimlite-Li/releases/latest");
                    isOutdated = !result.Contains($"v{Version.Substring(0,5)}");
                }
            }
            catch
            {
                return;
            }

            if (!isOutdated)
            {
                return;
            }
            
            DialogResult dialogResult = DarkMessageBox.Show(new Form 
			{ TopMost = true, StartPosition = FormStartPosition.CenterScreen }, 
				"A new update for Grimlite Li has been released! \n" +
				"Would you like to download the latest version?", 
				$"New update is available!", 
				MessageBoxButtons.YesNo, MessageBoxIcon.Error);
            
            if (dialogResult == DialogResult.Yes)
            {
                Process.Start("https://github.com/jib-akbr/Grimlite-Li/releases/latest");
            }
        }

        public static void CheckForFlashPlayer()
        {
            string flashVer = string.Empty;
            RegistryKey subKey = Registry.ClassesRoot.OpenSubKey(@"ShockwaveFlash.ShockwaveFlash\CurVer");
            
            if (subKey != null)
            {
                flashVer = subKey.GetValue(null).ToString();
            }
            else
            {
                DarkMessageBox.Show(
					new Form 
					{ 
						TopMost = true, 
						StartPosition = FormStartPosition.CenterScreen 
					}, 
					"You have to install the Adobe Flash Player plugin (for ActiveX) in order for the game screen to load.", 
					$"Failed loading the game!", 
					MessageBoxIcon.Error);
            }
        }
	}
}