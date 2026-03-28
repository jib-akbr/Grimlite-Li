using Grimoire.Game;
using Grimoire.Tools;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Grimoire.Botting.Commands.Misc
{
    public class CmdLoadBot : IBotCommand
    {
        public string BotFileName
        {
            get;
            set;
        }

        public string BotFilePath
        {
            get;
            set;
        }
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)]
        public bool PassDropList
        {
            get;
            set;
        } = true;
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)]
        public bool PassSkillPreset
        {
            get;
            set;
        } = false;
		
        public async Task Execute(IBotEngine instance)
        {
            //string name = instance.IsVar(BotFileName) ? Configuration.Tempvariable[instance.GetVar(BotFileName)] : BotFileName;
            string path = instance.IsVar(BotFilePath) ? Configuration.Tempvariable[instance.GetVar(BotFilePath)] : BotFilePath;
            if (File.Exists(path))
            {
                try
                {
                    string value;
                    using (TextReader reader = new StreamReader(path))
                    {
                        value = await reader.ReadToEndAsync();
                    }
                    JsonSerializerSettings serializerSettings = new JsonSerializerSettings
                    {
                        DefaultValueHandling = DefaultValueHandling.Include,
                        //NullValueHandling = NullValueHandling.Ignore,
                        TypeNameHandling = TypeNameHandling.All
                    };
                    //Save old skill if config does
                    var oldSkills = PassSkillPreset && instance.Configuration?.Skills.Count > 0 ?
                        instance.Configuration?.Skills : null;

                    var oldDropList = PassDropList ? instance.Configuration?.Drops : null;

                    Configuration newConfiguration = JsonConvert.DeserializeObject<Configuration>(value, serializerSettings);

                    int i = instance.CurrentConfiguration;

                    if (newConfiguration != null && newConfiguration?.Commands?.Count > 0)
                    {
                        //Previous Config
                        if (!Bot.Configurations.ContainsKey(i))
                            Bot.Configurations.Add(i, instance.Configuration);
                        else
                            Bot.Configurations[i] = instance.Configuration;
                        //Previous Index
                        if (!Bot.OldIndex.ContainsKey(i))
                            Bot.OldIndex.Add(i, instance.Index);
                        else
                            Bot.OldIndex[i] = instance.Index;

                        //This handle if u should change skillset for each gbot or not
                        if (oldSkills != null)
                            newConfiguration.Skills = oldSkills;
                        else
                            BotData.BotSkill = null; //This is needed to make Skillset dynamic

                        var set = new HashSet<string>(newConfiguration.Drops ?? Enumerable.Empty<string>()
                            , StringComparer.OrdinalIgnoreCase);

                        if (oldDropList != null)
                            set.UnionWith(oldDropList); //Merge old with new droplist

                        newConfiguration.Drops = set.ToList();

                        //Apply/Load new config
                        instance.Configuration = newConfiguration;
                        instance.Index = -1;
                        instance.LoadBankItems();
                        instance.LoadAllQuests();
                        instance.StartQuestList();
                        instance.CurrentConfiguration++;
                    }
                }
                catch (Exception e) { MessageBox.Show(e.ToString()); }
            }
        }

        public override string ToString()
        {
            return "Load bot: " + BotFileName;
        }
    }
}