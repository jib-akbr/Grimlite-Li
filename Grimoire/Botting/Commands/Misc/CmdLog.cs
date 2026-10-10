using Grimoire.Game;
using Grimoire.Game.Data;
using Grimoire.Networking;
using Grimoire.Tools;
using Grimoire.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Grimoire.Botting.Commands.Misc
{
    public class CmdLog : IBotCommand
    {
        public string Text
        {
            get;
            set;
        }

        public bool Debug
        {
            get;
            set;
        } = false;

        public bool ModMessage
        {
            get;
            set;
        } = false;

        private bool Clear
        {
            get;
            set;
        } = false;

        public async Task Execute(IBotEngine instance)
        {
            string filteredText = Text;
            filteredText = filteredText.Replace("{USERNAME}", Player.Username);
            filteredText = filteredText.Replace("{MAP}", Player.Map).Replace("{ROOM_ID}", World.RoomId.ToString()).Replace("{ROOM_NUM}", World.RoomNumber.ToString());
            filteredText = filteredText.Replace("{GOLD}", Player.Gold.ToString());
            filteredText = filteredText.Replace("{LEVEL}", Player.Level.ToString());
            filteredText = filteredText.Replace("{CELL}", Player.Cell).Replace("{PAD}", Player.Pad);
            filteredText = filteredText.Replace("{HEALTH}", Player.Health.ToString()).Replace("{MANA}", Player.Mana.ToString());
            filteredText = filteredText.Replace("{TIME}", $"{DateTime.Now:HH:mm:ss}");
            filteredText = filteredText.Replace("{TIME: 12}", $"{DateTime.Now:hh:mm:ss tt}");
            filteredText = filteredText.Replace("{TIME: 24}", $"{DateTime.Now:HH:mm:ss}");

            filteredText = new Regex(
                "{ITEM:\\s*(.*?)}", RegexOptions.IgnoreCase).Replace(filteredText, (Match m) =>
                $"{(Player.Inventory.Items.Find((InventoryItem x) => x.Name == m.Groups[1].Value) ?? new InventoryItem()).Quantity}");

            filteredText = new Regex(
                "{ITEM MAX:\\s*(.*?)}", RegexOptions.IgnoreCase).Replace(filteredText, (Match m) =>
                $"{(Player.Inventory.Items.Find((InventoryItem x) => x.Name == m.Groups[1].Value) ?? new InventoryItem()).MaxStack}");

            filteredText = new Regex(
                "{REP XP:\\s*(.*?)}", RegexOptions.IgnoreCase).Replace(filteredText, (Match m) =>
                $"{(Player.Factions.Find((Faction x) => x.Name == m.Groups[1].Value) ?? new Faction()).Rep}/" +
                $"{(Player.Factions.Find((Faction x) => x.Name == m.Groups[1].Value) ?? new Faction()).RequiredRep}");

            filteredText = new Regex(
                "{REP CURRENT:\\s*(.*?)}", RegexOptions.IgnoreCase).Replace(filteredText, (Match m) =>
                $"{(Player.Factions.Find((Faction x) => x.Name == m.Groups[1].Value) ?? new Faction()).Rep}");

            filteredText = new Regex(
                "{REP REMAINING:\\s*(.*?)}", RegexOptions.IgnoreCase).Replace(filteredText, (Match m) =>
                $"{(Player.Factions.Find((Faction x) => x.Name == m.Groups[1].Value) ?? new Faction()).RemainingRep}");

            filteredText = new Regex(
                "{REP REQUIRED:\\s*(.*?)}", RegexOptions.IgnoreCase).Replace(filteredText, (Match m) =>
                $"{(Player.Factions.Find((Faction x) => x.Name == m.Groups[1].Value) ?? new Faction()).RequiredRep}");

            filteredText = new Regex(
                "{REP RANK:\\s*(.*?)}", RegexOptions.IgnoreCase).Replace(filteredText, (Match m) =>
                $"{(Player.Factions.Find((Faction x) => x.Name == m.Groups[1].Value) ?? new Faction()).Rank}");

            filteredText = new Regex(
                "{REP TOTAL:\\s*(.*?)}", RegexOptions.IgnoreCase).Replace(filteredText, (Match m) =>
                $"{(Player.Factions.Find((Faction x) => x.Name == m.Groups[1].Value) ?? new Faction()).TotalRep}");

            filteredText = new Regex(
                "{INT VALUE:\\s*(.*?)}", RegexOptions.IgnoreCase).Replace(filteredText, (Match m) =>
                $"{Configuration.Tempvalues[m.Groups[1].Value]}");

            string[] parts = filteredText.Split('\\');
            string cleanMsg = parts[0];
            List<string> colors = parts.Skip(1).ToList();
            int colorIndex = 0;

            filteredText = Regex.Replace(cleanMsg, @">(.*?)<", match =>
            {
                string raw = match.Groups[1].Value;

                string color = colorIndex < colors.Count
                    ? colors[colorIndex++]
                    : "FFFFFF"; 

                color = color.TrimStart('#');

                return $"<font color='#{color}'>{raw}</font>";
            });

            cleanMsg = cleanMsg.Replace(">", "").Replace("<", "");

            if (Clear)
                LogForm.Instance.txtLogDebug.Clear();
            else if (Debug)
                LogForm.Instance.AppendDebug(cleanMsg, doAnyway: true);
            else
                LogForm.Instance.AppendScript(cleanMsg);

            if (ModMessage)
            {
                //Flash.CallGameFunction2("chatF.pushMsg", $"moderator,{text},SERVER,,,0");
                Flash.CallGameFunction2("chatF.pushMsg", new string[] { "server", filteredText, "SERVER", "", "", "0" });
                //await Proxy.Instance.SendToClient($"%xt%moderator%-1%{text}%");
            }
        }

        public override string ToString()
        {
            string typetxt = Debug ? "Debug" : "Script";
            string action = Text.Contains("{CLEAR}") ? "Clear" : "Log";
            return $"{action} {typetxt}: {Text}";
            //return Clear ? $"Clear {typetxt}" : $"Log {typetxt}: {Text}";
        }
    }
}
