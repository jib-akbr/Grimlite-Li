using Grimoire.Game;
using Grimoire.UI;
using System.Threading.Tasks;

namespace Grimoire.Botting.Commands.Item
{
    // Compat shims for bots saved by Grimlite Rev (Rev declares these in Commands.Item).
    // Mapped onto Li's PacketSpammer form.
    public class CmdPacketlist : IBotCommand
    {
        public enum state
        {
            On,
            Off,
            Add,
            Remove,
            Clear,
            SetDelay
        }

        public string Packet
        {
            get;
            set;
        }

        public int Delay
        {
            get;
            set;
        }

        public state State
        {
            get;
            set;
        }

        public async Task Execute(IBotEngine instance)
        {
            string packet = instance.ResolveVars(Packet);
            PacketSpammer spammer = PacketSpammer.Instance;
            switch (State)
            {
                case state.On:
                    if (!spammer.lstPackets.Items.Contains(packet))
                        spammer.lstPackets.Items.Add(packet);
                    spammer.numDelay.Value = Delay;
                    spammer.btnStart.PerformClick();
                    break;

                case state.Off:
                    spammer.btnStop.PerformClick();
                    break;

                case state.Add:
                    if (!spammer.lstPackets.Items.Contains(packet))
                        spammer.lstPackets.Items.Add(packet);
                    break;

                case state.Remove:
                    spammer.lstPackets.Items.Remove(packet);
                    break;

                case state.Clear:
                    spammer.lstPackets.Items.Clear();
                    break;

                case state.SetDelay:
                    spammer.numDelay.Value = Delay;
                    break;
            }
            await Task.FromResult<object>(null);
        }

        public override string ToString()
        {
            switch (State)
            {
                case state.On:
                    return "Packetlist On";

                case state.Off:
                    return "Packetlist Off";

                case state.Add:
                    return "Add to Packetlist: " + Packet;

                case state.Remove:
                    return "Remove from Packetlist: " + Packet;

                case state.Clear:
                    return "Clear Packetlist";

                case state.SetDelay:
                    return "Set Packetlist's Delay to: " + Delay;
            }
            return "Packetlist";
        }
    }

    public class CmdPacketlist2 : IBotCommand
    {
        public int Type
        {
            get;
            set;
        }

        public string Packet
        {
            get;
            set;
        }

        public int Delay
        {
            get;
            set;
        }

        private static bool _spamOn;

        public async Task Execute(IBotEngine instance)
        {
            string packet = instance.ResolveVars(Packet);
            PacketSpammer spammer = PacketSpammer.Instance;
            switch (Type)
            {
                case 0:
                    _spamOn = false;
                    spammer.btnStop.PerformClick();
                    Player.MoveToCell(Player.Cell, Player.Pad);
                    break;
                case 1:
                    _spamOn = true;
                    if (!spammer.lstPackets.Items.Contains(packet))
                        spammer.lstPackets.Items.Add(packet);
                    spammer.numDelay.Value = Delay;
                    spammer.btnStart.PerformClick();
                    break;
                default:
                    _spamOn = !_spamOn;
                    if (_spamOn)
                    {
                        if (!spammer.lstPackets.Items.Contains(packet))
                            spammer.lstPackets.Items.Add(packet);
                        spammer.numDelay.Value = Delay;
                        spammer.btnStart.PerformClick();
                    }
                    else
                    {
                        spammer.btnStop.PerformClick();
                        Player.MoveToCell(Player.Cell, Player.Pad);
                    }
                    break;
            }
            await Task.FromResult<object>(null);
        }

        public override string ToString()
        {
            switch (Type)
            {
                case 0:
                    return $"Spam packet: Off";
                case 1:
                    return $"Spam packet: On, {Packet}";
                default:
                    return "Spam packet";
            }
        }
    }
}
