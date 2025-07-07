using DarkUI.Forms;
using Grimoire.Botting;
using Grimoire.Game;
using Grimoire.UI;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace Grimoire.Networking.Handlers
{
    public class HandlerGramielT2 : IJsonMessageHandler
    {
        public string[] HandledCommands { get; } = { "ct" };

        int crystalcount = 0;

        public async void Handle(JsonMessage message)
        {
            await Task.Delay(500);

            try
            {
                JArray anims = (JArray)message.DataObject["anims"];
                foreach (var anim in anims)
                {
                    string msg = anim?["msg"]?.ToString()?.ToLower();

                    switch (Player.EquippedClass.ToLower()) {
                        case "archpaladin":
                        case "lightcaster":
                            Player.AttackMonster("id.2");
                            break;
                        default:
                            Player.AttackMonster("id.3");
                            break;
                    }

                    if (msg.Contains("shattering"))
                    {
                        crystalcount++;
                        
                        if (crystalcount % 4 == 0)
                        {
                            while (Player.SkillAvailable("5") == 0)
                            {
                                Player.ForceUseSkill("5");
                                await Task.Delay(10);
                                Player.UseSkill("5");
                            }
                        }
                    }

                }
                JArray actions = (JArray)message.DataObject["a"];
                if (actions != null)
                {
                    foreach (var action in actions)
                    {
                        var auras = action["auras"] as JArray;
                        if (auras != null)
                        {
                            foreach (var aura in auras)
                            {
                                string msgOn = aura?["msgOn"]?.ToString()?.ToLower();
                                if (!string.IsNullOrEmpty(msgOn) && msgOn.Contains("gramielattempts"))
                                {
                                    Player.AttackMonster("id.1");
                                    await Task.Delay(5000);
                                }
                            }
                        }
                    }
                }
            }
            catch
            {

            }
        }
    }
}