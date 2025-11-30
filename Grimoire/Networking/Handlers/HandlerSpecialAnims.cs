using Grimoire.Botting;
using Newtonsoft.Json.Linq;

namespace Grimoire.Networking.Handlers
{
    public class HandlerSpecialAnims : IJsonMessageHandler
    {
        public string[] HandledCommands { get; } = new string[] { "ct" };

        public void Handle(JsonMessage message)
        {
            try
            {
                if (message.DataObject?["anims"] != null)
                {
                    JArray anims = (JArray)message.DataObject["anims"];
                    if (anims != null)
                    {
                        foreach (JObject anim in anims)
                        {
                            string msg = anim?["msg"]?.ToString()?.ToLower();
                            if (msg != null && !string.IsNullOrEmpty(msg))
                            {
                                // Store animation message for bot statement commands
                                Configuration.LastAnimationMessage = msg;
                                Configuration.AnimationTriggered = true;
                            }
                        }
                    }
                }
            }
            catch
            {
                // Silently fail to avoid breaking game flow
            }
        }
    }
}
