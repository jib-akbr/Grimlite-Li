using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Grimoire.Game;
using Grimoire.Networking;

namespace Grimoire.Botting.Commands.Map
{
    /// <summary>
    /// Zone-based walk command that listens for zone events and moves when zone matches.
    /// Registers as a handler and returns immediately without blocking.
    /// Supports multiple zone/move pairs - when an event message arrives with args.zoneSet matching any Zone,
    /// it walks to the corresponding X,Y coordinates and unregisters.
    /// </summary>
    public class CmdZoneHandler : IBotCommand, IJsonMessageHandler
    {
        private string _targetZone = "";
        private string _moveX = "0";
        private string _moveY = "0";
        private bool _zoneMatched = false;
        private bool _movedToPosition = false;
        private string _currentZone = "";

        [JsonProperty("Zone", Order = 1)]
        public string Zone { get; set; } = "";

        [JsonProperty("Default X,Y", Order = 2)]
        public string Default { get; set; } = "";

        [JsonProperty("Move X,Y", Order = 3)]
        public string Move { get; set; } = "0,0";

        [JsonProperty("ExtraZones", Order = 4)]
        public string ExtraZones { get; set; } = "[]";

        public string[] HandledCommands => new[] { "event" };

        public Task Execute(IBotEngine instance)
        {
            try
            {
                _zoneMatched = false;

                // Register as handler to listen for zone events and return immediately
                Proxy.Instance.RegisterHandler(this);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CmdZoneHandler error: {ex.Message}");
            }

            return Task.FromResult<object>(null);
        }

        public void Handle(JsonMessage message)
        {
            try
            {
                if (_zoneMatched) return;

                // Check for event message with zoneSet
                JToken dataObj = message.DataObject;
                if (dataObj != null)
                {
                    string cmd = dataObj["cmd"]?.ToString();
                    if (cmd == "event")
                    {
                        JObject args = dataObj["args"] as JObject;
                        if (args != null)
                        {
                            string zoneSet = args["zoneSet"]?.ToString() ?? "";
                            
                            // Check primary zone
                            bool isZoneMatch = zoneSet.Equals(Zone, StringComparison.OrdinalIgnoreCase);
                            
                            // If not matched in primary, check extra zones
                            if (!isZoneMatch)
                            {
                                try
                                {
                                    JArray extraZones = JArray.Parse(ExtraZones ?? "[]");
                                    foreach (var zone in extraZones)
                                    {
                                        string zoneLabel = zone["Zone"]?.ToString() ?? "";
                                        if (zoneSet.Equals(zoneLabel, StringComparison.OrdinalIgnoreCase))
                                        {
                                            isZoneMatch = true;
                                            break;
                                        }
                                    }
                                }
                                catch { }
                            }
                            
                            // Zone just became active - walk to Move position
                            if (isZoneMatch && !_movedToPosition)
                            {
                                _movedToPosition = true;
                                _currentZone = zoneSet;
                                
                                // Find the corresponding move coordinates
                                string moveValue = "0,0";
                                if (zoneSet.Equals(Zone, StringComparison.OrdinalIgnoreCase))
                                {
                                    moveValue = Move;
                                }
                                else
                                {
                                    try
                                    {
                                        JArray extraZones = JArray.Parse(ExtraZones ?? "[]");
                                        foreach (var zone in extraZones)
                                        {
                                            string zoneLabel = zone["Zone"]?.ToString() ?? "";
                                            if (zoneSet.Equals(zoneLabel, StringComparison.OrdinalIgnoreCase))
                                            {
                                                moveValue = zone["Move"]?.ToString() ?? "0,0";
                                                break;
                                            }
                                        }
                                    }
                                    catch { }
                                }
                                
                                // Walk to Move position
                                string[] coords = moveValue.Split(',');
                                string x = coords.Length > 0 ? coords[0].Trim() : "0";
                                string y = coords.Length > 1 ? coords[1].Trim() : "0";
                                Player.WalkToPoint(x, y);
                                return;
                            }
                            
                            // Zone is no longer active - walk to Default position
                            if (!isZoneMatch && _movedToPosition && !string.IsNullOrWhiteSpace(Default))
                            {
                                string[] defaultCoords = Default.Split(',');
                                string defX = defaultCoords.Length > 0 ? defaultCoords[0].Trim() : "0";
                                string defY = defaultCoords.Length > 1 ? defaultCoords[1].Trim() : "0";
                                Player.WalkToPoint(defX, defY);
                                
                                _zoneMatched = true;
                                _movedToPosition = false;
                                
                                try
                                {
                                    Proxy.Instance.UnregisterHandler(this);
                                }
                                catch { }
                                return;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CmdZoneHandler.Handle error: {ex.Message}");
            }
        }

        public override string ToString()
        {
            return $"Zone {Zone} Handler";
        }
    }
}
