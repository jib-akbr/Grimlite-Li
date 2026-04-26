using DarkUI.Controls;
using DarkUI.Forms;
using Grimoire.Botting.Commands.Map;
using Grimoire.Botting.Commands.Misc;
using Grimoire.Botting.Commands.Misc.Statements;
using Grimoire.Botting.Commands.Quest;
using Grimoire.Game;
using Grimoire.Game.Data;
using Grimoire.Properties;
using Grimoire.Tools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Properties;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Grimoire.UI
{
    public class UserFriendlyCommandEditor : DarkForm
    {
        private IContainer components;
        private DarkButton btnOK;
        private ToolStripContainer toolStripContainer1;
        private SplitContainer splitContainer1;
        private DarkButton btnRawCommand;
        private DarkButton btnCancel;
        private DarkButton btnGetInfo;

        private static object cmdObj
        {
            get;
            set;
        }

        private static UserFriendlyCommandEditor commandEditor
        {
            get;
            set;
        }

        public static string cmd
        {
            get;
            set;
        }

        private static readonly JsonSerializerSettings _questSerializerSettings = new JsonSerializerSettings
        {
            DefaultValueHandling = DefaultValueHandling.Ignore,
            NullValueHandling = NullValueHandling.Include,
            TypeNameHandling = TypeNameHandling.All
        };

        private static readonly JsonSerializerSettings _serializerSettings = new JsonSerializerSettings
        {
            DefaultValueHandling = DefaultValueHandling.Include,
            NullValueHandling = NullValueHandling.Include,
            TypeNameHandling = TypeNameHandling.All
        };

        private List<StatementCommand> statementCommands;

        private UserFriendlyCommandEditor()
        {
            InitializeComponent();
            statementCommands = JsonConvert.DeserializeObject<List<StatementCommand>>(Resources.statementcmds, _serializerSettings);
        }

        private void RawCommandEditor_Load(object sender, EventArgs e)
        {

        }

        private void txtCmd_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Return:
                    btnOK.PerformClick();
                    break;

                case Keys.Escape:
                    btnCancel.PerformClick();
                    break;
            }
        }

        private static int AddZoneEntry(Panel panel, int yPos, string label, string x, string y, int index, dynamic zones)
        {
            // Zone Label
            panel.Controls.Add(new DarkLabel()
            {
                Text = $"Zone {(char)('A' + index)}",
                Size = new System.Drawing.Size(50, 20),
                Location = new System.Drawing.Point(10, yPos + 2),
                Anchor = AnchorStyles.Left | AnchorStyles.Top
            });
            
            var tbLabel = new DarkTextBox()
            {
                Name = $"tbZoneLabel{index}",
                Text = label,
                Size = new System.Drawing.Size(70, 20),
                Location = new System.Drawing.Point(65, yPos),
                Anchor = AnchorStyles.Left | AnchorStyles.Top,
                MaxLength = 10
            };
            panel.Controls.Add(tbLabel);
            
            // X coordinate
            var tbX = new DarkTextBox()
            {
                Name = $"tbZoneX{index}",
                Text = x,
                Size = new System.Drawing.Size(60, 20),
                Location = new System.Drawing.Point(140, yPos),
                Anchor = AnchorStyles.Left | AnchorStyles.Top
            };
            panel.Controls.Add(tbX);
            
            // Y coordinate
            var tbY = new DarkTextBox()
            {
                Name = $"tbZoneY{index}",
                Text = y,
                Size = new System.Drawing.Size(60, 20),
                Location = new System.Drawing.Point(205, yPos),
                Anchor = AnchorStyles.Left | AnchorStyles.Top
            };
            panel.Controls.Add(tbY);
            
            // Remove button (X) - only for zones not at index 0
            if (index < zones?.Count ?? 0)
            {
                var btnRemove = new DarkUI.Controls.DarkButton()
                {
                    Text = "✕",
                    Size = new System.Drawing.Size(22, 20),
                    Location = new System.Drawing.Point(270, yPos),
                    Anchor = AnchorStyles.Right | AnchorStyles.Top,
                    ForeColor = System.Drawing.Color.FromArgb(220, 100, 100)
                };
                btnRemove.Click += (s, e) =>
                {
                    zones.RemoveAt(index);
                };
                panel.Controls.Add(btnRemove);
            }
            
            return yPos + 30;
        }

        public static string Show(object obj)
        {
            cmdObj = obj;
            var serializer = obj.GetType() == typeof(CmdCompleteQuest) || obj.GetType() == typeof(CmdAcceptQuest) ? _questSerializerSettings : _serializerSettings;
            //MessageBox.Show(obj.GetType().ToString());
            JObject content = JObject.Parse(JsonConvert.SerializeObject(obj, serializer));
            using (commandEditor = new UserFriendlyCommandEditor())
            {
                int currentY = 13;
                int count = 0;
                // Skip these fields by default, but include them for commands that use them
                List<string> skipList = new List<string> { "Tag", "Description1", "Description2", "$type", "Value3", "TauntOrder", "Delay", "Label", "Value4", "Value5", "Value6", "ExtraAuras" };
                
                // CmdSpecialAnims needs Value3, TauntOrder, Delay, and Label
                if (obj.GetType().Name == "CmdSpecialAnims")
                {
                    skipList.Remove("Value3");
                    skipList.Remove("TauntOrder");
                    skipList.Remove("Delay");
                    skipList.Remove("Label");
                }
                
                // CmdBalanceHP needs Label for thresholds and Value3 for skill
                if (obj.GetType().Name == "CmdBalanceHP")
                {
                    skipList.Remove("Label");
                    skipList.Remove("Value3");
                }
                
                // Aura commands need Value3 for skill field and Value4-6 for multi-aura support
                if (obj.GetType().Name == "CmdPlayerAuraGreaterThan" || obj.GetType().Name == "CmdPlayerAuraLessThan" || 
                    obj.GetType().Name == "CmdPlayerAuraEquals" || obj.GetType().Name == "CmdTargetAuraGreaterThan" ||
                    obj.GetType().Name == "CmdTargetAuraLessThan" || obj.GetType().Name == "CmdTargetAuraEquals")
                {
                    skipList.Remove("Value3");
                    skipList.Remove("Value4");
                    skipList.Remove("Value5");
                    skipList.Remove("Value6");
                }
                
                // Zone Handler needs special handling for Zone, Move, ExtraZones
                if (obj.GetType().Name == "CmdZoneHandler")
                {
                    skipList.Add("Zone"); // Handle specially
                    skipList.Add("Default X,Y"); // Handle specially
                    skipList.Add("Move X,Y"); // Handle specially
                    skipList.Add("ExtraZones"); // Handle specially
                    skipList.Add("HandledCommands"); // Don't show in UI
                }
                
                string[] skip = skipList.ToArray();
                Dictionary<string, KeyValuePair<DarkLabel, DarkTextBox>> currentVars = new Dictionary<string, KeyValuePair<DarkLabel, DarkTextBox>>();
                bool isAuraCommand = obj.GetType().Name == "CmdPlayerAuraGreaterThan" || obj.GetType().Name == "CmdPlayerAuraLessThan" || 
                                     obj.GetType().Name == "CmdPlayerAuraEquals" || obj.GetType().Name == "CmdTargetAuraGreaterThan" ||
                                     obj.GetType().Name == "CmdTargetAuraLessThan" || obj.GetType().Name == "CmdTargetAuraEquals";
                
                bool isZoneHandlerCommand = obj.GetType().Name == "CmdZoneHandler";
                
                int multiAuraStartY = -1;
                DarkCheckBox chkMultipleAuras = null;
                Panel pnlMultipleAuras = null;
                
                // Setup Zone Handler UI if this is a CmdZoneHandler
                if (isZoneHandlerCommand)
                {
                    // Zone field (Zone 1)
                    commandEditor.Controls.Add(new DarkLabel()
                    {
                        Text = "Zone",
                        Size = new System.Drawing.Size(60, 20),
                        Location = new System.Drawing.Point(25, currentY + 2),
                        Anchor = AnchorStyles.Left | AnchorStyles.Top
                    });
                    
                    var tbZone = new DarkTextBox()
                    {
                        Name = "tbZoneField",
                        Text = content["Zone"]?.ToString() ?? "",
                        Size = new System.Drawing.Size(160, 20),
                        Location = new System.Drawing.Point(90, currentY),
                        Anchor = AnchorStyles.Right | AnchorStyles.Top | AnchorStyles.Left
                    };
                    commandEditor.Controls.Add(tbZone);
                    currentVars.Add("Zone", new KeyValuePair<DarkLabel, DarkTextBox>(null, tbZone));
                    currentY += 35;
                    
                    // Default X,Y field
                    commandEditor.Controls.Add(new DarkLabel()
                    {
                        Text = "Default X,Y",
                        Size = new System.Drawing.Size(60, 20),
                        Location = new System.Drawing.Point(25, currentY + 2),
                        Anchor = AnchorStyles.Left | AnchorStyles.Top
                    });
                    
                    var tbDefault = new DarkTextBox()
                    {
                        Name = "tbDefaultField",
                        Text = content["Default X,Y"]?.ToString() ?? "",
                        Size = new System.Drawing.Size(160, 20),
                        Location = new System.Drawing.Point(90, currentY),
                        Anchor = AnchorStyles.Right | AnchorStyles.Top | AnchorStyles.Left
                    };
                    commandEditor.Controls.Add(tbDefault);
                    currentVars.Add("Default", new KeyValuePair<DarkLabel, DarkTextBox>(null, tbDefault));
                    currentY += 35;
                    
                    // Move field (X,Y format) - Zone 1
                    commandEditor.Controls.Add(new DarkLabel()
                    {
                        Text = "Move X,Y",
                        Size = new System.Drawing.Size(60, 20),
                        Location = new System.Drawing.Point(25, currentY + 2),
                        Anchor = AnchorStyles.Left | AnchorStyles.Top
                    });
                    
                    var tbMove = new DarkTextBox()
                    {
                        Name = "tbMove",
                        Text = content["Move X,Y"]?.ToString() ?? "0,0",
                        Size = new System.Drawing.Size(160, 20),
                        Location = new System.Drawing.Point(90, currentY),
                        Anchor = AnchorStyles.Right | AnchorStyles.Top | AnchorStyles.Left
                    };
                    commandEditor.Controls.Add(tbMove);
                    currentVars.Add("Move", new KeyValuePair<DarkLabel, DarkTextBox>(null, tbMove));
                    currentY += 35;
                    
                    // Multiple Zones checkbox
                    var chkMultipleZones = new DarkCheckBox()
                    {
                        Name = "chkMultipleZones",
                        Text = "Multiple Zones",
                        Size = new System.Drawing.Size(160, 20),
                        Location = new System.Drawing.Point(25, currentY),
                        Checked = !string.IsNullOrEmpty(content["ExtraZones"]?.ToString()) && content["ExtraZones"].ToString() != "[]",
                        Anchor = AnchorStyles.Left | AnchorStyles.Top
                    };
                    commandEditor.Controls.Add(chkMultipleZones);
                    currentY += 30;
                    
                    // Multiple zones panel
                    Panel pnlMultipleZones = new Panel()
                    {
                        Name = "pnlMultipleZones",
                        Size = new System.Drawing.Size(290, 0),
                        Location = new System.Drawing.Point(15, currentY),
                        BackColor = System.Drawing.Color.FromArgb(36, 36, 46),
                        BorderStyle = BorderStyle.None,
                        Visible = false,
                        Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right
                    };
                    commandEditor.Controls.Add(pnlMultipleZones);
                    
                    int zoneAnimationSpeed = 8;
                    int zoneCurrentHeight = 0;
                    Timer zoneAnimationTimer = new Timer();
                    EventHandler zoneTickHandler = null;
                    int zoneTargetHeight = 80; // Zone 2 pair
                    
                    // Declare attachRemoveZoneHandler so it can be used in refreshZonePanelUI
                    Action<int> attachRemoveZoneHandler = null;
                    
                    // Helper function to refresh the entire panel UI (like Multiple Auras)
                    Action refreshZonePanelUI = null;
                    refreshZonePanelUI = () =>
                    {
                        pnlMultipleZones.Controls.Clear();
                        int panelY = 10;
                        
                        // Zone 2 - always add first (like Aura 2)
                        try
                        {
                            string extraZonesJson = content["ExtraZones"]?.ToString() ?? "[]";
                            JArray extraZones = JArray.Parse(extraZonesJson);
                            
                            if (extraZones.Count > 0)
                            {
                                var zone2 = extraZones[0];
                                
                                // Zone 2 Label
                                pnlMultipleZones.Controls.Add(new DarkLabel()
                                {
                                    Text = "Zone 2",
                                    Size = new System.Drawing.Size(60, 20),
                                    Location = new System.Drawing.Point(10, panelY + 2),
                                    Anchor = AnchorStyles.Left | AnchorStyles.Top
                                });
                                
                                var tbZone2 = new DarkTextBox()
                                {
                                    Name = "tbZone2",
                                    Text = zone2["Zone"]?.ToString() ?? "",
                                    Size = new System.Drawing.Size(160, 20),
                                    Location = new System.Drawing.Point(80, panelY),
                                    Anchor = AnchorStyles.Left | AnchorStyles.Top
                                };
                                pnlMultipleZones.Controls.Add(tbZone2);
                                panelY += 30;
                                
                                // Move 2 Label
                                pnlMultipleZones.Controls.Add(new DarkLabel()
                                {
                                    Text = "Move X,Y",
                                    Size = new System.Drawing.Size(60, 20),
                                    Location = new System.Drawing.Point(10, panelY + 2),
                                    Anchor = AnchorStyles.Left | AnchorStyles.Top
                                });
                                
                                var tbMove2 = new DarkTextBox()
                                {
                                    Name = "tbMove2",
                                    Text = zone2["Move"]?.ToString() ?? "0,0",
                                    Size = new System.Drawing.Size(160, 20),
                                    Location = new System.Drawing.Point(80, panelY),
                                    Anchor = AnchorStyles.Left | AnchorStyles.Top
                                };
                                pnlMultipleZones.Controls.Add(tbMove2);
                                panelY += 30;
                            }
                            else
                            {
                                // No extra zones yet, just show empty Zone 2 pair
                                pnlMultipleZones.Controls.Add(new DarkLabel()
                                {
                                    Text = "Zone 2",
                                    Size = new System.Drawing.Size(60, 20),
                                    Location = new System.Drawing.Point(10, panelY + 2),
                                    Anchor = AnchorStyles.Left | AnchorStyles.Top
                                });
                                
                                var tbZone2 = new DarkTextBox()
                                {
                                    Name = "tbZone2",
                                    Text = "",
                                    Size = new System.Drawing.Size(160, 20),
                                    Location = new System.Drawing.Point(80, panelY),
                                    Anchor = AnchorStyles.Left | AnchorStyles.Top
                                };
                                pnlMultipleZones.Controls.Add(tbZone2);
                                panelY += 30;
                                
                                pnlMultipleZones.Controls.Add(new DarkLabel()
                                {
                                    Text = "Move X,Y",
                                    Size = new System.Drawing.Size(60, 20),
                                    Location = new System.Drawing.Point(10, panelY + 2),
                                    Anchor = AnchorStyles.Left | AnchorStyles.Top
                                });
                                
                                var tbMove2 = new DarkTextBox()
                                {
                                    Name = "tbMove2",
                                    Text = "0,0",
                                    Size = new System.Drawing.Size(160, 20),
                                    Location = new System.Drawing.Point(80, panelY),
                                    Anchor = AnchorStyles.Left | AnchorStyles.Top
                                };
                                pnlMultipleZones.Controls.Add(tbMove2);
                                panelY += 30;
                            }
                            
                            // Add any additional zones (3+) with Click to remove (clickable label)
                            for (int i = 1; i < extraZones.Count; i++)
                            {
                                var zone = extraZones[i];
                                
                                // Zone N Label (clickable to remove)
                                var lblZoneN = new DarkLabel()
                                {
                                    Text = $"Zone {i + 2}",
                                    Size = new System.Drawing.Size(60, 20),
                                    Location = new System.Drawing.Point(10, panelY + 2),
                                    Anchor = AnchorStyles.Left | AnchorStyles.Top,
                                    Cursor = Cursors.Hand
                                };
                                int zoneIdx = i;
                                lblZoneN.Click += (s, e) =>
                                {
                                    attachRemoveZoneHandler(zoneIdx);
                                };
                                pnlMultipleZones.Controls.Add(lblZoneN);
                                
                                var tbZoneN = new DarkTextBox()
                                {
                                    Name = $"tbZone{i + 2}",
                                    Text = zone["Zone"]?.ToString() ?? "",
                                    Size = new System.Drawing.Size(160, 20),
                                    Location = new System.Drawing.Point(80, panelY),
                                    Anchor = AnchorStyles.Left | AnchorStyles.Top
                                };
                                pnlMultipleZones.Controls.Add(tbZoneN);
                                panelY += 30;
                                
                                // Move N Label
                                pnlMultipleZones.Controls.Add(new DarkLabel()
                                {
                                    Text = "Move X,Y",
                                    Size = new System.Drawing.Size(60, 20),
                                    Location = new System.Drawing.Point(10, panelY + 2),
                                    Anchor = AnchorStyles.Left | AnchorStyles.Top
                                });
                                
                                var tbMoveN = new DarkTextBox()
                                {
                                    Name = $"tbMove{i + 2}",
                                    Text = zone["Move"]?.ToString() ?? "0,0",
                                    Size = new System.Drawing.Size(160, 20),
                                    Location = new System.Drawing.Point(80, panelY),
                                    Anchor = AnchorStyles.Left | AnchorStyles.Top
                                };
                                pnlMultipleZones.Controls.Add(tbMoveN);
                                panelY += 30;
                            }
                        }
                        catch { }
                        
                        // + button to add more zones
                        var btnAddZone = new DarkUI.Controls.DarkButton()
                        {
                            Name = "btnAddZone",
                            Text = "+",
                            Size = new System.Drawing.Size(30, 25),
                            Location = new System.Drawing.Point(10, panelY),
                            Anchor = AnchorStyles.Left | AnchorStyles.Top
                        };
                        pnlMultipleZones.Controls.Add(btnAddZone);
                        
                        // Set panel height to fit all content
                        pnlMultipleZones.Height = panelY + 40;
                        
                        // Update target height for animation
                        zoneTargetHeight = panelY + 40;
                        
                        // Reattach add button handler
                        btnAddZone.Click += (s, e) =>
                        {
                            // Store old panel height BEFORE making changes
                            int oldPanelHeight = pnlMultipleZones.Height;
                            
                            try
                            {
                                JArray extraZones = new JArray();
                                
                                // Collect all zone pair data from textboxes in panel
                                var zoneBoxes = pnlMultipleZones.Controls.OfType<DarkTextBox>()
                                    .Where(t => t.Name.StartsWith("tbZone"))
                                    .OrderBy(t => int.Parse(t.Name.Substring(6)))
                                    .ToList();
                                
                                var moveBoxes = pnlMultipleZones.Controls.OfType<DarkTextBox>()
                                    .Where(t => t.Name.StartsWith("tbMove"))
                                    .OrderBy(t => int.Parse(t.Name.Substring(6)))
                                    .ToList();
                                
                                // Add all current zones to array
                                for (int i = 0; i < zoneBoxes.Count && i < moveBoxes.Count; i++)
                                {
                                    extraZones.Add(new JObject
                                    {
                                        { "Zone", zoneBoxes[i].Text ?? "" },
                                        { "Move", moveBoxes[i].Text ?? "0,0" }
                                    });
                                }
                                
                                // Add new empty zone
                                extraZones.Add(new JObject
                                {
                                    { "Zone", "" },
                                    { "Move", "0,0" }
                                });
                                
                                content["ExtraZones"] = extraZones.ToString();
                                
                                // Rebuild the panel - this will calculate the correct new height
                                refreshZonePanelUI();
                                
                                // Adjust window based on actual height change
                                int newPanelHeight = pnlMultipleZones.Height;
                                int heightIncrease = newPanelHeight - oldPanelHeight;
                                commandEditor.Size = new Size(commandEditor.Size.Width, commandEditor.Size.Height + heightIncrease);
                            }
                            catch { }
                        };
                    };
                    
                    // Define the remove handler for zones
                    attachRemoveZoneHandler = (zoneIdx) =>
                    {
                        int index = zoneIdx;
                        
                        // Remove from ExtraZones array
                        var existingExtra = content["ExtraZones"]?.ToString() ?? "[]";
                        JArray extraZones = null;
                        try
                        {
                            extraZones = JArray.Parse(existingExtra);
                            if (index >= 1 && index < extraZones.Count)
                            {
                                extraZones.RemoveAt(index);
                                content["ExtraZones"] = extraZones.Count > 0 ? extraZones.ToString() : "[]";
                            }
                        }
                        catch { }
                        
                        // Store old height before refresh
                        int oldPanelHeight = pnlMultipleZones.Height;
                        
                        // Rebuild the panel - this will calculate the correct new height
                        refreshZonePanelUI();
                        
                        // Now adjust the command editor window based on the actual height change
                        int newPanelHeight = pnlMultipleZones.Height;
                        int heightReduction = oldPanelHeight - newPanelHeight;
                        commandEditor.Size = new Size(commandEditor.Size.Width, commandEditor.Size.Height - heightReduction);
                    };
                    
                    chkMultipleZones.CheckedChanged += (s, e) =>
                    {
                        zoneAnimationTimer.Stop();
                        
                        // Remove old tick handler if it exists
                        if (zoneTickHandler != null)
                            zoneAnimationTimer.Tick -= zoneTickHandler;
                        
                        zoneAnimationTimer.Interval = 30;
                        
                        zoneTickHandler = (sender, args) =>
                        {
                            if (chkMultipleZones.Checked)
                            {
                                // Expanding
                                zoneCurrentHeight = Math.Min(zoneCurrentHeight + zoneAnimationSpeed, zoneTargetHeight);
                                pnlMultipleZones.Height = zoneCurrentHeight;
                                commandEditor.Size = new Size(commandEditor.Size.Width, commandEditor.Size.Height + zoneAnimationSpeed);
                                
                                if (zoneCurrentHeight >= zoneTargetHeight)
                                {
                                    pnlMultipleZones.Height = zoneTargetHeight;
                                    zoneAnimationTimer.Stop();
                                }
                            }
                            else
                            {
                                // Collapsing
                                zoneCurrentHeight = Math.Max(zoneCurrentHeight - zoneAnimationSpeed, 0);
                                pnlMultipleZones.Height = zoneCurrentHeight;
                                commandEditor.Size = new Size(commandEditor.Size.Width, commandEditor.Size.Height - zoneAnimationSpeed);
                                
                                if (zoneCurrentHeight <= 0)
                                {
                                    pnlMultipleZones.Visible = false;
                                    pnlMultipleZones.Height = 0;
                                    zoneAnimationTimer.Stop();
                                }
                            }
                        };
                        zoneAnimationTimer.Tick += zoneTickHandler;
                        
                        if (chkMultipleZones.Checked)
                        {
                            pnlMultipleZones.Visible = true;
                            // Refresh panel BEFORE starting animation to calculate correct height
                            refreshZonePanelUI();
                            // Recalculate target height based on actual panel content
                            zoneTargetHeight = pnlMultipleZones.Height;
                            // Reset panel height to 0 so animation starts from 0
                            pnlMultipleZones.Height = 0;
                            zoneCurrentHeight = 0;
                            zoneAnimationTimer.Start();
                        }
                        else
                        {
                            zoneCurrentHeight = pnlMultipleZones.Height;
                            zoneAnimationTimer.Start();
                        }
                    };
                    
                    // If checkbox is checked on load, initialize panel
                    if (chkMultipleZones.Checked)
                    {
                        pnlMultipleZones.Visible = true;
                        refreshZonePanelUI();
                        // Also need to expand the commandEditor window height to show the panel
                        commandEditor.Size = new Size(commandEditor.Size.Width, commandEditor.Size.Height + pnlMultipleZones.Height);
                        currentY += 10;
                    }
                    else
                    {
                        currentY += 40;
                    }
                }
                
                foreach (KeyValuePair<string, JToken> item in content)
                {

                    if (!string.IsNullOrEmpty(item.Key) && Array.IndexOf(skip, item.Key) == -1 && commandEditor.statementCommands.Find((StatementCommand s) => s.GetType() == content.GetType())?.Text != item.Key)
                    {
                        string lblText = item.Key;
                        string tbText = item.Value.ToString();
                        switch (item.Key)
                        {
                            case "Value1":
                                lblText = commandEditor.statementCommands.Find((StatementCommand s) => s.GetType() == obj.GetType()).Description1;
                                tbText = tbText == lblText ? "" : tbText;
                                break;
                            case "Value2":
                                lblText = commandEditor.statementCommands.Find((StatementCommand s) => s.GetType() == obj.GetType()).Description2;
                                tbText = tbText == lblText ? "" : tbText;
                                break;
                            case "Value3":
                                if (obj.GetType().Name == "CmdBalanceHP")
                                    lblText = "Skill";
                                else if (isAuraCommand)
                                    lblText = "Skill";
                                else
                                    lblText = "Attack Priority";
                                break;
                            case "Value4":
                                lblText = "Aura Name 2";
                                break;
                            case "Value5":
                                lblText = "Aura Value 2";
                                break;
                            case "Value6":
                                lblText = "Operator";
                                break;
                            case "TauntOrder":
                                lblText = "Taunt Order";
                                break;
                            case "Label":
                                lblText = obj.GetType().Name == "CmdBalanceHP" ? "HP Thresholds" : "Account Total";
                                break;
                            case "Quest":
                                var qObj = JsonConvert.DeserializeObject<Quest>(item.Value.ToString());
                                lblText = "Quest ID"; 
                                tbText = qObj.Id.ToString(); 
                                break;
                            case "SkillSet":
                                lblText = "Skill Set";
                                break;
                        }
                        
                        // For aura commands, add "Multiple Auras" checkbox before Value4
                        if (isAuraCommand && item.Key == "Value4" && chkMultipleAuras == null)
                        {
                            multiAuraStartY = currentY;
                            chkMultipleAuras = new DarkCheckBox()
                            {
                                Name = "chkMultipleAuras",
                                Text = "Multiple Auras",
                                Size = new System.Drawing.Size(160, 20),
                                Location = new System.Drawing.Point(25, currentY),
                                Checked = !string.IsNullOrEmpty(content["Value4"]?.ToString()) && content["Value4"].ToString() != "Value4",
                                Anchor = AnchorStyles.Left | AnchorStyles.Top
                            };
                            commandEditor.Controls.Add(chkMultipleAuras);
                            currentY += 30;
                            
                            // Create collapsible panel for multiple auras
                            pnlMultipleAuras = new Panel()
                            {
                                Name = "pnlMultipleAuras",
                                Size = new System.Drawing.Size(290, 0),
                                Location = new System.Drawing.Point(15, currentY),
                                BackColor = System.Drawing.Color.FromArgb(36, 36, 46),
                                BorderStyle = BorderStyle.None,
                                Visible = false,
                                Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right
                            };
                            commandEditor.Controls.Add(pnlMultipleAuras);
                            
                            // Calculate target height based on extra auras stored
                            int targetHeight = 130; // Base height for Aura 2 fields + button
                            try
                            {
                                string extraAurasJson = content["ExtraAuras"]?.ToString() ?? "[]";
                                JArray extraAuras = JArray.Parse(extraAurasJson);
                                targetHeight += extraAuras.Count * 100; // Each additional aura takes ~100px
                            }
                            catch { }
                            
                            int animationSpeed = 8;
                            int currentHeight = 0;
                            Timer animationTimer = new Timer();
                            EventHandler tickHandler = null;
                            
                            chkMultipleAuras.CheckedChanged += (s, e) =>
                            {
                                animationTimer.Stop();
                                
                                // Sync textbox values back to ExtraAuras before refresh
                                if (chkMultipleAuras.Checked)
                                {
                                    try
                                    {
                                        JArray extraAuras = new JArray();
                                        // Collect all aura 3+ data from textboxes
                                        for (int i = 3; i <= 20; i++)
                                        {
                                            var tbName = pnlMultipleAuras.Controls.OfType<DarkTextBox>()
                                                .FirstOrDefault(t => t.Name == $"tbAuraName{i}");
                                            var tbValue = pnlMultipleAuras.Controls.OfType<DarkTextBox>()
                                                .FirstOrDefault(t => t.Name == $"tbAuraValue{i}");
                                            var cbOp = pnlMultipleAuras.Controls.OfType<DarkUI.Controls.DarkComboBox>()
                                                .FirstOrDefault(c => c.Name == $"cbOperator{i}");
                                            
                                            // If this aura's Name control exists, save all its data (even if fields are empty)
                                            if (tbName != null)
                                            {
                                                var auraObj = new JObject();
                                                auraObj["Name"] = tbName.Text ?? "";
                                                auraObj["Value"] = tbValue?.Text ?? "";
                                                auraObj["Operator"] = cbOp?.SelectedItem?.ToString() ?? "AND";
                                                extraAuras.Add(auraObj);
                                            }
                                        }
                                        content["ExtraAuras"] = extraAuras.Count > 0 ? extraAuras.ToString() : "[]";
                                    }
                                    catch { }
                                }
                                
                                // Recalculate target height based on current extra auras
                                int newTargetHeight = 130;
                                try
                                {
                                    string extraAurasJson = content["ExtraAuras"]?.ToString() ?? "[]";
                                    JArray extraAuras = JArray.Parse(extraAurasJson);
                                    newTargetHeight += extraAuras.Count * 100;
                                }
                                catch { }
                                targetHeight = newTargetHeight;
                                
                                // Remove old tick handler if it exists
                                if (tickHandler != null)
                                    animationTimer.Tick -= tickHandler;
                                
                                animationTimer.Interval = 30;
                                
                                tickHandler = (sender, args) =>
                                {
                                    if (chkMultipleAuras.Checked)
                                    {
                                        // Expanding
                                        currentHeight = Math.Min(currentHeight + animationSpeed, targetHeight);
                                        pnlMultipleAuras.Height = currentHeight;
                                        commandEditor.Size = new Size(commandEditor.Size.Width, commandEditor.Size.Height + animationSpeed);
                                        
                                        if (currentHeight >= targetHeight)
                                        {
                                            pnlMultipleAuras.Height = targetHeight;
                                            animationTimer.Stop();
                                        }
                                    }
                                    else
                                    {
                                        // Collapsing
                                        currentHeight = Math.Max(currentHeight - animationSpeed, 0);
                                        pnlMultipleAuras.Height = currentHeight;
                                        commandEditor.Size = new Size(commandEditor.Size.Width, commandEditor.Size.Height - animationSpeed);
                                        
                                        if (currentHeight <= 0)
                                        {
                                            pnlMultipleAuras.Visible = false;
                                            pnlMultipleAuras.Height = 0;
                                            animationTimer.Stop();
                                        }
                                    }
                                };
                                animationTimer.Tick += tickHandler;
                                
                                if (chkMultipleAuras.Checked)
                                {
                                    pnlMultipleAuras.Visible = true;
                                    currentHeight = 0;
                                    animationTimer.Start();
                                }
                                else
                                {
                                    currentHeight = pnlMultipleAuras.Height;
                                    animationTimer.Start();
                                }
                            };
                            

                        }
                        
                        // Skip Value4, Value5, Value6 as they're handled specially
                        if (item.Key == "Value4" || item.Key == "Value5" || item.Key == "Value6")
                            continue;
                        
                        // Special handling for SkillSet - create ComboBox instead of TextBox
                        if (item.Key == "SkillSet")
                        {
                            var lblSkillSet = new DarkLabel()
                            {
                                Name = $"lbl{item.Key}{count}",
                                Text = lblText,
                                Size = new System.Drawing.Size(90, 20),
                                Location = new System.Drawing.Point(25, currentY + 2),
                                Anchor = AnchorStyles.Left | AnchorStyles.Top
                            };
                            
                            var cbSkillSet = new DarkUI.Controls.DarkComboBox()
                            {
                                Name = $"cb{item.Key}{count}",
                                Size = new System.Drawing.Size(160, 24),
                                Location = new System.Drawing.Point(125, currentY - 2),
                                Anchor = AnchorStyles.Right | AnchorStyles.Top | AnchorStyles.Left,
                                DropDownStyle = ComboBoxStyle.DropDownList
                            };
                            
                            // Populate with skillset options
                            cbSkillSet.Items.Add("Auto Attack"); // Default auto attack option
                            try
                            {
                                var skillSetNames = SkillSetManager.Instance.GetAllSkillSetNames();
                                foreach (var skillSetName in skillSetNames)
                                {
                                    cbSkillSet.Items.Add(skillSetName);
                                }
                            }
                            catch { }
                            
                            // Set selected value
                            if (!string.IsNullOrEmpty(tbText) && cbSkillSet.Items.Contains(tbText))
                                cbSkillSet.SelectedItem = tbText;
                            else
                                cbSkillSet.SelectedIndex = 0;
                            
                            commandEditor.Controls.Add(lblSkillSet);
                            commandEditor.Controls.Add(cbSkillSet);
                            
                            // Store reference for later retrieval
                            currentVars.Add(item.Key, new KeyValuePair<DarkLabel, DarkTextBox>(
                                lblSkillSet,
                                new DarkTextBox() { Name = cbSkillSet.Name, Text = cbSkillSet.SelectedItem?.ToString() ?? "" }));
                            
                            count++;
                            currentY += 30;
                            continue;
                        }
                        
                        currentVars.Add(item.Key, new KeyValuePair<DarkLabel, DarkTextBox>(
                            new DarkLabel()
                            {
                                Name = $"lbl{item.Key}{count}",
                                Text = lblText,
                                Size = new System.Drawing.Size(90, 20),
                                Location = new System.Drawing.Point(25, currentY + 2),
                                Anchor = AnchorStyles.Left | AnchorStyles.Top
                            },
                            new DarkTextBox()
                            {
                                Name = $"tb{item.Key}{count}",
                                Text = tbText,
                                Size = new System.Drawing.Size(160, 20),
                                Location = new System.Drawing.Point(125, currentY),
                                Anchor = AnchorStyles.Right | AnchorStyles.Top | AnchorStyles.Left
                            }));
                        commandEditor.Controls.Add(currentVars[item.Key].Key);
                        commandEditor.Controls.Add(currentVars[item.Key].Value);

                        // Make key handler for each textbox
                        currentVars[item.Key].Value.KeyDown += commandEditor.txtCmd_KeyDown;
                        
                        count++;
                        currentY += 30;
                    }
                }
                
                // Add multi-aura fields inside the panel if it's an aura command
                if (isAuraCommand && pnlMultipleAuras != null)
                {
                    // Clear existing controls in the panel to avoid duplicates
                    pnlMultipleAuras.Controls.Clear();
                    
                    int panelY = 10;
                    List<Control> dynamicAuraControls = new List<Control>();
                    
                    // Declare attachRemoveHandler first so it can be used in addAuraPair
                    Action<int> attachRemoveHandler = null;
                    
                    // Helper function to add an aura pair at the given Y position
                    Func<int, string, string, string, int, bool, int> addAuraPair = (yPos, auraNameValue, auraValueValue, operatorValue, auraIndex, showRemoveBtn) =>
                    {
                        // Aura Name Label
                        var lblAuraName = new DarkLabel()
                        {
                            Name = $"lblAuraName{auraIndex}",
                            Text = $"Aura Name {auraIndex}",
                            Size = new System.Drawing.Size(90, 20),
                            Location = new System.Drawing.Point(10, yPos + 2),
                            Anchor = AnchorStyles.Left | AnchorStyles.Top
                        };
                        pnlMultipleAuras.Controls.Add(lblAuraName);
                        
                        // Aura Name TextBox
                        var tbAuraName = new DarkTextBox()
                        {
                            Name = $"tbAuraName{auraIndex}",
                            Text = auraNameValue,
                            Size = new System.Drawing.Size(160, 20),
                            Location = new System.Drawing.Point(110, yPos),
                            Anchor = AnchorStyles.Left | AnchorStyles.Top
                        };
                        pnlMultipleAuras.Controls.Add(tbAuraName);
                        dynamicAuraControls.Add(tbAuraName);
                        
                        // Make label clickable for remove functionality (for removable auras)
                        if (showRemoveBtn)
                        {
                            lblAuraName.Cursor = Cursors.Hand;
                            lblAuraName.Click += (s, e) =>
                            {
                                attachRemoveHandler(auraIndex);
                            };
                        }
                        
                        yPos += 30;
                        
                        // Aura Value Label
                        pnlMultipleAuras.Controls.Add(new DarkLabel()
                        {
                            Name = $"lblAuraValue{auraIndex}",
                            Text = $"Aura Value {auraIndex}",
                            Size = new System.Drawing.Size(90, 20),
                            Location = new System.Drawing.Point(10, yPos + 2),
                            Anchor = AnchorStyles.Left | AnchorStyles.Top
                        });
                        
                        // Aura Value TextBox
                        var tbAuraValue = new DarkTextBox()
                        {
                            Name = $"tbAuraValue{auraIndex}",
                            Text = auraValueValue,
                            Size = new System.Drawing.Size(160, 20),
                            Location = new System.Drawing.Point(110, yPos),
                            Anchor = AnchorStyles.Right | AnchorStyles.Top | AnchorStyles.Left
                        };
                        pnlMultipleAuras.Controls.Add(tbAuraValue);
                        dynamicAuraControls.Add(tbAuraValue);
                        
                        yPos += 30;
                        
                        // Operator ComboBox
                        pnlMultipleAuras.Controls.Add(new DarkLabel()
                        {
                            Name = $"lblOperator{auraIndex}",
                            Text = "Operator",
                            Size = new System.Drawing.Size(90, 20),
                            Location = new System.Drawing.Point(10, yPos + 2),
                            Anchor = AnchorStyles.Left | AnchorStyles.Top
                        });
                        
                        var cbOperator = new DarkUI.Controls.DarkComboBox()
                        {
                            Name = $"cbOperator{auraIndex}",
                            Size = new System.Drawing.Size(160, 24),
                            Location = new System.Drawing.Point(110, yPos - 2),
                            Anchor = AnchorStyles.Right | AnchorStyles.Top | AnchorStyles.Left,
                            DropDownStyle = ComboBoxStyle.DropDownList
                        };
                        cbOperator.Items.Add("AND");
                        cbOperator.Items.Add("OR");
                        if (!string.IsNullOrEmpty(operatorValue) && (operatorValue == "AND" || operatorValue == "OR"))
                            cbOperator.SelectedItem = operatorValue;
                        else
                            cbOperator.SelectedIndex = 0;
                        pnlMultipleAuras.Controls.Add(cbOperator);
                        dynamicAuraControls.Add(cbOperator);
                        
                        return yPos + 30;
                    };
                    
                    // Add Aura 2 (always shown when multiple auras is enabled)
                    panelY = addAuraPair(panelY, content["Value4"]?.ToString() ?? "", content["Value5"]?.ToString() ?? "", 
                                        content["Value6"]?.ToString() ?? "AND", 2, false);
                    
                    // Parse and add any stored additional auras
                    string extraAurasJson = content["ExtraAuras"]?.ToString() ?? "[]";
                    try
                    {
                        JArray extraAuras = JArray.Parse(extraAurasJson);
                        for (int i = 0; i < extraAuras.Count; i++)
                        {
                            var aura = extraAuras[i];
                            panelY = addAuraPair(panelY, aura["Name"]?.ToString() ?? "", aura["Value"]?.ToString() ?? "", 
                                               aura["Operator"]?.ToString() ?? "AND", 3 + i, true);
                        }
                    }
                    catch
                    {
                        // If JSON parse fails, just skip extra auras
                    }
                    
                    // Add "+" button to add more auras
                    var btnAddAura = new DarkUI.Controls.DarkButton()
                    {
                        Name = "btnAddAura",
                        Text = "+",
                        Size = new System.Drawing.Size(30, 25),
                        Location = new System.Drawing.Point(10, panelY),
                        Anchor = AnchorStyles.Left | AnchorStyles.Top
                    };
                    pnlMultipleAuras.Controls.Add(btnAddAura);
                    
                    // Store aura index for adding new auras
                    int currentAuraIndex = 3 + (extraAurasJson != "[]" ? (JArray.Parse(extraAurasJson).Count) : 0);
                    
                    // Helper function to refresh the entire panel UI
                    Action refreshPanelUI = null;
                    refreshPanelUI = () =>
                    {
                        pnlMultipleAuras.Controls.Clear();
                        int panelY = 10;
                        
                        // Aura 2 (always show)
                        panelY = addAuraPair(panelY, content["Value4"]?.ToString() ?? "", content["Value5"]?.ToString() ?? "", 
                                            content["Value6"]?.ToString() ?? "AND", 2, false);
                        
                        // Extra auras
                        string extraAurasJson = content["ExtraAuras"]?.ToString() ?? "[]";
                        try
                        {
                            JArray extraAuras = JArray.Parse(extraAurasJson);
                            for (int i = 0; i < extraAuras.Count; i++)
                            {
                                var aura = extraAuras[i];
                                panelY = addAuraPair(panelY, aura["Name"]?.ToString() ?? "", aura["Value"]?.ToString() ?? "", 
                                                   aura["Operator"]?.ToString() ?? "AND", 3 + i, true);
                            }
                        }
                        catch { }
                        
                        // Add + button
                        var btnAddAura = new DarkUI.Controls.DarkButton()
                        {
                            Name = "btnAddAura",
                            Text = "+",
                            Size = new System.Drawing.Size(30, 25),
                            Location = new System.Drawing.Point(10, panelY),
                            Anchor = AnchorStyles.Left | AnchorStyles.Top
                        };
                        pnlMultipleAuras.Controls.Add(btnAddAura);
                        
                        // Set panel height to fit all content - add button height + padding
                        pnlMultipleAuras.Height = panelY + 40;
                        
                        // Reattach add button handler
                        btnAddAura.Click += (s, e) =>
                        {
                            // Store old height BEFORE making changes
                            int oldPanelHeight = pnlMultipleAuras.Height;
                            
                            var existingExtra = content["ExtraAuras"]?.ToString() ?? "[]";
                            JArray extraAuras = null;
                            try
                            {
                                extraAuras = JArray.Parse(existingExtra);
                            }
                            catch
                            {
                                extraAuras = new JArray();
                            }
                            
                            var newAura = new JObject();
                            newAura["Name"] = "";
                            newAura["Value"] = "";
                            newAura["Operator"] = "AND";
                            extraAuras.Add(newAura);
                            content["ExtraAuras"] = extraAuras.ToString();
                            
                            // Rebuild the panel - this will calculate the correct new height
                            refreshPanelUI();
                            
                            // Adjust window based on actual height change
                            int newPanelHeight = pnlMultipleAuras.Height;
                            int heightIncrease = newPanelHeight - oldPanelHeight;
                            commandEditor.Size = new Size(commandEditor.Size.Width, commandEditor.Size.Height + heightIncrease);
                        };
                    };
                    
                    // Now define the remove handler
                    attachRemoveHandler = (auraIndex) =>
                    {
                        int index = auraIndex - 3;
                        
                        // Remove from ExtraAuras array
                        var existingExtra = content["ExtraAuras"]?.ToString() ?? "[]";
                        JArray extraAuras = null;
                        try
                        {
                            extraAuras = JArray.Parse(existingExtra);
                            if (index >= 0 && index < extraAuras.Count)
                            {
                                extraAuras.RemoveAt(index);
                                content["ExtraAuras"] = extraAuras.Count > 0 ? extraAuras.ToString() : "[]";
                            }
                        }
                        catch { }
                        
                        // Store old height before refresh
                        int oldPanelHeight = pnlMultipleAuras.Height;
                        
                        // Rebuild the panel - this will calculate the correct new height
                        refreshPanelUI();
                        
                        // Now adjust the command editor window based on the actual height change
                        int newPanelHeight = pnlMultipleAuras.Height;
                        int heightReduction = oldPanelHeight - newPanelHeight;
                        commandEditor.Size = new Size(commandEditor.Size.Width, commandEditor.Size.Height - heightReduction);
                    };
                    
                    // Attach initial + button handler
                    btnAddAura.Click += (s, e) =>
                    {
                        // Store old height BEFORE making changes
                        int oldPanelHeight = pnlMultipleAuras.Height;
                        
                        var existingExtra = content["ExtraAuras"]?.ToString() ?? "[]";
                        JArray extraAuras = null;
                        try
                        {
                            extraAuras = JArray.Parse(existingExtra);
                        }
                        catch
                        {
                            extraAuras = new JArray();
                        }
                        
                        var newAura = new JObject();
                        newAura["Name"] = "";
                        newAura["Value"] = "";
                        newAura["Operator"] = "AND";
                        extraAuras.Add(newAura);
                        content["ExtraAuras"] = extraAuras.ToString();
                        
                        // Rebuild the panel - this will calculate the correct new height
                        refreshPanelUI();
                        
                        // Adjust window based on actual height change
                        int newPanelHeight = pnlMultipleAuras.Height;
                        int heightIncrease = newPanelHeight - oldPanelHeight;
                        commandEditor.Size = new Size(commandEditor.Size.Width, commandEditor.Size.Height + heightIncrease);
                    };
                    
                    // Store currentVars references for Value4, Value5, Value6 (Aura 2)
                    var tbAuraName2 = pnlMultipleAuras.Controls.OfType<DarkTextBox>().FirstOrDefault(t => t.Name == "tbAuraName2");
                    var tbAuraValue2 = pnlMultipleAuras.Controls.OfType<DarkTextBox>().FirstOrDefault(t => t.Name == "tbAuraValue2");
                    var cbOperator2 = pnlMultipleAuras.Controls.OfType<DarkUI.Controls.DarkComboBox>().FirstOrDefault(c => c.Name == "cbOperator2");

                    if (tbAuraName2 != null)
                        currentVars.Add("Value4", new KeyValuePair<DarkLabel, DarkTextBox>(null, tbAuraName2));
                    if (tbAuraValue2 != null)
                        currentVars.Add("Value5", new KeyValuePair<DarkLabel, DarkTextBox>(null, tbAuraValue2));
                    if (cbOperator2 != null)
                        currentVars.Add("Value6", new KeyValuePair<DarkLabel, DarkTextBox>(null, new DarkTextBox() { Name = "dummyValue6", Text = cbOperator2.SelectedItem?.ToString() ?? "AND" }));

                    // If checkbox is checked on load, make panel visible and adjust currentY
                    if (chkMultipleAuras.Checked)
                    {
                        pnlMultipleAuras.Visible = true;
                        // Calculate the actual height from the populated controls
                        pnlMultipleAuras.Height = pnlMultipleAuras.Controls.Cast<Control>().Max(c => c.Bottom) + 10;
                        // The panel is positioned at a specific Y location, so set currentY to the bottom of the panel
                        currentY = pnlMultipleAuras.Location.Y + pnlMultipleAuras.Height;
                    }
                    else
                    {
                        // If checkbox isn't checked, keep panel hidden with 0 height
                        pnlMultipleAuras.Visible = false;
                        pnlMultipleAuras.Height = 0;
                    }
                }
                bool hasMapProps = currentVars.Keys.Any(k =>
    k.IndexOf("cell", StringComparison.OrdinalIgnoreCase) >= 0 ||
    k.IndexOf("map", StringComparison.OrdinalIgnoreCase) >= 0 ||
    k.IndexOf("pad", StringComparison.OrdinalIgnoreCase) >= 0);

                if (!hasMapProps)
                {
                    commandEditor.btnGetInfo.Visible = false;
                    commandEditor.Size = new Size(commandEditor.Size.Width, commandEditor.Size.Height + currentY - 13);
                }
                else
                {
                    commandEditor.btnGetInfo.Visible = true;
                    commandEditor.Size = new Size(commandEditor.Size.Width, commandEditor.Size.Height + currentY + 10);
                }
                DialogResult results = commandEditor.ShowDialog();
                bool dialog = results == DialogResult.OK;
                bool dialog2 = results == DialogResult.Abort;
                if (dialog)
                {
                    // Handle Multiple Auras checkbox state
                    var chkMultiple = commandEditor.Controls.OfType<DarkCheckBox>()
                        .FirstOrDefault(c => c.Name == "chkMultipleAuras");
                    
                    foreach (KeyValuePair<string, JToken> item in content)
                    {
                        if (currentVars.ContainsKey(item.Key))
                        {
                            if (item.Key == "Quest")
                                continue;
                            // Special handling for SkillSet ComboBox
                            if (item.Key == "SkillSet")
                            {
                                var cbSkillSet = commandEditor.Controls.OfType<DarkUI.Controls.DarkComboBox>()
                                    .FirstOrDefault(c => c.Name.StartsWith("cbSkillSet"));
                                if (cbSkillSet != null && cbSkillSet.SelectedItem != null)
                                    content[item.Key] = cbSkillSet.SelectedItem.ToString();
                                else
                                    content[item.Key] = ""; // Default to empty
                            }
                            // Special handling for Value6 (Operator ComboBox)
                            else if (item.Key == "Value6")
                            {
                                var cbOperator = commandEditor.Controls.OfType<DarkUI.Controls.DarkComboBox>()
                                    .FirstOrDefault(c => c.Name == "cbOperator2");
                                if (cbOperator != null && cbOperator.SelectedItem != null)
                                    content[item.Key] = cbOperator.SelectedItem.ToString();
                                else
                                    content[item.Key] = "AND"; // Default to AND
                            }
                            else if (currentVars[item.Key].Value != null)
                            {
                                content[item.Key] = currentVars[item.Key].Value.Text;
                            }
                        }
                    }
                    
                    // If Multiple Auras is checked, collect all extra auras
                    if (chkMultiple != null && chkMultiple.Checked)
                    {
                        var pnlMultiAuras = commandEditor.Controls.OfType<Panel>()
                            .FirstOrDefault(p => p.Name == "pnlMultipleAuras");
                        
                        if (pnlMultiAuras != null)
                        {
                            // Collect aura 3+ data
                            JArray extraAuras = new JArray();
                            
                            for (int i = 3; i <= 20; i++) // Support up to aura 20
                            {
                                var tbName = pnlMultiAuras.Controls.OfType<DarkTextBox>()
                                    .FirstOrDefault(t => t.Name == $"tbAuraName{i}");
                                var tbValue = pnlMultiAuras.Controls.OfType<DarkTextBox>()
                                    .FirstOrDefault(t => t.Name == $"tbAuraValue{i}");
                                var cbOp = pnlMultiAuras.Controls.OfType<DarkUI.Controls.DarkComboBox>()
                                    .FirstOrDefault(c => c.Name == $"cbOperator{i}");
                                
                                if (tbName != null && !string.IsNullOrEmpty(tbName.Text))
                                {
                                    var auraObj = new JObject();
                                    auraObj["Name"] = tbName.Text;
                                    auraObj["Value"] = tbValue?.Text ?? "";
                                    auraObj["Operator"] = cbOp?.SelectedItem?.ToString() ?? "AND";
                                    extraAuras.Add(auraObj);
                                }
                            }
                            
                            // Store extra auras in the command
                            if (extraAuras.Count > 0)
                                content["ExtraAuras"] = extraAuras.ToString();
                            else
                                content["ExtraAuras"] = "[]";
                        }
                        
                        // Ensure Value4 has at least a space so the checkbox stays checked on reload
                        if (string.IsNullOrEmpty(content["Value4"]?.ToString()))
                            content["Value4"] = " ";
                    }
                    else if (chkMultiple != null && !chkMultiple.Checked)
                    {
                        // Clear multi-aura values if unchecked
                        content["Value4"] = "";
                        content["Value5"] = "";
                        content["Value6"] = "";
                        content["ExtraAuras"] = "[]";
                    }
                    
                    // Handle Zone Handler data collection
                    if (isZoneHandlerCommand)
                    {
                        // Collect Zone field (Zone 1)
                        var tbZone = commandEditor.Controls.OfType<DarkTextBox>()
                            .FirstOrDefault(t => t.Name == "tbZoneField");
                        if (tbZone != null)
                            content["Zone"] = tbZone.Text;
                        
                        // Collect Default field
                        var tbDefault = commandEditor.Controls.OfType<DarkTextBox>()
                            .FirstOrDefault(t => t.Name == "tbDefaultField");
                        if (tbDefault != null)
                            content["Default X,Y"] = tbDefault.Text;
                        
                        // Collect Move field (X,Y format)
                        var tbMove = commandEditor.Controls.OfType<DarkTextBox>()
                            .FirstOrDefault(t => t.Name == "tbMove");
                        if (tbMove != null)
                            content["Move X,Y"] = tbMove.Text;
                        
                        // Collect Multiple Zones if checkbox is checked
                        var chkMultipleZones = commandEditor.Controls.OfType<DarkCheckBox>()
                            .FirstOrDefault(c => c.Name == "chkMultipleZones");
                        
                        if (chkMultipleZones != null && chkMultipleZones.Checked)
                        {
                            var pnlMultipleZones = commandEditor.Controls.OfType<Panel>()
                                .FirstOrDefault(p => p.Name == "pnlMultipleZones");
                            
                            if (pnlMultipleZones != null)
                            {
                                JArray zones = new JArray();
                                
                                // Find all zone textboxes in the panel and build array
                                var zoneBoxes = pnlMultipleZones.Controls.OfType<DarkTextBox>()
                                    .Where(t => t.Name.StartsWith("tbZone"))
                                    .OrderBy(t => int.Parse(t.Name.Substring(6)))
                                    .ToList();
                                
                                var moveBoxes = pnlMultipleZones.Controls.OfType<DarkTextBox>()
                                    .Where(t => t.Name.StartsWith("tbMove"))
                                    .OrderBy(t => int.Parse(t.Name.Substring(6)))
                                    .ToList();
                                
                                for (int i = 0; i < zoneBoxes.Count && i < moveBoxes.Count; i++)
                                {
                                    zones.Add(new JObject
                                    {
                                        { "Zone", zoneBoxes[i].Text ?? "" },
                                        { "Move", moveBoxes[i].Text ?? "0,0" }
                                    });
                                }
                                
                                content["ExtraZones"] = zones.Count > 0 ? zones.ToString() : "[]";
                            }
                        }
                        else
                        {
                            content["ExtraZones"] = "[]";
                        }
                    }
                    
                    if (currentVars.ContainsKey("Quest"))
                    {
                        if (int.TryParse(currentVars["Quest"].Value.Text, out int newId))
                        {
                            JObject questObj = (JObject)content["Quest"];
                            questObj["QuestID"] = newId; // update hanya field QuestID
                        }
                    }
                    var serialized = JsonConvert.SerializeObject(content, Formatting.Indented, _serializerSettings);
                    return serialized;
                }
                else if (dialog2)
                    return RawCommandEditor.Show(JsonConvert.SerializeObject(cmdObj, Formatting.Indented, serializer));
                else return null;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null)
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(UserFriendlyCommandEditor));
            this.btnOK = new DarkUI.Controls.DarkButton();
            this.btnCancel = new DarkUI.Controls.DarkButton();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.btnRawCommand = new DarkUI.Controls.DarkButton();
            this.btnGetInfo = new DarkUI.Controls.DarkButton();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.SuspendLayout();
            // 
            // btnOK
            // 
            this.btnOK.Checked = false;
            this.btnOK.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btnOK.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnOK.Location = new System.Drawing.Point(0, 0);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(137, 23);
            this.btnOK.TabIndex = 0;
            this.btnOK.Text = "OK";
            // 
            // btnCancel
            // 
            this.btnCancel.Checked = false;
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnCancel.Location = new System.Drawing.Point(0, 0);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(141, 23);
            this.btnCancel.TabIndex = 1;
            this.btnCancel.Text = "Cancel";
            // 
            // splitContainer1
            // 
            this.splitContainer1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.splitContainer1.Location = new System.Drawing.Point(12, 46);
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.btnCancel);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.btnOK);
            this.splitContainer1.Size = new System.Drawing.Size(282, 23);
            this.splitContainer1.SplitterDistance = 141;
            this.splitContainer1.TabIndex = 2;
            // 
            // btnRawCommand
            // 
            this.btnRawCommand.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.btnRawCommand.Checked = false;
            this.btnRawCommand.DialogResult = System.Windows.Forms.DialogResult.Abort;
            this.btnRawCommand.Location = new System.Drawing.Point(12, 17);
            this.btnRawCommand.Name = "btnRawCommand";
            this.btnRawCommand.Size = new System.Drawing.Size(282, 23);
            this.btnRawCommand.TabIndex = 3;
            this.btnRawCommand.Text = "Raw Command Editor";
            // 
            // btnGetInfo
            // 
            this.btnGetInfo.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.btnGetInfo.Checked = false;
            this.btnGetInfo.Location = new System.Drawing.Point(12, -12);
            this.btnGetInfo.Name = "btnGetInfo";
            this.btnGetInfo.Size = new System.Drawing.Size(282, 23);
            this.btnGetInfo.TabIndex = 4;
            this.btnGetInfo.Text = "Get Cell / Map / Pad";
            this.btnGetInfo.Click += new System.EventHandler(this.BtnGetInfo_Click);
            // 
            // UserFriendlyCommandEditor
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(308, 81);
            this.Controls.Add(this.btnGetInfo);
            this.Controls.Add(this.btnRawCommand);
            this.Controls.Add(this.splitContainer1);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.Name = "UserFriendlyCommandEditor";
            this.Text = "Command Editor";
            this.TopMost = true;
            this.Load += new System.EventHandler(this.RawCommandEditor_Load);
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.UserFriendlyCommandEditor_KeyDown);
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.ResumeLayout(false);

        }
        private void BtnGetInfo_Click(object sender, EventArgs e)
        {
            try
            {
                //Player current location
                string cell = Player.Cell;
                string map = Player.Map;
                string pad = Player.Pad;

                // Fill relevant textbox
                foreach (Control ctrl in this.Controls)
                {
                    if (ctrl is DarkTextBox tb)
                    {
                        if (tb.Name.IndexOf("swf", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            tb.Name.IndexOf("max", StringComparison.OrdinalIgnoreCase) >= 0)
                            continue;
                        else if (tb.Name.IndexOf("Cell", StringComparison.OrdinalIgnoreCase) >= 0)
                            tb.Text = cell;
                        else if (tb.Name.IndexOf("Map", StringComparison.OrdinalIgnoreCase) >= 0)
                            tb.Text = map;
                        else if (tb.Name.IndexOf("Pad", StringComparison.OrdinalIgnoreCase) >= 0)
                            tb.Text = pad;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gagal mengambil info dari Player.\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private void UserFriendlyCommandEditor_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Return:
                    btnOK.PerformClick();
                    break;

                case Keys.Escape:
                    btnCancel.PerformClick();
                    break;
            }
        }
    }
}