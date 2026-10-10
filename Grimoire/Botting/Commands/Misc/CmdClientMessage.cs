using Grimoire.Game;
using Grimoire.Networking;
using Grimoire.Tools;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Grimoire.Botting.Commands.Misc
{
    public class CmdClientMessage : RegularExpression, IBotCommand
    {
        public string Messages
        {
            get;
            set;
        }

        public bool IsWarning
        {
            get;
            set;
        } = false;

        public string MsgType
        {
            get;
            set;
        } = "server";
        public string SenderName
        {
            get;
            set;
        } = "SERVER";

        public string HexColor
        {
            get;
            set;
        } //= "FFC0CB";

        public async Task Execute(IBotEngine instance)
        {
            string Messages = instance.ResolveVars(this.Messages);
            //(instance.IsVar(this.Messages) ? Configuration.Tempvariable[instance.GetVar(this.Messages)] : this.Messages);
            string _MsgType = instance.ResolveVars(MsgType);
            if (IsWarning)
                _MsgType = "warning";

            if (!string.IsNullOrEmpty(HexColor))
                Messages = $"<font color='#{HexColor}'>{Messages}</font>";

            string[] args = { _MsgType, Messages, SenderName, "", "", "0" };

            Flash.CallGameFunction2("chatF.pushMsg", args);
            //Flash.CallGameFunction2("chatF.pushMsg", $"{_MsgType},{Messages},{SenderName},,,0");
            /*
            return;
            if (!IsWarning)
            {
                await Proxy.Instance.SendToClient($"%xt%server%-1%{Messages}%");
            }
            else
            {
                await Proxy.Instance.SendToClient($"%xt%warning%-1%{Messages}%");
                // Flash.CallGameFunction2("chatF.pushMsg", $"warning,{Messages},SERVER,,0");
            }*/
        }

        public override string ToString()
        {
            string warn = IsWarning ? "warning" : "info";
            return $"Send {warn} : {Messages}";
        }
    }
}
