using Grimoire.Game;
using Newtonsoft.Json.Linq;

namespace Grimoire.Networking.Handlers
{
	public class HandlerAutoZoneQueenIona: IJsonMessageHandler
	{
		public string[] HandledCommands { get; } = { "event" };

		public void Handle(JsonMessage message)
		{
			try
			{
				JObject args = (JObject)message.DataObject["args"];
				string zone = args["zoneSet"].ToString();
				switch (zone)
				{
					case "A":
						Player.WalkToPoint("217", "366");
						break;
					case "B":
						Player.WalkToPoint("867", "308");
						break;
					default:
						Player.WalkToPoint("485", "351");
						break;
				}
			}
			catch { }
		}
	}
}
