using Grimoire.Game;
using Newtonsoft.Json.Linq;

namespace Grimoire.Networking.Handlers
{
    public class HandlerAutoZoneQueenIona : IJsonMessageHandler
    {
        public string[] HandledCommands { get; } = { "event" };

        public void Handle(JsonMessage message)
        {
            try
            {
                JObject args = (JObject)message.DataObject["args"];
                string zone = args["zoneSet"].ToString();
                int positiveChargeCount = Player.GetAuras(true, "Positive Charge");
                int negativeChargeCount = Player.GetAuras(true, "Negative Charge");

                switch (zone)
                {
                    case "A":
                        if (positiveChargeCount > 0)
                        {
                            Player.WalkToPoint("678", "310");
                        }
                        else
                        {
                            Player.WalkToPoint("217", "366");
                        }
                        break;
                    case "B":
                        if (negativeChargeCount > 0)
                        {
                            Player.WalkToPoint("217", "366");
                        }
                        else
                        {
                            Player.WalkToPoint("678", "310");
                        }
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