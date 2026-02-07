using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Forms;
using Grimoire.Botting;
using Grimoire.Game;
using Grimoire.Networking;
using DarkUI.Forms;
using Grimoire.Tools;
using MaidRemake.LockedMapHandle;
using MaidRemake.WhitelistMap;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Grimoire.Tools.Maid;
using Grimoire.Networking.Handlers.Maid;
using System.Drawing;
using Grimoire.Game.Data;
using System.Linq;
using System.Threading;
using System.Collections.Generic;

namespace Grimoire.UI.Maid
{
    public partial class MaidRemake : DarkForm
    {
        public static MaidRemake Instance { get; } = new MaidRemake();

        public string targetUsername => MaidRemake.Instance.cmbGotoUsername.Text.ToLower();

        public bool isPlayerInMyCell => bool.Parse(Flash.Call<string>("GetCellPlayers", new string[] { targetUsername }) ?? "False");

        public bool isPlayerInMyRoom => IsPlayerInMap(targetUsername);

        public int skillDelay => (int)MaidRemake.Instance.numSkillDelay.Value;

        LowLevelKeyboardHook kbh = new LowLevelKeyboardHook();

        public CellJumperHandler CJHandler { get; } = new CellJumperHandler();

        public JoinMapHandler JoinMapHandler { get; } = new JoinMapHandler();

        public WarningMsgHandler RedMsgHandler { get; } = new WarningMsgHandler();

        public CopyWalkHandler CopyWalkHandler { get; } = new CopyWalkHandler();

        public PartyChatHandler PartyChatHandler { get; } = new PartyChatHandler();

        public PartyInvitationHandler PartyInvitationHandler { get; } = new PartyInvitationHandler();

        private int healthPercent => (int)MaidRemake.Instance.numHealthPercent.Value;

        string[] buffSkill = null;
        int buffIndex = 0;

        string[] healSkill = null;
        int healIndex = 0;

        string[] monsterList = null;

        bool onPause = false;

        bool forceSkill = false;
        bool balance = false;

        Stopwatch stopwatch = new Stopwatch();

        // Skillset support
        private string _selectedSkillSet = "";
        private Grimoire.Botting.Configuration _selectedConfiguration = null;
        
        // Aura caching to avoid repeated expensive Flash calls
        private Dictionary<string, int> _auraCache = new Dictionary<string, int>();
        private bool _auraCacheValid = false;
        private DateTime _lastAuraPreloadTime = DateTime.MinValue;
        private const int AURA_PRELOAD_INTERVAL_MS = 10000; // Preload every 10 seconds

        public MaidRemake()
        {
            InitializeComponent();

            KeyPreview = true;

            //KeyListener non Global Hook
            this.KeyDown += new KeyEventHandler(this.hotkey);
            if (Player.IsLoggedIn) cmbGotoUsername.Text = Player.Username;
            cmbUltraBoss.SelectedIndex = 0;
            this.Text = $"Maid Remake";

            // Load skillsets asynchronously in background to avoid blocking UI
            this.Shown += (s, e) => LoadSkillsetsAsync();
            
            // Subscribe to skillset saved event to auto-refresh dropdown
            SkillSetManager.Instance.SkillSetSaved += (s, e) => RefreshSkillsetsAsync();

            if (cbAntiCounter.Checked)
                Flash.FlashCall2 += AntiCounterHandler;
            if (cbPartyCmd.Checked)
            {
                Proxy.Instance.RegisterHandler(PartyInvitationHandler);
                Proxy.Instance.RegisterHandler(PartyChatHandler);
            }
            if (cbAntiCounter.Checked)
                Flash.FlashCall2 += AntiCounterHandler;
            if (cbPartyCmd.Checked)
            {
                Proxy.Instance.RegisterHandler(PartyInvitationHandler);
                Proxy.Instance.RegisterHandler(PartyChatHandler);
            }
            ToolTip toolTip = new ToolTip();
            toolTip.SetToolTip(this.cbPartyCmd,
                "[Auto accept any party invitation when checked]" +
                "\n\nEnter /p party chat's to use the commands below" +
                "\n.join {mapname-room}" +
                "\n.acc {questIds}" +
                "\n.turnin {questIds}" +
                "\n.target {playername} => change Maid's master target" +
                "\n.start => turn on Maid" +
                "\n.stop => turn off Maid"
                );
        }

        /// <summary>
        /// Load skillsets asynchronously in background thread to avoid blocking UI
        /// </summary>
        private void LoadSkillsetsAsync()
        {
            Task.Run(() =>
            {
                try
                {
                    var skillSets = SkillSetManager.Instance.GetAllSkillSetNames();
                    if (skillSets != null && skillSets.Count > 0)
                    {
                        // Add to dropdown on UI thread
                        this.Invoke((Action)(() =>
                        {
                            try
                            {
                                // Only add if separator not already there
                                bool hasSeparator = false;
                                foreach (var item in cmbPreset.Items)
                                {
                                    if (item?.ToString() == "---")
                                    {
                                        hasSeparator = true;
                                        break;
                                    }
                                }

                                if (!hasSeparator)
                                {
                                    cmbPreset.Items.Add("---");
                                }

                                foreach (var skillSet in skillSets)
                                {
                                    if (!cmbPreset.Items.Contains(skillSet))
                                    {
                                        cmbPreset.Items.Add(skillSet);
                                    }
                                }

                                // If we have a saved skillset, select it
                                if (!string.IsNullOrEmpty(_selectedSkillSet) && cmbPreset.Items.Contains(_selectedSkillSet))
                                {
                                    cmbPreset.SelectedIndex = -1; // Reset
                                    cmbPreset.SelectedItem = _selectedSkillSet;
                                    LogForm.Instance?.AppendDebug($"[Maid] Restored skillset selection: {_selectedSkillSet}");
                                }
                            }
                            catch (Exception ex)
                            {
                                LogForm.Instance?.AppendDebug($"[Maid] Error populating skillsets dropdown: {ex.Message}");
                            }
                        }));
                    }
                }
                catch { /* Fail silently */ }
            });
        }

        /// <summary>
        /// Refresh skillsets dropdown when a new skillset is saved or deleted
        /// </summary>
        private void RefreshSkillsetsAsync()
        {
            Task.Run(() =>
            {
                try
                {
                    var skillSets = SkillSetManager.Instance.GetAllSkillSetNames();
                    if (skillSets != null)
                    {
                        this.Invoke((Action)(() =>
                        {
                            try
                            {
                                // Find separator position
                                int separatorIndex = -1;
                                for (int i = 0; i < cmbPreset.Items.Count; i++)
                                {
                                    if (cmbPreset.Items[i]?.ToString() == "---")
                                    {
                                        separatorIndex = i;
                                        break;
                                    }
                                }

                                // Remove all items after separator (old skillsets)
                                if (separatorIndex >= 0)
                                {
                                    while (cmbPreset.Items.Count > separatorIndex + 1)
                                    {
                                        cmbPreset.Items.RemoveAt(cmbPreset.Items.Count - 1);
                                    }
                                }
                                else
                                {
                                    // No separator found, add it
                                    cmbPreset.Items.Add("---");
                                    separatorIndex = cmbPreset.Items.Count - 1;
                                }

                                // Add current skillsets
                                foreach (var skillSet in skillSets)
                                {
                                    if (!cmbPreset.Items.Contains(skillSet))
                                    {
                                        cmbPreset.Items.Add(skillSet);
                                    }
                                }

                                LogForm.Instance?.AppendDebug($"[Maid] Skillsets refreshed - {skillSets.Count} skillsets available");
                            }
                            catch (Exception ex)
                            {
                                LogForm.Instance?.AppendDebug($"[Maid] Error refreshing skillsets dropdown: {ex.Message}");
                            }
                        }));
                    }
                }
                catch { /* Fail silently */ }
            });
        }

        private void Main_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
            }
        }

        private async void cbEnablePlugin_CheckedChanged(object sender, EventArgs e)
        {
            resetSpecials();
            if (cbEnablePlugin.Checked)
            {
                startUI();

                int gotoTry = 0;
                string equippedclass = Player.EquippedClass.ToLower();
                var skillproperties = Flash.Instance.GetGameObject<List<JObject>>("world.actions.active");

                // Check if using custom skillset or manual skill list
                List<SavedSkill> customSkillSet = null;
                List<Skill> configurationSkills = null;
                string[] skillList = null;
                int skillIndex = 0;

                // Try to load custom skillset if one is selected
                if (!string.IsNullOrEmpty(_selectedSkillSet))
                {
                    try
                    {
                        var skillSetData = SkillSetManager.Instance.LoadSkillSet(_selectedSkillSet);
                        if (skillSetData != null && skillSetData.Skills != null && skillSetData.Skills.Count > 0)
                        {
                            // Keep all skills including aura conditions for full support
                            customSkillSet = skillSetData.Skills;
                            LogForm.Instance?.AppendDebug($"[Maid] Using custom skillset: {_selectedSkillSet} ({customSkillSet.Count} entries including aura conditions)");
                            
                            // Preload auras for the skillset
                            await PreloadAurasForSkillsetAsync(customSkillSet);
                            _lastAuraPreloadTime = DateTime.Now; // Start the 10-second refresh timer after initial load
                        }
                        else if (_selectedConfiguration != null && _selectedConfiguration.Skills != null && _selectedConfiguration.Skills.Count > 0)
                        {
                            // Use Configuration format skills if loaded
                            configurationSkills = _selectedConfiguration.Skills;
                            LogForm.Instance?.AppendDebug($"[Maid] Using Configuration skillset: {_selectedSkillSet} ({configurationSkills.Count} skills)");
                        }
                        else
                        {
                            LogForm.Instance?.AppendDebug($"[Maid] Selected skillset '{_selectedSkillSet}' is empty or not found, falling back to manual list");
                            _selectedSkillSet = "";
                            _selectedConfiguration = null;
                        }
                    }
                    catch (Exception ex)
                    {
                        LogForm.Instance?.AppendDebug($"[Maid] Error loading skillset '{_selectedSkillSet}': {ex.Message}");
                        _selectedSkillSet = "";
                        _selectedConfiguration = null;
                    }
                }

                // Fall back to manual skill list if no skillset
                if (customSkillSet == null && configurationSkills == null)
                {
                    skillList = tbSkillList.Text.Split(',');
                }

                if (cbHandleLockedMap.Checked && AlternativeMap.Count() > 0)
                    AlternativeMap.Init();
                else if (cbHandleLockedMap.Checked)
                    cbHandleLockedMap.Checked = false;

                Proxy.Instance.RegisterHandler(RedMsgHandler);

                Proxy.Instance.RegisterHandler(JoinMapHandler);

                if (!cbUnfollow.Checked)
                    Proxy.Instance.RegisterHandler(CJHandler);

                if (cbCopyWalk.Checked)
                    Proxy.Instance.RegisterHandler(CopyWalkHandler);

                if (cbSpecialAnims.Checked)
                    Flash.FlashCall2 += AnimsMsgHandler;

                if (!cbUnfollow.Checked && Player.IsLoggedIn && !World.IsMapLoading && isPlayerInMyRoom && !isPlayerInMyCell)
                    Player.GoToPlayer(targetUsername);

                if (cbAttackPriority.Checked)
                    monsterList = tbAttPriority.Text.Split(',');

                if (cbUseHeal.Checked)
                    healSkill = tbHealSkill.Text.Split(',');

                if (cbBuffIfStop.Checked)
                {
                    buffSkill = tbBuffSkill.Text.Split(',');
                    buffIndex = 0;
                }

                // auto equip Scroll of Enrage
                if (cmbUltraBoss.SelectedItem.ToString() != "None")
                {
                    equipEnrage();
                }

                while (cbEnablePlugin.Checked)
                {
                    try
                    {
                        // Refresh aura cache every 10 seconds
                        if (customSkillSet != null && 
                            (DateTime.Now - _lastAuraPreloadTime).TotalMilliseconds > AURA_PRELOAD_INTERVAL_MS)
                        {
                            _lastAuraPreloadTime = DateTime.Now;
                            _ = PreloadAurasForSkillsetAsync(customSkillSet); // Fire and forget
                        }
                        
                        // while player is logout -> do delay (2s), wait first join, do first join delay
                        if (cbEnablePlugin.Checked && !Player.IsLoggedIn)
                            await waitForFirstJoin();

                        // plugin disabled
                        if (!cbEnablePlugin.Checked)
                            return;

                        // starting the plugin
                        if ((isPlayerInMyRoom || cbUnfollow.Checked) && Player.IsLoggedIn && !World.IsMapLoading && !onPause)
                        {
                            gotoTry = 0;

                            if (!Player.IsAlive)
                            {
                                skillIndex = 0;
                                World.SetSpawnPoint();
                                await Task.Delay(500);
                                forceSkill = false;
                                continue;
                            }

                            if (cbUseHeal.Checked && tbHealSkill.Text != String.Empty && isHealthUnder(healthPercent))
                            {
                                useSkill(healSkill[healIndex]);
                                //Player.UseSkill(healSkill[healIndex]);
                                healIndex++;

                                if (healIndex >= healSkill.Length)
                                    healIndex = 0;

                                await Task.Delay(skillDelay);
                                continue;
                            }

                            if (cbStopAttack.Checked)
                            {
                                if (Player.HasTarget)
                                {
                                    Player.CancelAutoAttack();
                                    Player.CancelTarget();
                                }

                                if (cbBuffIfStop.Checked && tbBuffSkill.Text != String.Empty)
                                {
                                    useSkill(buffSkill[buffIndex], true);
                                    //Player.UseSkill(buffSkill[buffIndex]);
                                    buffIndex++;

                                    if (buffIndex >= buffSkill.Length)
                                        buffIndex = 0;
                                }

                                await Task.Delay(skillDelay);
                                continue;
                            }

                            if (cbAttackPriority.Checked && !forceSkill)
                                doPriorityAttack();

                            // set targetting to availabe monster in current cell
                            if (World.IsMonsterAvailable("*") && !Player.HasTarget)
                                Player.AttackMonster("*");

                            // Get current skill index as string
                            string currentSkillIndex = "";
                            SavedSkill currentSkillData = null;
                            Skill currentConfigSkill = null;
                            
                            if (customSkillSet != null && skillIndex < customSkillSet.Count)
                            {
                                currentSkillData = customSkillSet[skillIndex];
                                
                                // Handle Label/Aura condition entries
                                if (currentSkillData.Type == 2) // Label type
                                {
                                    // Evaluate the aura condition
                                    LogForm.Instance?.AppendDebug($"[MaidSkillSet] Processing aura statement: {currentSkillData.Text}");
                                    
                                    if (_auraCacheValid)
                                    {
                                        bool conditionMet = CheckAuraCondition(currentSkillData);
                                        
                                        if (conditionMet)
                                        {
                                            // Extract skill index from the condition text and execute it
                                            string skillToExecute = ExtractSkillIndexFromCondition(currentSkillData.Text);
                                            
                                            if (!string.IsNullOrEmpty(skillToExecute))
                                            {
                                                LogForm.Instance?.AppendDebug($"[MaidSkillSet] Condition MET - executing skill {skillToExecute}");
                                                useSkill(skillToExecute);
                                            }
                                        }
                                        else
                                        {
                                            LogForm.Instance?.AppendDebug($"[MaidSkillSet] Condition NOT met - skipping");
                                        }
                                    }
                                    else
                                    {
                                        LogForm.Instance?.AppendDebug($"[MaidSkillSet] Aura cache not ready, waiting...");
                                    }
                                    
                                    // Always advance past the aura condition
                                    skillIndex = (skillIndex + 1) % customSkillSet.Count;
                                    continue;
                                }
                                
                                currentSkillIndex = currentSkillData.Index;
                            }
                            else if (configurationSkills != null && skillIndex < configurationSkills.Count)
                            {
                                currentConfigSkill = configurationSkills[skillIndex];
                                
                                // Handle Label/Aura condition entries (Type.Label = 2)
                                // These execute aura checks and cast skills if conditions are met
                                if (currentConfigSkill.Type == Skill.SkillType.Label)
                                {
                                    LogForm.Instance?.AppendDebug($"[MaidSkillSet] Processing aura statement: {currentConfigSkill.Text}");
                                    try
                                    {
                                        // ExecuteSkill() handles Label type by calling ExecuteStatementCommand()
                                        // which evaluates the aura condition and casts the skill if met
                                        await currentConfigSkill.ExecuteSkill();
                                    }
                                    catch (Exception ex)
                                    {
                                        LogForm.Instance?.AppendDebug($"[MaidSkillSet] Error processing aura statement: {ex.Message}");
                                    }
                                    
                                    // Move to next entry after processing aura statement
                                    skillIndex = (skillIndex + 1) % configurationSkills.Count;
                                    continue;
                                }
                                
                                // This is a regular skill entry (Type.Normal or Type.Safe)
                                currentSkillIndex = currentConfigSkill.Index;
                            }
                            else if (skillList != null && skillIndex < skillList.Length)
                                currentSkillIndex = skillList[skillIndex];
                            if (string.IsNullOrEmpty(currentSkillIndex))
                                continue;

                            // Validate skill index is numeric before trying to use it
                            if (!int.TryParse(currentSkillIndex, out int _))
                            {
                                LogForm.Instance?.AppendDebug($"[Maid] Invalid skill index: {currentSkillIndex}, skipping");
                                int listCount = customSkillSet != null ? customSkillSet.Count : (configurationSkills != null ? configurationSkills.Count : skillList.Length);
                                skillIndex = (skillIndex + 1) % listCount;
                                continue;
                            }

                            //keep using buff when no enemy target & checking waitskill
                            try
                            {
                                string selfskill = skillproperties[int.Parse(currentSkillIndex)]?["tgt"]?.ToString();
                                //debug($"{selfskill}");
                                if (cbWaitSkill.Checked && selfskill != "h") //H or F [H - monster/hostile, F - Friendly/self]
                                {
                                    await Task.Delay(Player.SkillAvailable(currentSkillIndex));
                                    Player.ForceUseSkill(currentSkillIndex);
                                    await Task.Delay(150);
                                    skillIndex = (skillIndex + 1) % (customSkillSet != null ? customSkillSet.Count : skillList.Length);
                                }
                            }
                            catch (Exception ex)
                            {
                                LogForm.Instance?.AppendDebug($"[Maid] Error executing buff check for skill {currentSkillIndex}: {ex.Message}");
                                skillIndex = (skillIndex + 1) % (customSkillSet != null ? customSkillSet.Count : skillList.Length);
                                continue;
                            }

                            // waiting for skill CD if 'Wait' skill checked
                            if (cbWaitSkill.Checked && (Player.SkillAvailable(currentSkillIndex) > 0 || !Player.HasTarget))
                            {
                                await Task.Delay(150);
                                continue;
                            }

                            // do attack with skills
                            if (Player.HasTarget)
                            {
                                try
                                {
                                    //For class with some aura detection or Ultragramiel boss fight
                                    await SpecialCombo();
                                    if (tauntTask == null)
                                        taunt();
                                    // force, to ensure a skill is REALLY executed 
                                    if (forceSkill)
                                    {
                                        string skillAct = numSkillAct.Value.ToString();
                                        LogForm.Instance?.AppendDebug($"[MaidSkillSet] Force executing skill {skillAct}");
                                        await Task.Delay(1000);
                                        await Task.Delay(Player.SkillAvailable(skillAct));
                                        //Player.UseSkill(skillAct);
                                        useSkill(skillAct, true);
                                        forceSkill = false;
                                        await Task.Delay(500);
                                    }
                                    else
                                    {   // normal skill spamming
                                        string skillSource = customSkillSet != null ? $"skillset[{skillIndex}]" : (configurationSkills != null ? $"config[{skillIndex}]" : $"manual[{skillIndex}]");
                                        LogForm.Instance?.AppendDebug($"[MaidSkillSet] Using skill {currentSkillIndex} from {skillSource}");
                                        useSkill(currentSkillIndex);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    LogForm.Instance?.AppendDebug($"[Maid] Error using skill {currentSkillIndex}: {ex.Message}");
                                }
                            }

                            skillIndex++;
                            if (customSkillSet != null)
                                skillIndex = skillIndex % customSkillSet.Count;
                            else if (configurationSkills != null)
                                skillIndex = skillIndex % configurationSkills.Count;
                            else if (skillList != null)
                                skillIndex = skillIndex % skillList.Length;
                        }
                        else if (Player.IsLoggedIn && !World.IsMapLoading)
                        {
                            gotoTarget(targetUsername);
                            if (cbStopIf.Checked)
                            {
                                gotoTry++;
                                if (gotoTry >= 5)
                                {
                                    gotoTry = 0;
                                    stopMaid();
                                }
                            }

                            // wait loading screen before try to goto again (max: 5100 ms)
                            for (int i = 0; i < 36 && cbEnablePlugin.Checked && Player.IsLoggedIn && !World.IsMapLoading; i++)
                                await Task.Delay(150);

                            // wait map loading end
                            while (cbEnablePlugin.Checked && Player.IsLoggedIn && World.IsMapLoading)
                                await Task.Delay(500);

                            // wait 2 second before try to goto or join to different map (when locked map handler is enabled)
                            for (int i = 0; i < 8 && cbEnablePlugin.Checked && cbHandleLockedMap.Checked && Player.IsLoggedIn && !World.IsMapLoading; i++)
                                await Task.Delay(250);

                            // goto target current cell when in the same room
                            while (cbEnablePlugin.Checked && Player.IsLoggedIn && isPlayerInMyRoom && !isPlayerInMyCell)
                            {
                                Player.GoToPlayer(targetUsername);
                                debug("Attempt to chase");
                                if (cbEnablePlugin.Checked && Player.IsLoggedIn && isPlayerInMyRoom && !isPlayerInMyCell)
                                    await Task.Delay(1000);
                                else break;
                            }
                        }

                        await Task.Delay(skillDelay);
                    }
                    catch { }
                }
            }
            else
            {
                stopMaid();
            }
        }

        private async Task waitSkill(string index)
        {
            // Auto-fix for stuck skill 5 bug
            if (index == "5")
            {
                await Player.ResetSkill5IfStuck();
            }
            
            int cd = Player.SkillAvailable(index);
            await Task.Delay(Math.Min(cd, 1500));
            useSkill(index, true);
            for (int i = 0; i < 10 && Player.SkillAvailable(index) == 0; i++)
            {
                await Task.Delay(100); //Ensure its going to cooldown
            }
            useSkill(index, true);
        }

        // Check if an aura condition is met
        private bool CheckAuraCondition(SavedSkill auraConditionSkill)
        {
            if (string.IsNullOrEmpty(auraConditionSkill.Text))
                return true;

            try
            {
                // Parse the aura text: "[Player Aura <] XXI - The World|1|1|0 - The Fool|1|AND"
                string text = auraConditionSkill.Text;
                
                // Determine operator: < or >
                bool isLessThan = text.Contains("[Player Aura <]");
                bool isGreaterThan = text.Contains("[Player Aura >]");
                
                if (!isLessThan && !isGreaterThan)
                    return true;

                // Remove the operator prefix
                string aurasection = text.Replace("[Player Aura <] ", "").Replace("[Player Aura >] ", "");
                
                // Split by | to get aura info
                string[] parts = aurasection.Split('|');
                if (parts.Length < 2)
                    return true;

                // First aura: name and target value
                string aura1Name = parts[0].Trim();
                if (!int.TryParse(parts[1], out int targetValue1))
                    return true;

                // Get FRESH aura value (not cached) for immediate evaluation
                int current1 = Player.GetAuras(true, aura1Name);
                
                LogForm.Instance?.AppendDebug($"[AuraStmt] Evaluating: {aura1Name} = {current1}, Target = {targetValue1}, Operator = {(isLessThan ? "<" : ">")}");

                // Check first aura
                bool condition1Met = isLessThan ? (current1 < targetValue1) : (current1 > targetValue1);

                // Check if there's a second aura condition
                if (parts.Length >= 5)
                {
                    // Multi-aura condition: Aura1|Value1|SkillIndex|Aura2|Value2|Operator
                    string aura2Name = parts[3].Trim();
                    if (!int.TryParse(parts[4], out int targetValue2))
                        return condition1Met;

                    int current2 = Player.GetAuras(true, aura2Name);
                    bool condition2Met = isLessThan ? (current2 < targetValue2) : (current2 > targetValue2);
                    
                    // Get operator (AND or OR)
                    string op = (parts.Length > 5) ? parts[5].Trim().ToUpper() : "AND";
                    
                    bool finalResult = op == "AND" ? (condition1Met && condition2Met) : (condition1Met || condition2Met);
                    
                    LogForm.Instance?.AppendDebug($"[AuraStmt] Multi-aura: {aura1Name}={current1}, {aura2Name}={current2}, Operator={op}, Result={finalResult}");
                    
                    return finalResult;
                }

                LogForm.Instance?.AppendDebug($"[AuraStmt] Single aura condition: {(condition1Met ? "MET" : "NOT met")}");
                return condition1Met;
            }
            catch (Exception ex)
            {
                LogForm.Instance?.AppendDebug($"[AuraStmt] Error evaluating condition: {ex.Message}");
                return true; // Default to true on error
            }
        }

        /// <summary>
        /// Extract skill index from aura condition text
        /// Format: [Player Aura <] AuraName|TargetValue|SkillIndex|...
        /// Returns the SkillIndex (3rd element after split by |)
        /// </summary>
        private string ExtractSkillIndexFromCondition(string conditionText)
        {
            try
            {
                // Remove the operator prefix
                string aurasection = conditionText.Replace("[Player Aura <] ", "").Replace("[Player Aura >] ", "");
                
                // Split by | - format is: AuraName|Threshold|SkillIndex|...
                string[] parts = aurasection.Split('|');
                
                // The skill index is the 3rd element (index 2)
                if (parts.Length >= 3)
                {
                    return parts[2].Trim();
                }
            }
            catch { }
            
            return null;
        }

        private int GetCachedAura(string auraName)
        {
            // COMPLETELY NON-BLOCKING - no Flash calls in main loop
            // Just return what's in cache, or 0 if not cached yet
            
            lock (_auraCache)
            {
                if (_auraCache.ContainsKey(auraName))
                {
                    return _auraCache[auraName];
                }
            }
            
            // Not in cache - return 0 without blocking
            // PreloadAurasForSkillsetAsync will fetch these values in background
            return 0;
        }

        /// <summary>
        /// Pre-load all auras including those needed by SpecialCombo and skillset conditions
        /// Only called every 10 seconds or on player death - not every loop iteration
        /// </summary>
        private async Task PreloadAurasForSkillsetAsync(List<SavedSkill> customSkillSet)
        {
            if (customSkillSet == null) 
            {
                _auraCacheValid = true;
                return;
            }
            
            // Mark as invalid while we're loading
            _auraCacheValid = false;
            
            // Run in background thread
            await Task.Run(() =>
            {
                try
                {
                    // Collect all unique aura names from the skillset
                    var auraNames = new HashSet<string>();
                    
                    foreach (var skill in customSkillSet)
                    {
                        if (skill.Type == 2 && !string.IsNullOrEmpty(skill.Text))
                        {
                            var extracted = ExtractAuraNames(skill.Text);
                            foreach (var name in extracted)
                            {
                                auraNames.Add(name);
                            }
                        }
                    }
                    
                    // Add auras needed by SpecialCombo
                    AddSpecialComboAuras(auraNames);
                    
                    if (auraNames.Count == 0)
                    {
                        _auraCacheValid = true;
                        return;
                    }
                    
                    // Clear old cache
                    lock (_auraCache)
                    {
                        _auraCache.Clear();
                    }
                    
                    LogForm.Instance?.AppendDebug($"[Maid] Preloading {auraNames.Count} auras...");
                    
                    // Fetch each aura with individual timeout
                    int successCount = 0;
                    foreach (var auraName in auraNames)
                    {
                        try
                        {
                            // Create a task for this specific aura
                            var auraTask = Task.Run(() => 
                            {
                                try 
                                { 
                                    return Player.GetAuras(true, auraName); 
                                }
                                catch 
                                { 
                                    return 0; 
                                }
                            });
                            
                            // Wait max 150ms per aura
                            if (auraTask.Wait(TimeSpan.FromMilliseconds(150)))
                            {
                                int value = auraTask.Result;
                                lock (_auraCache)
                                {
                                    _auraCache[auraName] = value;
                                }
                                successCount++;
                            }
                            else
                            {
                                // Timeout - default to 0
                                LogForm.Instance?.AppendDebug($"[Maid] Timeout preloading aura: {auraName}");
                                lock (_auraCache)
                                {
                                    _auraCache[auraName] = 0;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            LogForm.Instance?.AppendDebug($"[Maid] Error preloading aura {auraName}: {ex.Message}");
                            lock (_auraCache)
                            {
                                _auraCache[auraName] = 0;
                            }
                        }
                    }
                    
                    LogForm.Instance?.AppendDebug($"[Maid] Preloaded {successCount}/{auraNames.Count} auras successfully");
                    _auraCacheValid = true;
                    
                    // Confirm cache is ready for condition evaluation
                    if (successCount > 0)
                    {
                        LogForm.Instance?.AppendDebug($"[Maid] Aura cache ready - conditions will now be evaluated");
                    }
                }
                catch (Exception ex)
                {
                    LogForm.Instance?.AppendDebug($"[Maid] Error in PreloadAuras: {ex.Message}");
                    _auraCacheValid = true; // Mark as valid anyway to prevent infinite retry
                }
            });
        }

        /// <summary>
        /// Extract aura names from condition text
        /// Example: "[Player Aura <] XXI - The World|1|1|0 - The Fool|1|AND"
        /// Returns: ["XXI - The World", "0 - The Fool"]
        /// </summary>
        private List<string> ExtractAuraNames(string conditionText)
        {
            var names = new List<string>();
            try
            {
                if (conditionText.Contains("[Player Aura <]") || conditionText.Contains("[Player Aura >]"))
                {
                    // Find content after the bracket
                    int bracketEnd = conditionText.IndexOf("]");
                    if (bracketEnd < 0) return names;
                    
                    string afterBracket = conditionText.Substring(bracketEnd + 1).Trim();
                    
                    // Split by AND/OR
                    var parts = System.Text.RegularExpressions.Regex.Split(afterBracket, @"\s+(AND|OR)\s+");
                    
                    foreach (var part in parts)
                    {
                        if (part == "AND" || part == "OR") continue;
                        
                        // Parse "AuraName|threshold|..."
                        var segments = part.Trim().Split('|');
                        if (segments.Length > 0 && !string.IsNullOrWhiteSpace(segments[0]))
                        {
                            names.Add(segments[0].Trim());
                        }
                    }
                }
            }
            catch { /* Ignore parse errors */ }
            
            return names;
        }

        /// <summary>
        /// Add auras needed by SpecialCombo based on current class and equipped items
        /// </summary>
        private void AddSpecialComboAuras(HashSet<string> auraNames)
        {
            // Potion-related auras (check for equipped potions)
            var potion = Player.Inventory.Items.FirstOrDefault((InventoryItem i) =>
                i.IsEquipped &&
                i.Quantity >= 1 &&
                potionNames.Any(pots => i.Name.IndexOf(pots, StringComparison.OrdinalIgnoreCase) >= 0));
            
            if (potion != null)
            {
                if (potion.Name.Contains("Potent"))
                {
                    auraNames.Add("Potent Honor Malice");
                }
                else if (potion.Name.Contains("Felicitous"))
                {
                    auraNames.Add("Felicitous Philtre");
                }
            }
            
            // Class-specific auras
            string equippedClass = Player.EquippedClass;
            
            if (equippedClass.Contains("CHRONO SHADOW"))
            {
                auraNames.Add("Rounds Empty");
            }
            else if (equippedClass == "ARCANA INVOKER")
            {
                auraNames.Add("0 - The Fool");
                auraNames.Add("XXI - The World");
                auraNames.Add("XX - Judgement");
                auraNames.Add("End of the world");
            }
            else if (equippedClass == "ARCHMAGE")
            {
                auraNames.Add("Arcane Flux");
                auraNames.Add("Corporeal Ascension");
                auraNames.Add("Arcane Sigil");
            }
            
            // Map-specific auras
            if (Player.Map == "ascendedeclipse")
            {
                auraNames.Add("Sun's Heat");
            }
        }

        /// <summary>
        /// Loads a Configuration object from JSON and sets it as the active skillset
        /// Can be called with Configuration JSON pasted directly
        /// </summary>
        public void LoadConfigurationFromJson(string jsonContent, string configName = "Loaded Configuration")
        {
            try
            {
                var settings = new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.Auto,
                    NullValueHandling = NullValueHandling.Ignore
                };
                
                var config = JsonConvert.DeserializeObject<Grimoire.Botting.Configuration>(jsonContent, settings);
                
                if (config != null && config.Skills != null && config.Skills.Count > 0)
                {
                    _selectedConfiguration = config;
                    _selectedSkillSet = configName;
                    LogForm.Instance?.AppendDebug($"[Maid] Loaded Configuration with {config.Skills.Count} skills");
                    logConfigurationSkills(config);
                }
                else
                {
                    LogForm.Instance?.AppendDebug($"[Maid] Configuration loaded but has no skills");
                }
            }
            catch (Exception ex)
            {
                LogForm.Instance?.AppendDebug($"[Maid] Error loading Configuration from JSON: {ex.Message}");
            }
        }

        /// <summary>
        /// Logs the skills in a Configuration for debugging purposes
        /// </summary>
        private void logConfigurationSkills(Grimoire.Botting.Configuration config)
        {
            if (config?.Skills == null) return;
            
            LogForm.Instance?.AppendDebug($"[Maid] Configuration contains {config.Skills.Count} entries:");
            for (int i = 0; i < config.Skills.Count && i < 10; i++) // Log first 10 skills
            {
                var skill = config.Skills[i];
                LogForm.Instance?.AppendDebug($"  [{i}] Index: {skill.Index}, Text: {skill.Text}, Type: {skill.Type}");
            }
            if (config.Skills.Count > 10)
                LogForm.Instance?.AppendDebug($"  ... and {config.Skills.Count - 10} more skills");
        }

        private void useSkill(string skillIndex, bool force = false)
        {
            try
            {
                var skillproperties = Flash.Instance.GetGameObject<List<JObject>>("world.actions.active");
                string skillName = skillproperties != null && int.TryParse(skillIndex, out int idx) && idx < skillproperties.Count
                    ? skillproperties[idx]?["name"]?.ToString() ?? "Unknown"
                    : "Unknown";
                
                LogForm.Instance?.AppendDebug($"[MaidSkill] Executing skill {skillIndex} ({skillName}) - Force: {force}");
                
                if (isUsingCSH() || force)
                {
                    LogForm.Instance?.AppendDebug($"[MaidSkill] Using ForceUseSkill for index {skillIndex}");
                    Player.ForceUseSkill(skillIndex);
                    return;
                }
                LogForm.Instance?.AppendDebug($"[MaidSkill] Using UseSkill for index {skillIndex}");
                Player.UseSkill(skillIndex);
            }
            catch (Exception ex)
            {
                LogForm.Instance?.AppendDebug($"[MaidSkill] Error executing skill {skillIndex}: {ex.Message}");
            }
        }

        private void equipEnrage()
        {
            InventoryItem item = Player.Inventory.Items.FirstOrDefault((InventoryItem i) => i.Name.Equals("Scroll of Enrage") && i.IsEquippable);
            Player.EquipPotion(item.Id, item.Description, item.File, item.Name);
            Task.Delay(1000);
        }

        private void taunt()
        {
            if (tbSpecialMsg.Text.StartsWith("tc;", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    string[] parts = tbSpecialMsg.Text.Split(';');

                    // Default value
                    int cycle = 2;
                    string mon = "*";
                    int second = 12;
                    int order = -1;

                    if (parts.Length > 1 && !int.TryParse(parts[1], out cycle))
                        throw new Exception("Cycle isn't a valid number!");

                    if (parts.Length > 2 && !string.IsNullOrWhiteSpace(parts[2]))
                        mon = parts[2];

                    if (parts.Length > 3 && !int.TryParse(parts[3], out second))
                        throw new Exception("Seconds isn't a valid number!");
                    if (parts.Length > 4 && !int.TryParse(parts[4], out order))
                        order -= 1;

                    if (tauntTask == null && World.IsMonsterAvailable(mon))
                    {
                        ResetToken(true);
                        tauntTask = cycleTaunt(cycle, mon, second, order);
                    }
                }
                catch (Exception ex)
                {
                    Task.Run(() =>
                    MessageBox.Show(
                        $"Wrong format:\n{ex.Message}" +
                        $"\n\nExample: tc;<cycle>;<monster>;<second>" +
                        $"\nHere, i'll fix that for u",
                        "TauntCycle Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning
                    )
                    );
                    Task.Delay(2000);
                    stopMaid();
                    Task.Delay(200);
                    Player.MoveToCell(Player.Cell, Player.Pad);
                    tbSpecialMsg.Text = $"tc;2;*;12;<order 1-4>(optional)";
                }
            }
        }

        private bool isUsingCSH()
        {
            return Player.EquippedClass.Contains("CHRONO SHADOW");
        }

        private string msgTemp;
        string[] potionNames = { "Felicitous Philtre", "Potent Malice", "Potent Honor" };
        private async Task SpecialCombo()
        {
            var potion = Player.Inventory.Items.FirstOrDefault((InventoryItem i) =>
                i.IsEquipped &&
                i.Quantity >= 1 &&
                potionNames.Any(pots => i.Name.IndexOf(pots, StringComparison.OrdinalIgnoreCase) >= 0));
            if (potion != null)
            {
                if (potion.Name.Contains("Potent") && Player.GetAuras(true, "Potent Honor Malice") == 0)
                {
                    await waitSkill("5");
                }
                else if (potion.Name.Contains("Felicitous") && Player.GetAuras(true, "Felicitous Philtre") == 0)
                {
                    await waitSkill("5");
                    //await Task.Delay(Player.SkillAvailable("5"));
                    //useSkill("5");
                }
            }

            if (Player.Map == "voidxyfrag" && Player.EquippedClass == "LEGION REVENANT")
            {
                if (!string.IsNullOrWhiteSpace(msgTemp))
                    return;
                //Save and set specialMsg
                msgTemp = tbSpecialMsg.Text;
                tbSpecialMsg.Text = "bleeee";
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(msgTemp))
                {
                    tbSpecialMsg.Text = msgTemp;
                    msgTemp = string.Empty; // Reset setelah keluar map
                }
            }

            if (isUsingCSH() && (cmbPreset.SelectedItem?.ToString() == "CSH" || _selectedSkillSet == "CSH"))
            {
                if (Player.GetAuras(true, "Rounds Empty") == 1 || Player.Mana < 15)
                {
                    await waitSkill("4");
                    await waitSkill("1");
                    /*useSkill("4");
                    await Task.Delay(Player.SkillAvailable("1"));
                    await Task.Delay(100);
                    useSkill("1");
                    await Task.Delay(200);*/
                }
            }
            
            // Only run AI combo if AI is selected
            if (cmbPreset.SelectedItem?.ToString() == "AI" || _selectedSkillSet == "AI")
            {
                if (Player.EquippedClass == "ARCANA INVOKER")
                {
                    if ((Player.GetAuras(true, "0 - The Fool") == 0 &&
                        Player.GetAuras(true, "XXI - The World") == 0) || 
                        Player.GetAuras(true, "XX - Judgement") == 1 ||
                        Player.GetAuras(true, "End of the world") >= 20)
                    {
                        await waitSkill("1");
                        await Task.Delay(200);
                    }
                }
            }
            
            // Only run AM combo if AM is selected
            if (cmbPreset.SelectedItem?.ToString() == "AM" || _selectedSkillSet == "AM")
            {
                if (Player.EquippedClass == "ARCHMAGE")
                {
                    if (Player.GetAuras(true, "Arcane Flux") == 1 &&
                        Player.GetAuras(true, "Corporeal Ascension") == 0 ||
                        Player.GetAuras(true, "Arcane Sigil") == 0)
                    {
                        await waitSkill("4");
                    }
                }
            }

            // ultra gramiel
            if (Player.Map == "ultragramiel")
            {
                // string target = Player.GetTargetName().ToLower();
                if (Player.GetTargetName?.IndexOf("grace crystal", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    World.IsMonsterAvailable("grace crystal"))
                {
                    CheckCrystalHealthBalance();
                    return;
                }

                if (tauntTask == null)
                {
                    ResetToken(true);
                    tauntTask = cycleTaunt(4, "Gramiel", 20);
                }
                else if (!World.IsMonsterAvailable("Gramiel"))
                {
                    ResetToken(false);
                }
            }
        }
        private void CheckCrystalHealthBalance()
        {
            var monsters = World.GetAllMonsters();
            var L_crystal = monsters.FirstOrDefault(m => m.MonMapID == 2);
            var R_crystal = monsters.FirstOrDefault(m => m.MonMapID == 3);

            if (forceSkill)
            {
                balance = false;
                doPriorityAttack();
                return;
            }

            //Gramiel crystal HP threshold
            const int threshold = 100;
            //Get Current target MonId
            int.TryParse(Flash.GetGameObject("world.myAvatar.target.objData.MonMapID").Replace("\"", ""), out int currentId);

            if (currentId == 2 && L_crystal.Health <= threshold && R_crystal.Health > threshold)
            {
                Player.AttackMonster("id.3");
                balance = true;
                return;
            }
            else if (currentId == 3 && R_crystal.Health <= threshold && L_crystal.Health > threshold)
            {
                Player.AttackMonster("id.2");
                balance = true;
                return;
            }
            balance = false;
        }

        private Task tauntTask = null;
        private CancellationTokenSource cts;
        private void ResetToken(bool createNew)
        {
            if (cts != null)
            {
                cts.Cancel();
                cts.Dispose();
                cts = null;
            }
            tauntTask = null;
            if (createNew)
                cts = new CancellationTokenSource();
        }
        private async Task cycleTaunt(int cycle = 2, string mon = "*", int second = 12, int count = -1)
        {
            if (count == -1)
            {
                count = cmbGotoUsername.Items.IndexOf(Player.Username.ToLower());
            }
            if (count > cycle)
            {
                count %= cycle;
            }
            debug($"Executing tauntcycle with Cycle = {cycle}, Every {second}s, initial taunt at {second / cycle * count}s");
            while (World.IsMonsterAvailable(mon) && !cts.IsCancellationRequested)
            {
                if (count <= 0)
                {
                    debug($"Count = {count} forcing to taunt");
                    count = cycle;
                    forceSkill = true;
                    Player.AttackMonster(mon);
                }
                else
                    debug($"Count = {count}");
                count--;
                await Task.Delay(second / cycle * 1000);
            }
            debug($"Monster no longer detected, stopping taunt cycle");
            ResetToken(true);
        }

        private Grimoire.Networking.Message CreateMessage(string raw)
        {
            if (raw != null && raw.Length > 0)
            {
                switch (raw.Trim()[0])
                {
                    case '%':
                        return new XtMessage(raw);
                    case '<':
                        return new XmlMessage(raw);
                    case '{':
                        return new JsonMessage(raw);
                }
            }

            return null;
        }


        private void AnimsMsgHandler(string function, params object[] args)
        {
            if (function != "packetFromServer") return;
            try
            {
                Networking.Message message = CreateMessage((string)args[0]);
                if (message is JsonMessage)
                {
                    JsonMessage jsonMessage = message as JsonMessage;
                    if (jsonMessage.DataObject?["anims"] != null)
                    {
                        JArray anims = (JArray)jsonMessage.DataObject["anims"];
                        if (anims != null)
                        {
                            //System.Console.WriteLine("anims: " + anims);
                            foreach (JObject anim in anims)
                            {
                                string msg = anim?["msg"]?.ToString()?.ToLower();

                                if (msg != null)
                                {
                                    int monId = 0;

                                    int.TryParse(anim?["tInf"]?.ToString()?.Split(':')[1], out monId);

                                    // Store animation message for bot statement commands
                                    Configuration.LastAnimationMessage = msg;
                                    Configuration.AnimationTriggered = true;

                                    string[] inputMsg = tbSpecialMsg.Text?.ToLower().Split(',');
                                    foreach (string m in inputMsg)
                                    {
                                        string specialMsg = m.Trim();
                                        if (!string.IsNullOrEmpty(specialMsg))
                                        {
                                            if (msg.Contains(specialMsg) && ultraBossHandler(msg, monId))
                                            {
                                                //LogForm.Instance.devDebug($"Forcing taunt into id:{monId}");
                                                forceSkill = true;
                                                return;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                debug($"e: {e}");
            }
        }

        private bool counterAttack = false;

        private void AntiCounterHandler(string function, params object[] args)
        {
            if (function != "packetFromServer") return;
            try
            {
                Grimoire.Networking.Message message = CreateMessage((string)args[0]);
                JsonMessage jsonMessage = message as JsonMessage;
                if (jsonMessage != null)
                {
                    if (jsonMessage.DataObject?["anims"] != null)
                    {
                        JArray anims = (JArray)jsonMessage.DataObject["anims"];
                        if (anims != null)
                        {
                            foreach (JObject anim in anims)
                            {
                                string msg = anim?["msg"]?.ToString()?.ToLower();
                                if (msg != null)
                                {
                                    if (msg.Contains("prepares a counter attack"))
                                    {
                                        //debug("Counter Attack: active");
                                        Task.Run(async () => 
                                        { 
                                            counterAttack = true;
                                            cbStopAttack.Checked = true;
                                            await Task.Delay(10000);
                                            //This function will auto exit Counter/Stop Atk
                                            counterAttack = false;
                                            cbStopAttack.Checked = false;
                                        });
                                    }
                                }
                            }
                        }
                    }
                    if (jsonMessage.DataObject?["a"] != null)
                    {
                        JArray a = (JArray)jsonMessage.DataObject?["a"];
                        if (a != null)
                        {
                            if (Player.GetAuras(true, "Sun's Heat") > 0 || counterAttack)
								cbStopAttack.Checked = true;  
                            else if (Player.Map == "ascendedeclipse") //changed to avoid force uncheck
                                cbStopAttack.Checked = false;
                            foreach (JObject aura in a)
                            {
                                JObject aura2 = (JObject)aura["aura"];
                                if (aura2?["nam"]?.ToString() == "Counter Attack" && aura.GetValue("cmd")?.ToString() == "aura--")
                                {
                                    counterAttack = false;
                                    cbStopAttack.Checked = false;
                                    //debug("Counter Attack: fades");
                                    break;
                                } //Commented Old Counter handling
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                debug($"e: {e}");
            }
        }

        private void debug(string text)
        {
            LogForm.Instance.AppendDebug($"[Maid] {text}");
        }

        private async Task waitForFirstJoin()
        {
            // wait player to join the map
            while (cbEnablePlugin.Checked && World.IsMapLoading)
                await Task.Delay(2000);

            // do first join delay
            if (cbEnablePlugin.Checked)
                await Task.Delay((int)numRelogDelay.Value);
        }

        private void doPriorityAttack()
        {
            if (balance)
                return;

            string currentTarget = Player.GetTargetName;
            for (int i = 0; i < monsterList.Length; i++)
            {
                //if (monsterList[i].Equals(Player.GetTargetName(), StringComparison.OrdinalIgnoreCase))
                if (currentTarget?.IndexOf(monsterList[i], StringComparison.OrdinalIgnoreCase) >= 0)
                    return; //Made special for CSH non autoattack cases

                if (World.IsMonsterAvailable(monsterList[i]))
                {
                    Player.AttackMonster(monsterList[i]);
                    return;
                }
            }
        }

        private bool isPlayerInCombat()
        {
            return (Player.CurrentState == Player.State.InCombat ? true : false);
        }

        private bool IsPlayerInMap(string targetUsername)
        {
            foreach (string player in World.PlayersInMap)
            {
                if (player.ToLower() == targetUsername)
                    return true;
            }
            return false;
        }

        private bool isHealthUnder(int percentage)
        {
            int healthBoundary = Player.HealthMax * percentage / 100;
            return Player.Health <= healthBoundary ? true : false;
        }

        private async void gotoTarget(string targetUsername)
        {
            if (Player.CurrentState != Player.State.Idle)
                Player.MoveToCell("Enter", "Spawn");
            await Task.Delay(500);
            Player.GoToPlayer(targetUsername);
            //await Proxy.Instance.SendToServer($"%xt%zm%cmd%1%goto%{targetUsername}%");
        }

        /* UI state */

        public void startUI()
        {
            cbSpecialAnims.Enabled = false;
            tbSpecialMsg.Enabled = false;
            numSkillAct.Enabled = false;
            cmbGotoUsername.Enabled = false;
            tbSkillList.Enabled = false;
            gbOptions.Enabled = false;
            cbWaitSkill.Enabled = false;
            btnMe.Enabled = false;
            cbCopyWalk.Enabled = false;
            cmbUltraBoss.Enabled = false;
            Root.Instance.maidStrip.Font = new Font("Segoe UI", 9, FontStyle.Bold | FontStyle.Underline);
            if (LockedMapForm.Instance.Visible)
            {
                if (LockedMapForm.Instance.WindowState == FormWindowState.Minimized)
                    LockedMapForm.Instance.WindowState = FormWindowState.Normal;
                LockedMapForm.Instance.Hide();
            }
            if (WhitelistMapForm.Instance.Visible)
            {
                if (WhitelistMapForm.Instance.WindowState == FormWindowState.Minimized)
                    WhitelistMapForm.Instance.WindowState = FormWindowState.Normal;
                WhitelistMapForm.Instance.Hide();
            }
            antiCounter();
        }

        public void stopMaid()
        {
            Proxy.Instance.UnregisterHandler(RedMsgHandler);
            Proxy.Instance.UnregisterHandler(CJHandler);
            Proxy.Instance.UnregisterHandler(JoinMapHandler);
            Proxy.Instance.UnregisterHandler(CopyWalkHandler);
            if (cbSpecialAnims.Checked)
                Flash.FlashCall2 -= AnimsMsgHandler;
            if (cbAntiCounter.Checked)
                Flash.FlashCall2 -= AntiCounterHandler;

            cbSpecialAnims.Enabled = true;
            tbSpecialMsg.Enabled = true;
            numSkillAct.Enabled = true;
            cmbGotoUsername.Enabled = true;
            tbSkillList.Enabled = true;
            gbOptions.Enabled = true;
            cbWaitSkill.Enabled = true;
            btnMe.Enabled = true;
            cbCopyWalk.Enabled = true;
            cmbUltraBoss.Enabled = true;
            cbEnablePlugin.Checked = false;
            onPause = false;
            ResetToken(true);
            if (!string.IsNullOrWhiteSpace(msgTemp))
            {
                tbSpecialMsg.Text = msgTemp;
                msgTemp = string.Empty; // Reset setelah keluar map
            }
            Root.Instance.maidStrip.Font = new Font("Segoe UI", 9, FontStyle.Regular);
        }

        public void resetSpecials()
        {
            if (Player.Map == "ascendeclipse" && Player.Cell != "r3")
            {
                sunConvergenceCount = 0;
                moonConvergenceCount = 0;
            }
            if (Player.Map == "astralshrine" && Player.Cell != "r2")
            {
                beholdOurStarfireCount = 0;
            }
            if (Player.Map == "ultragramiel" && Player.Cell != "r2")
            {
                crystalCount = 0;
            }
            counterAttack = false;
        }

        /* Hotkey */

        private void cbEnableGlobalHotkey_CheckedChanged(object sender, EventArgs e)
        {
            //cbUnfollow.Enabled = cbEnableGlobalHotkey.Checked;
            //cbStopAttack.Enabled = cbEnableGlobalHotkey.Checked;
            if (cbEnableGlobalHotkey.Checked)
            {
                kbh.OnKeyPressed += globalHotkey;
                kbh.OnKeyUnpressed += (s, ek) => { };
                this.KeyDown -= hotkey;

                kbh.HookKeyboard();
            }
            else
            {
                cbStopAttack.Checked = false;
                cbUnfollow.Checked = false;
                kbh.OnKeyPressed -= globalHotkey;
                kbh.OnKeyUnpressed -= (s, ek) => { };
                this.KeyDown += new KeyEventHandler(this.hotkey);

                kbh.UnHookKeyboard();
            }
        }

        private void hotkey(object sender, KeyEventArgs e)
        {
            //if (cmbGotoUsername.Focused || tbAttPriority.Focused || tbSpecialMsg.Focused)
            if (IsFocusedOnBox)
                return;

            switch (e.KeyCode)
            {
                case Keys.R:
                    // LockCell: R
                    e.SuppressKeyPress = true;
                    cbUnfollow.Checked = cbUnfollow.Checked ? false : true;
                    break;
                case Keys.T:
                    // StopAttack: T
                    e.SuppressKeyPress = true;
                    cbStopAttack.Checked = cbStopAttack.Checked ? false : true;
                    break;
            }
        }

        private void globalHotkey(object sender, Keys e)
        {
            //if (cmbGotoUsername.Focused || tbAttPriority.Focused || tbSpecialMsg.Focused)
            if (IsFocusedOnBox)
                return;

            switch (e)
            {
                case Keys.R:
                    // LockCell: R
                    cbUnfollow.Checked = cbUnfollow.Checked ? false : true;
                    break;
                case Keys.T:
                    // StopAttack: T
                    cbStopAttack.Checked = cbStopAttack.Checked ? false : true;
                    break;
            }
        }
        /* Other Control */
        public bool IsFocusedOnBox => this.ActiveControl is TextBox || this.ActiveControl is ComboBox;

        public void pauseFollow()
        {
            if (onPause) return;
            if (cbCopyWalk.Checked)
                Proxy.Instance.UnregisterHandler(CopyWalkHandler);
            onPause = true;
            //debug("onPause: true");
        }

        public void resumeFollow()
        {
            if (!onPause) return;
            if (cbCopyWalk.Checked)
                Proxy.Instance.RegisterHandler(CopyWalkHandler);
            onPause = false;
            //debug("onPause: false");
        }

        private void cbLockCell_CheckedChanged(object sender, EventArgs e)
        {
            if (!cbEnablePlugin.Checked)
                return;
            if (cbUnfollow.Checked)
            {
                Proxy.Instance.UnregisterHandler(CJHandler);
                if (cbCopyWalk.Checked) Proxy.Instance.UnregisterHandler(CopyWalkHandler);
            }
            else
            {
                Proxy.Instance.RegisterHandler(CJHandler);
                if (cbCopyWalk.Checked) Proxy.Instance.RegisterHandler(CopyWalkHandler);
            }
        }

        private void cbStopAttack_CheckedChanged(object sender, EventArgs e)
        {
            // if (cbEnableGlobalHotkey.Checked == false) return;
            if (cbStopAttack.Checked)
            {
                lbStopAttackBg.BackColor = System.Drawing.Color.DeepPink;
                stopwatch.Reset();
                stopwatch.Start();
                timerStopAttack.Enabled = true;
                cbStopAttack.BackColor = System.Drawing.Color.Magenta;
                Player.CancelAutoAttack();
                Player.CancelTarget();
                Player.Rest();
            }
            else
            {
                lbStopAttackBg.BackColor = System.Drawing.Color.Transparent;
                stopwatch.Stop();
                this.Text = "Maid Remake";
                timerStopAttack.Enabled = false;
                cbStopAttack.BackColor = System.Drawing.SystemColors.Control;
            }
        }

        private void cbUseHeal_CheckedChanged(object sender, EventArgs e)
        {
            tbHealSkill.Enabled = !cbUseHeal.Checked;
            numHealthPercent.Enabled = !cbUseHeal.Checked;
            if (cbUseHeal.Checked)
            {
                healSkill = tbHealSkill.Text.Split(',');
            }
        }

        private void cbBuffIfStop_CheckedChanged(object sender, EventArgs e)
        {
            tbBuffSkill.Enabled = !cbBuffIfStop.Checked;
            if (cbBuffIfStop.Checked)
            {
                buffSkill = tbBuffSkill.Text.Split(',');
                buffIndex = 0;
            }
        }

        private void cbAttackPriority_CheckedChanged(object sender, EventArgs e)
        {
            tbAttPriority.Enabled = !cbAttackPriority.Checked;
            if (cbAttackPriority.Checked)
            {
                monsterList = tbAttPriority.Text.Split(',');
            }
        }

        private void timerStopAttack_Tick(object sender, EventArgs e)
        {
            this.Text = $"Maid Remake ({string.Format("{0:hh\\:mm\\:ss}", stopwatch.Elapsed)})";
        }

        private int sunConvergenceCount = 0;
        private int moonConvergenceCount = 0;
        private int crystalCount = 0;
        private int beholdOurStarfireCount = 0;

        private bool ultraBossHandler(string msg, int monId = 0)
        {
            bool act = true;
            if (msg.Contains("shattering"))
            {
                switch (cmbUltraBoss.SelectedItem.ToString())
                {
                    case "Gramiel L1":
                    case "Gramiel L2":
                        if (monId != 2)
                            return false;
                        crystalCount++;
                        debug($"Defense shattering 'Left Crystal' count: {crystalCount}");
                        break;
                    case "Gramiel R1":
                    case "Gramiel R2":
                        if (monId != 3)
                            return false;
                        crystalCount++;
                        debug($"Defense shattering 'Right Crystal' count: {crystalCount}");
                        break;
                }
            }

            if (msg.Contains("sun converge"))
            {
                sunConvergenceCount++;
                debug($"Sun Converges count: {sunConvergenceCount}");
            }
            if (msg.Contains("moon converge"))
            {
                moonConvergenceCount++;
                debug($"Moon Converges count: {moonConvergenceCount}");
            }
            if (msg.Contains("behold our starfire"))
            {
                beholdOurStarfireCount++;
                debug($"Behold our starfire count: {beholdOurStarfireCount}");
            }
            switch (cmbUltraBoss.SelectedItem.ToString())
            {
                case "Asc.Solstice P1":
                    act = sunConvergenceCount % 2 != 0 || !msg.Contains("sun converge");
                    break;
                case "Asc.Solstice P2":
                    act = sunConvergenceCount % 2 == 0 || !msg.Contains("sun converge");
                    break;
                case "Asc.Midnight P1":
                    act = moonConvergenceCount % 2 != 0 || !msg.Contains("moon converge");
                    break;
                case "Asc.Midnight P2":
                    act = moonConvergenceCount % 2 == 0 || !msg.Contains("moon converge");
                    break;
                case "Ast.Empyrean P1":
                    act = beholdOurStarfireCount % 2 != 0 || !msg.Contains("behold our starfire");
                    break;
                case "Ast.Empyrean P2":
                    act = beholdOurStarfireCount % 2 == 0 || !msg.Contains("behold our starfire");
                    break;
                case "Gramiel L1":
                case "Gramiel R1":
                    act = crystalCount % 2 != 0 || !msg.Contains("shattering");
                    break;
                case "Gramiel L2":
                case "Gramiel R2":
                    act = crystalCount % 2 == 0 || !msg.Contains("shattering");
                    break;
            }
            return act;
        }

        private void cmbUltraBoss_SelectedIndexChanged(object sender, EventArgs e)
        {
            switch (cmbUltraBoss.SelectedItem.ToString())
            {
                case "None":
                    tbSpecialMsg.Enabled = true;
                    cbAttackPriority.Enabled = true;
                    numSkillAct.Enabled = true;
                    break;
                case "Asc.Solstice P1":
                case "Asc.Solstice P2":
                    cbAttackPriority.Checked = true;
                    tbAttPriority.Text = "Ascended Solstice";
                    tbSpecialMsg.Text = "sun converges";
                    numSkillAct.Value = 5;
                    break;
                case "Asc.Midnight P1":
                case "Asc.Midnight P2":
                    cbAttackPriority.Checked = true;
                    tbAttPriority.Text = "Ascended Midnight";
                    tbSpecialMsg.Text = "moon converges";
                    numSkillAct.Value = 5;
                    break;
                case "Ast.Empyrean P1":
                case "Ast.Empyrean P2":
                    cbAttackPriority.Checked = true;
                    tbAttPriority.Text = "Astral Empyrean";
                    tbSpecialMsg.Text = "behold our starfire";
                    numSkillAct.Value = 5;
                    break;
                case "Gramiel L1":
                case "Gramiel L2":
                    tbAttPriority.Text = "id.2,crystal";
                    cbAttackPriority.Checked = true;
                    tbSpecialMsg.Text = "shattering";
                    numSkillAct.Value = 5;
                    break;
                case "Gramiel R1":
                case "Gramiel R2":
                    cbAttackPriority.Checked = true;
                    tbAttPriority.Text = "id.3,crystal";
                    tbSpecialMsg.Text = "shattering";
                    numSkillAct.Value = 5;
                    break;
            }
        }

        private void cmbPreset_SelectedIndexChanged(object sender, EventArgs e)
        {
            string selected = cmbPreset.SelectedItem?.ToString() ?? "";
            
            // Skip separator
            if (selected == "---") return;
            
            // Check if it's a custom skillset
            if (SkillSetManager.Instance.SkillSetExists(selected))
            {
                _selectedSkillSet = selected;
                return;
            }
            
            // Otherwise it's a class preset
            _selectedSkillSet = "";
            ClassPreset.cbClear();
            switch (selected)
            {
                case "LR":
                    ClassPreset.LR();
                    break;
                case "LC":
                    ClassPreset.LC();
                    break;
                case "LOO":
                    ClassPreset.LOO();
                    break;
                case "SC":
                    ClassPreset.SC();
                    break;
                case "AP":
                    ClassPreset.AP();
                    break;
                case "CCMD":
                    ClassPreset.CCMD();
                    break;
                case "SSOT":
                    ClassPreset.SSOT();
                    break;
                case "NCM":
                    ClassPreset.NCM();
                    break;
                case "TK":
                    ClassPreset.TK();
                    break;
                case "AI":
                    ClassPreset.AI();
                    break;
                case "AM":
                    ClassPreset.AM();
                    break;
                case "CSH":
                    ClassPreset.CSH();
                    break;
                case "CSH v2":
                    ClassPreset.CSHGunslinger();
                    break;
            }
            ClassPreset.cbSet();
        }

        // get username in cell
        private void cmbGotoUsername_Clicked(object sender, EventArgs e)
        {
            if (World.IsMapLoading)
                return;
            cmbGotoUsername.Items.Clear();
            foreach (string player in World.PlayersInMap)
                cmbGotoUsername.Items.Add(player);
        }

        private void lblLockedMapSetting_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (LockedMapForm.Instance.Visible || LockedMapForm.Instance.WindowState == FormWindowState.Minimized)
            {
                LockedMapForm.Instance.WindowState = FormWindowState.Normal;
                LockedMapForm.Instance.Hide();
            }
            else if (!LockedMapForm.Instance.Visible)
            {
                LockedMapForm.Instance.Show(this);
            }
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            MaidConfig maidConfig = new MaidConfig
            {
                Target = cmbGotoUsername.Text,
                SkillList = tbSkillList.Text,
                SkillDelay = (int)numSkillDelay.Value,
                WaitSkill = cbWaitSkill.Checked,
                StopFailedGoto = cbStopIf.Checked,
                LockedZoneHandler = cbHandleLockedMap.Checked,
                LockedZoneHandlerMaps = LockedMapForm.Instance.tbLockedMapAlternative.Text,
                WhitelistMap = cbWhitelistMap.Checked,
                WhitelistMapMaps = WhitelistMapForm.Instance.tbWhitelistMap.Text,
                RelogDelay = (int)numRelogDelay.Value,
                GlobalHotkey = cbEnableGlobalHotkey.Checked,
                SafeSkill = cbUseHeal.Checked,
                SafeSkillList = tbHealSkill.Text,
                SafeSkillHP = (int)numHealthPercent.Value,
                BuffStopAttack = cbBuffIfStop.Checked,
                BuffStopAttackList = tbBuffSkill.Text,
                AttackPriority = cbAttackPriority.Checked,
                AttackPriorityMonster = tbAttPriority.Text,
                CopyWalk = cbCopyWalk.Checked,
                SpecialMsg = tbSpecialMsg.Text,
                SpecialAct = (int)numSkillAct.Value,
                AntiCounter = cbAntiCounter.Checked,
                UltraBossExtra = cmbUltraBoss.SelectedIndex,
                SelectedSkillSet = _selectedSkillSet,
            };

            string configFolder = Path.Combine(Application.StartupPath, "Config");
            if (!Directory.Exists(configFolder))
                Directory.CreateDirectory(configFolder);

            using (SaveFileDialog saveFileDialog = new SaveFileDialog())
            {
                saveFileDialog.Title = "Save config";
                saveFileDialog.InitialDirectory = configFolder;
                saveFileDialog.Filter = "Maid config|*.json";
                saveFileDialog.DefaultExt = ".json";
                saveFileDialog.CheckFileExists = false;
                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        File.WriteAllText(saveFileDialog.FileName, JsonConvert.SerializeObject(maidConfig, Formatting.Indented));
                        string[] path = saveFileDialog.FileName.Split('\\');
                        gbConfig.Text = $"Config : {path[path.Length - 1]}";
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Unable to save config: " + ex.Message);
                    }
                }
            }
        }

        private void btnLoad_Click(object sender, EventArgs e)
        {
            string configFolder = Path.Combine(Application.StartupPath, "Config");
            if (!Directory.Exists(configFolder))
                Directory.CreateDirectory(configFolder);

            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Title = "Load config";
                openFileDialog.InitialDirectory = configFolder;
                openFileDialog.Filter = "Maid config|*.json";
                openFileDialog.DefaultExt = ".json";
                if (openFileDialog.ShowDialog() == DialogResult.OK &&
                    TryDeserialize(File.ReadAllText(openFileDialog.FileName), out MaidConfig config))
                {
                    gbConfig.Text = $"Config : {openFileDialog.SafeFileName}";
                    cmbUltraBoss.SelectedIndex = config.UltraBossExtra;
                    cmbGotoUsername.Text = config.Target;
                    tbSkillList.Text = config.SkillList;
                    numSkillDelay.Value = config.SkillDelay;
                    cbWaitSkill.Checked = config.WaitSkill;
                    cbStopIf.Checked = config.StopFailedGoto;
                    cbHandleLockedMap.Checked = config.LockedZoneHandler;
                    LockedMapForm.Instance.tbLockedMapAlternative.Text = config.LockedZoneHandlerMaps;
                    cbWhitelistMap.Checked = config.WhitelistMap;
                    WhitelistMapForm.Instance.tbWhitelistMap.Text = config.WhitelistMapMaps;
                    numRelogDelay.Value = config.RelogDelay;
                    cbEnableGlobalHotkey.Checked = config.GlobalHotkey;
                    cbUseHeal.Checked = config.SafeSkill;
                    tbHealSkill.Text = config.SafeSkillList;
                    numHealthPercent.Value = config.SafeSkillHP;
                    cbBuffIfStop.Checked = config.BuffStopAttack;
                    tbBuffSkill.Text = config.BuffStopAttackList;
                    cbAttackPriority.Checked = config.AttackPriority;
                    tbAttPriority.Text = config.AttackPriorityMonster;
                    cbCopyWalk.Checked = config.CopyWalk;
                    tbSpecialMsg.Text = config.SpecialMsg;
                    numSkillAct.Value = config.SpecialAct;
                    cbAntiCounter.Checked = config.AntiCounter;
                    
                    // Load SelectedSkillSet for backward compatibility
                    _selectedSkillSet = config.SelectedSkillSet ?? "";
                }
            }
            if (cbEnableGlobalHotkey.Checked)
                this.KeyDown -= hotkey; //Global hotkey will disable instance hotkey
            
        }

        private bool TryDeserialize(string json, out MaidConfig config)
        {
            try
            {
                config = JsonConvert.DeserializeObject<MaidConfig>(json);
                return true;
            }
            catch (Exception e) { MessageBox.Show(e.ToString()); }
            config = null;
            return false;
        }

        private void cbPartyCmd_CheckedChanged(object sender, EventArgs e)
        {
            if (cbPartyCmd.Checked)
            {
                Proxy.Instance.RegisterHandler(PartyInvitationHandler);
                Proxy.Instance.RegisterHandler(PartyChatHandler);
            }
            else
            {
                Proxy.Instance.UnregisterHandler(PartyInvitationHandler);
                Proxy.Instance.UnregisterHandler(PartyChatHandler);
            }
        }

        private void lblWhitelistMap_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (WhitelistMapForm.Instance.Visible || WhitelistMapForm.Instance.WindowState == FormWindowState.Minimized)
            {
                WhitelistMapForm.Instance.WindowState = FormWindowState.Normal;
                WhitelistMapForm.Instance.Hide();
            }
            else if (!WhitelistMapForm.Instance.Visible)
            {
                WhitelistMapForm.Instance.Show(this);
            }
        }

        private void btnMe_Click(object sender, EventArgs e)
        {
            if (Player.IsLoggedIn) cmbGotoUsername.Text = Player.Username;
        }

        private void cbAntiCounter_CheckedChanged(object sender, EventArgs e)
        {
            antiCounter();
        }
        private void antiCounter()
        {
            Flash.FlashCall2 -= AntiCounterHandler;
            if (cbAntiCounter.Checked)
                Flash.FlashCall2 += AntiCounterHandler;
        }
        private void cbSpecialAnims_CheckedChanged(object sender, EventArgs e)
        {
            tbSpecialMsg.Enabled = cbSpecialAnims.Checked;
            numSkillAct.Enabled = cbSpecialAnims.Checked;
        }
    }
}
