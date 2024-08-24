using Grimoire.Game;
using Newtonsoft.Json.Linq;
using System;

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
                Console.WriteLine("Zone: " + zone);

                bool isPositive = positiveChargeCount > 0;
                bool isNegative = negativeChargeCount > 0;

                switch (zone)
                {
                    case "A":
                        if (!isPositive || isNegative)
                            Player.WalkToPoint("609", "337");
                        else
                            Player.WalkToPoint("411", "342");
                        break;
                    case "B":
                        if (!isPositive || isNegative)
                            Player.WalkToPoint("411", "342");
                        else
                            Player.WalkToPoint("609", "337");
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