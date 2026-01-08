using DarkUI.Controls;
using DarkUI.Forms;
using Grimoire.Botting.Commands.Misc.Statements;
using Grimoire.Botting.Commands.Quest;
using Grimoire.Game;
using Grimoire.Game.Data;
using Grimoire.Properties;
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
                List<string> skipList = new List<string> { "Tag", "Description1", "Description2", "$type", "Value3", "TauntOrder", "Delay", "Label", "Value4", "Value5", "Value6" };
                
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
                
                string[] skip = skipList.ToArray();
                Dictionary<string, KeyValuePair<DarkLabel, DarkTextBox>> currentVars = new Dictionary<string, KeyValuePair<DarkLabel, DarkTextBox>>();
                bool isAuraCommand = obj.GetType().Name == "CmdPlayerAuraGreaterThan" || obj.GetType().Name == "CmdPlayerAuraLessThan" || 
                                     obj.GetType().Name == "CmdPlayerAuraEquals" || obj.GetType().Name == "CmdTargetAuraGreaterThan" ||
                                     obj.GetType().Name == "CmdTargetAuraLessThan" || obj.GetType().Name == "CmdTargetAuraEquals";
                
                int multiAuraStartY = -1;
                DarkCheckBox chkMultipleAuras = null;
                Panel pnlMultipleAuras = null;
                
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
                                    lblText = "Skill Index (optional)";
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
                                BorderStyle = BorderStyle.FixedSingle,
                                Visible = false,
                                Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right
                            };
                            commandEditor.Controls.Add(pnlMultipleAuras);
                            
                            int targetHeight = 120;
                            int animationSpeed = 8;
                            int currentHeight = 0;
                            Timer animationTimer = new Timer();
                            EventHandler tickHandler = null;
                            
                            chkMultipleAuras.CheckedChanged += (s, e) =>
                            {
                                animationTimer.Stop();
                                
                                // Remove old tick handler if it exists
                                if (tickHandler != null)
                                    animationTimer.Tick -= tickHandler;
                                
                                currentHeight = pnlMultipleAuras.Height;
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
                                    currentHeight = targetHeight;
                                    animationTimer.Start();
                                }
                            };
                            
                            // If the checkbox should start checked, expand the panel immediately
                            if (chkMultipleAuras.Checked)
                            {
                                pnlMultipleAuras.Visible = true;
                                pnlMultipleAuras.Height = 120;
                                currentY += 120;
                            }
                        }
                        
                        // Skip Value4, Value5, Value6 as they're handled specially
                        if (item.Key == "Value4" || item.Key == "Value5" || item.Key == "Value6")
                            continue;
                        
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
                    
                    // Aura Name 2
                    pnlMultipleAuras.Controls.Add(new DarkLabel()
                    {
                        Name = "lblAuraName2",
                        Text = "Aura Name 2",
                        Size = new System.Drawing.Size(80, 20),
                        Location = new System.Drawing.Point(10, panelY),
                        Anchor = AnchorStyles.Left | AnchorStyles.Top
                    });
                    var tbAuraName2 = new DarkTextBox()
                    {
                        Name = "tbValue4",
                        Text = content["Value4"]?.ToString() ?? "",
                        Size = new System.Drawing.Size(180, 20),
                        Location = new System.Drawing.Point(100, panelY),
                        Anchor = AnchorStyles.Right | AnchorStyles.Top | AnchorStyles.Left
                    };
                    pnlMultipleAuras.Controls.Add(tbAuraName2);
                    currentVars.Add("Value4", new KeyValuePair<DarkLabel, DarkTextBox>(null, tbAuraName2));
                    
                    panelY += 30;
                    
                    // Aura Value 2
                    pnlMultipleAuras.Controls.Add(new DarkLabel()
                    {
                        Name = "lblAuraValue2",
                        Text = "Aura Value 2",
                        Size = new System.Drawing.Size(80, 20),
                        Location = new System.Drawing.Point(10, panelY),
                        Anchor = AnchorStyles.Left | AnchorStyles.Top
                    });
                    var tbAuraValue2 = new DarkTextBox()
                    {
                        Name = "tbValue5",
                        Text = content["Value5"]?.ToString() ?? "",
                        Size = new System.Drawing.Size(180, 20),
                        Location = new System.Drawing.Point(100, panelY),
                        Anchor = AnchorStyles.Right | AnchorStyles.Top | AnchorStyles.Left
                    };
                    pnlMultipleAuras.Controls.Add(tbAuraValue2);
                    currentVars.Add("Value5", new KeyValuePair<DarkLabel, DarkTextBox>(null, tbAuraValue2));
                    
                    panelY += 30;
                    
                    // Operator
                    pnlMultipleAuras.Controls.Add(new DarkLabel()
                    {
                        Name = "lblOperator",
                        Text = "Operator",
                        Size = new System.Drawing.Size(80, 20),
                        Location = new System.Drawing.Point(10, panelY),
                        Anchor = AnchorStyles.Left | AnchorStyles.Top
                    });
                    var cbOperator = new DarkUI.Controls.DarkComboBox()
                    {
                        Name = "cbValue6",
                        Size = new System.Drawing.Size(180, 24),
                        Location = new System.Drawing.Point(100, panelY - 2),
                        Anchor = AnchorStyles.Right | AnchorStyles.Top | AnchorStyles.Left,
                        DropDownStyle = ComboBoxStyle.DropDownList,
                        BackColor = System.Drawing.Color.FromArgb(45, 45, 48),
                        ForeColor = System.Drawing.Color.FromArgb(220, 220, 220)
                    };
                    cbOperator.Items.Add("AND");
                    cbOperator.Items.Add("OR");
                    string operatorValue = content["Value6"]?.ToString() ?? "AND";
                    if (!string.IsNullOrEmpty(operatorValue) && (operatorValue == "AND" || operatorValue == "OR"))
                        cbOperator.SelectedItem = operatorValue;
                    else
                        cbOperator.SelectedIndex = 0;
                    pnlMultipleAuras.Controls.Add(cbOperator);
                    currentVars.Add("Value6", new KeyValuePair<DarkLabel, DarkTextBox>(null, new DarkTextBox() { Name = "dummyValue6", Text = cbOperator.SelectedItem?.ToString() ?? "AND" }));
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
                            // Special handling for Value6 (Operator ComboBox)
                            if (item.Key == "Value6")
                            {
                                var cbOperator = commandEditor.Controls.OfType<DarkUI.Controls.DarkComboBox>()
                                    .FirstOrDefault(c => c.Name == "cbValue6");
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
                    
                    // If Multiple Auras is checked but Value4 is empty, set a placeholder to preserve state
                    if (chkMultiple != null && chkMultiple.Checked)
                    {
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