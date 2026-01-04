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
                // Check for animation messages in the anims array
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

                // Check for aura msgOn messages in the auras array (inside "a" actions)
                if (message.DataObject?["a"] != null)
                {
                    JArray actions = (JArray)message.DataObject["a"];
                    if (actions != null)
                    {
                        foreach (JObject action in actions)
                        {
                            if (action?["auras"] != null)
                            {
                                JArray auras = (JArray)action["auras"];
                                if (auras != null)
                                {
                                    foreach (JObject aura in auras)
                                    {
                                        string msgOn = aura?["msgOn"]?.ToString();
                                        if (msgOn != null && !string.IsNullOrEmpty(msgOn))
                                        {
                                            // Remove the @ prefix if present, then store
                                            if (msgOn.StartsWith("@"))
                                                msgOn = msgOn.Substring(1);

                                            msgOn = msgOn.ToLower();
                                            Configuration.LastAnimationMessage = msgOn;
                                            Configuration.AnimationTriggered = true;
                                        }
                                    }
                                }
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
