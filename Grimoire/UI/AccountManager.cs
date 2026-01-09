using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Grimoire.Botting;
using Grimoire.Game;
using Grimoire.Game.Data;
using Grimoire.Tools;
using Grimoire.UI;
using Newtonsoft.Json;
using System.IO;
using System.Threading.Tasks;
using System.Net.Http;
using System.Xml;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Drawing;
using DarkUI.Forms;
using DarkUI.Controls;
using Grimoire.Networking;

namespace Grimoire.UI
{
    // Helper class to match the API JSON structure
   public class ServerApiResponse
{
    [JsonProperty("sName")]
    public string Name { get; set; }

    [JsonProperty("iCount")]
    public int PlayerCount { get; set; }

    [JsonProperty("iMax")]
    public int MaxPlayers { get; set; }

    [JsonProperty("bOnline")]
    public int OnlineInt { get; set; }
    
    public bool IsOnline => OnlineInt != 0;

    [JsonProperty("sIP")]
    public string Ip { get; set; }

    [JsonProperty("iPort")]
    public int Port { get; set; }

    [JsonProperty("bUpg")]
    public int MemberOnlyInt { get; set; }
    
    public bool IsMemberOnly => MemberOnlyInt != 0;

    [JsonProperty("sLang")]
    public string Language { get; set; }

    [JsonProperty("iChat")]
    public int ChatLevel { get; set; }

    [JsonProperty("iLevel")]
    public int LevelRequirement { get; set; }
}

    public class AccountManager : DarkForm
    {
        private static AccountManager _instance;
        public static AccountManager Instance
        {
            get
            {
                if (_instance == null || _instance.IsDisposed)
                {
                    _instance = new AccountManager();
                }
                return _instance;
            }
        }
        
        private FlowLayoutPanel flowAccounts;
        private DarkButton btnStartSelected;
        private DarkButton btnRemoveSelected;
        private DarkButton btnStartAll;
        private DarkLabel lblSelectedTop;
        private DarkLabel lblSelectedCount;
        private DarkNumericUpDown nudColumns;
        private DarkComboBox cbServers;
        private FlowLayoutPanel flowServers;
        private DarkButton btnRefreshServers;
        private DateTime _lastRefreshTime = DateTime.MinValue;
        private const int RefreshCooldownMs = 5000; // 5 second cooldown between manual refreshes
        private static readonly HttpClient _httpClient = CreateHttpClient();

        private static HttpClient CreateHttpClient()
        {
            var handler = new HttpClientHandler()
            {
                MaxConnectionsPerServer = 10,
                UseCookies = false
            };
            
            var client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
            
            // Add Basic Authentication header
            string authString = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("449f889db3d655d2ef4a:27863d426bc5bb46c410daf7ed6b479ba4a9f7eb"));
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authString);
            client.DefaultRequestHeaders.Add("User-Agent", "Grimoire/1.0");
            
            return client;
        }

        private ToolTip _toolTip;

        // Add-account controls
        private DarkTextBox tbNewUsername;
        private DarkTextBox tbNewPassword;
        private DarkTextBox tbNewDisplayName;
        private DarkButton btnAddAccount;
        private DarkLabel lblAddHint;
        private DarkGroupBox gbAddAccountTop;
        private HashSet<int> _selected = new HashSet<int>();

        // Script selector
        private DarkTextBox tbScriptPath;
        private DarkButton btnBrowseScript;
        private DarkButton btnSetScriptDir;
        private DarkCheckBox cbStartWithScript;
        private TreeView treeScripts;
        private string _scriptBaseDir = string.Empty;
        private DarkLabel lblLastRefresh;
        private System.Windows.Forms.Timer serverRefreshTimer;

        // Reveal state
        private bool _allRevealed = false;
        private PictureBox _pbRevealAll;
        private System.Drawing.Image _showImage;  // Eye icon
        private System.Drawing.Image _hideImage;  // Hide icon
        
        // Add Account panel state
        private bool _addAccountExpanded = true;  // Default to expanded
        private DarkPanel _addPanelContainer;  // Reference for saving state

        private Config _config;
        private Config _charSelectConfig;  // Separate config for account management
        private List<(string Username, string Password, string DisplayName, bool UseDisplayName)> _accounts = new List<(string, string, string, bool)>();

    private AccountManager()
{
     Text = "Account Manager";
     Width = 1000;
     Height = 700;
     MinimumSize = new System.Drawing.Size(900, 600);
     StartPosition = FormStartPosition.CenterScreen;
     Icon = global::Properties.Resources.GrimoireIcon;

    // Maid theme colors - unified dark theme with outlines
    var bgDark = System.Drawing.Color.FromArgb(30, 30, 38);
    var outlineColor = System.Drawing.Color.FromArgb(60, 60, 70);
    var textColor = System.Drawing.Color.FromArgb(220, 220, 230);

    this.BackColor = bgDark;

    // Main container with sidebar + content (LEFT = Accounts, RIGHT = Servers)
    var mainSplit = new SplitContainer
    {
        Dock = DockStyle.Fill,
        BackColor = bgDark,
        BorderStyle = BorderStyle.None
    };
    mainSplit.Panel1.BackColor = bgDark;
    mainSplit.Panel2.BackColor = bgDark;
    mainSplit.SplitterWidth = 1;

    // ===== LEFT PANEL (Empty for now) =====
    var leftPanel = new DarkPanel { Dock = DockStyle.Fill, BackColor = bgDark };

    // ===== RIGHT PANEL (Account List + Server Selector + Buttons) =====
    var rightPanel = new DarkPanel { Dock = DockStyle.Fill, BackColor = bgDark };

    flowAccounts = new FlowLayoutPanel
    {
        Dock = DockStyle.Fill,
        BackColor = bgDark,
        AutoScroll = true,
        WrapContents = true,
        FlowDirection = FlowDirection.LeftToRight,
        Padding = new Padding(0)
    };
    flowAccounts.Resize += (s, e) => UpdateCardSizes();
    flowAccounts.VisibleChanged += (s, e) => UpdateCardSizes();

    var lblLoginServer = new DarkLabel
    {
        Text = "Login Server",
        Dock = DockStyle.Top,
        Height = 20,
        AutoSize = false,
        TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
        ForeColor = textColor,
        Font = new System.Drawing.Font("Segoe UI", 9f, System.Drawing.FontStyle.Bold),
        Padding = new Padding(2, 0, 0, 2),
        Cursor = System.Windows.Forms.Cursors.Hand
    };

    lblLastRefresh = new DarkLabel
    {
        Text = "Last refresh: Never",
        Dock = DockStyle.Top,
        Height = 18,
        AutoSize = false,
        TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
        ForeColor = System.Drawing.Color.FromArgb(120, 120, 130),
        Font = new System.Drawing.Font("Segoe UI", 7.5f),
        Padding = new Padding(2, 2, 0, 0)
    };

    flowServers = new FlowLayoutPanel
    {
        Dock = DockStyle.Fill,
        BackColor = bgDark,
        AutoScroll = true,
        WrapContents = true,
        FlowDirection = FlowDirection.LeftToRight,
        Padding = new Padding(6)
    };
    flowServers.Resize += (s, e) => UpdateServerSizes();
    flowServers.VisibleChanged += (s, e) => UpdateServerSizes();

    btnRefreshServers = new DarkButton
    {
        Dock = DockStyle.Right,
        Width = 90,
        Height = 23,
        Text = "Refresh"
    };
    btnRefreshServers.Click += async (s, e) =>
    {
        // Rate limiting - prevent refresh spam
        if ((DateTime.Now - _lastRefreshTime).TotalMilliseconds < RefreshCooldownMs)
        {
            MessageBox.Show($"Please wait {RefreshCooldownMs / 1000} seconds between refreshes.");
            return;
        }
        
        _lastRefreshTime = DateTime.Now;
        btnRefreshServers.Enabled = false;
        btnRefreshServers.Text = "Refreshing...";
        
        try
        {
            bool ok = await TryFetchServersAsync();
            if (!ok)
            {
                await Task.Run(() =>
                {
                    try { AutoRelogin.ResetServers(); } catch { }
                });
            }
        }
        finally
        {
            btnRefreshServers.Text = "Refresh";
            btnRefreshServers.Enabled = true;
        }
    };

    // Create server panel container with collapsible header
    var serverPanelContainer = new DarkPanel
    {
        Dock = DockStyle.Fill,
        BackColor = bgDark,
        Padding = new Padding(0),
        BorderStyle = BorderStyle.FixedSingle,
        ForeColor = outlineColor
    };



    // Server controls panel (login server, combobox, refresh) - ALWAYS VISIBLE
    var serverControlsPanel = new DarkPanel
    {
        Dock = DockStyle.Top,
        Height = 74,
        BackColor = bgDark,
        Padding = new Padding(8, 8, 8, 8),
        BorderStyle = BorderStyle.FixedSingle,
        ForeColor = outlineColor
    };

    var serverRow = new DarkPanel
    {
        Dock = DockStyle.Top,
        Height = 23,
        Padding = new Padding(0),
        BackColor = bgDark
    };

    // Spacer between combobox and button
    var serverSpacer = new DarkPanel
    {
        Dock = DockStyle.Right,
        Width = 8,
        BackColor = bgDark
    };

    // Create a fixed-height container for the combobox
    var comboContainer = new Panel
    {
        Dock = DockStyle.Fill,
        Height = 23,
        Padding = new Padding(0, 0, 0, 0),
        BackColor = bgDark
    };

    cbServers = new DarkComboBox
    {
        Width = comboContainer.Width,
        Top = 0,
        Left = 0,
        Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top,
        DropDownStyle = ComboBoxStyle.DropDownList,
        Font = new System.Drawing.Font("Segoe UI", 9f)
    };
    cbServers.DisplayMember = "Name";
    
    cbServers.SelectedIndexChanged += (s, e) =>
    {
        if (cbServers.SelectedItem is Server s1)
        {
            try { Proxy.Instance.DestinationServerOverride = s1; } catch { }
            try
            {
                if (flowServers != null && !flowServers.IsDisposed)
                {
                    foreach (Control c in flowServers.Controls)
                    {
                        if (c.Tag is Server ss && ss == s1)
                        {
                            c.BackColor = System.Drawing.Color.FromArgb(60, 80, 120);
                            try { flowServers.ScrollControlIntoView(c); } catch { }
                        }
                        else
                        {
                            c.BackColor = System.Drawing.Color.FromArgb(50, 50, 62);
                        }
                    }
                }
            }
            catch { }
        }
    };

    // Force combo to resize with container
    comboContainer.Resize += (s, e) => 
    {
        if (cbServers != null && !cbServers.IsDisposed)
            cbServers.Width = Math.Max(50, comboContainer.Width);
    };

    comboContainer.Controls.Add(cbServers);
    // Add in right-to-left order: Button (rightmost), Spacer, Combobox (fills left)
    serverRow.Controls.Add(comboContainer);
    serverRow.Controls.Add(serverSpacer);
    serverRow.Controls.Add(btnRefreshServers);

    serverControlsPanel.Controls.Add(serverRow);
    serverControlsPanel.Controls.Add(lblLastRefresh);
    
    // Wrap Login Server label in a panel with border for outline
    var loginServerContainer = new DarkPanel
    {
        Dock = DockStyle.Top,
        Height = 20,
        BackColor = bgDark,
        Padding = new Padding(0),
        BorderStyle = BorderStyle.FixedSingle,
        ForeColor = outlineColor
    };
    loginServerContainer.Controls.Add(lblLoginServer);
    
    serverControlsPanel.Controls.Add(loginServerContainer);
    serverRow.BringToFront();

    // Make Login Server label clickable to toggle server list
    lblLoginServer.Click += (s, e) => ToggleServerPanel(serverPanelContainer);
    loginServerContainer.Click += (s, e) => ToggleServerPanel(serverPanelContainer);

    // Server list container - COLLAPSIBLE
    var serversListContainer = new DarkPanel
    {
        Dock = DockStyle.Fill,
        BackColor = bgDark,
        Padding = new Padding(6),
        BorderStyle = BorderStyle.None,
        ForeColor = outlineColor
    };
    serversListContainer.Controls.Add(flowServers);

    // Add to container in order: controls (always visible), then server list (collapsible)
    serverPanelContainer.Controls.Add(serversListContainer);
    serverPanelContainer.Controls.Add(serverControlsPanel);

    rightPanel.Controls.Add(serverPanelContainer);

    // Selection info integrated into add panel header
    lblSelectedTop = new DarkLabel
    {
        Text = "Selected: 0",
        Dock = DockStyle.Right,
        Width = 100,
        TextAlign = System.Drawing.ContentAlignment.MiddleRight,
        ForeColor = System.Drawing.Color.FromArgb(150, 180, 220),
        Font = new System.Drawing.Font("Segoe UI", 9f, System.Drawing.FontStyle.Bold)
    };

    // Add Account Section with collapsible header
    var addPanelContainer = new DarkPanel
    {
        Dock = DockStyle.Top,
        Height = 108,
        BackColor = bgDark,
        Padding = new Padding(0),
        BorderStyle = BorderStyle.FixedSingle,
        ForeColor = outlineColor
    };
    _addPanelContainer = addPanelContainer;  // Store reference for saving state

    var addHeader = new DarkPanel
    {
        Dock = DockStyle.Top,
        Height = 32,
        BackColor = System.Drawing.Color.FromArgb(25, 25, 32),
        Padding = new Padding(12, 6, 12, 6)
    };

    var lblAddTitle = new DarkLabel
    {
        Text = "▼ Add Account",
        Dock = DockStyle.Left,
        Width = 150,
        TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
        ForeColor = textColor,
        Font = new System.Drawing.Font("Segoe UI", 9f, System.Drawing.FontStyle.Bold),
        Cursor = System.Windows.Forms.Cursors.Hand
    };

    // Load images for reveal toggle
    _showImage = global::Properties.Resources.eye;  // Eye icon
    _hideImage = global::Properties.Resources.rhide;  // Hide icon
    
    // Load saved reveal state from config
    _allRevealed = false; // Default to false, will be set from config after loading
    
    var pbRevealAll = new PictureBox
    {
        Size = new System.Drawing.Size(40, 20),
        Dock = DockStyle.Right,
        Margin = new Padding(4, 0, 12, 0),
        BackgroundImageLayout = ImageLayout.None,
        SizeMode = PictureBoxSizeMode.StretchImage,
        TabStop = false,
        Cursor = System.Windows.Forms.Cursors.Hand,
        Image = _showImage  // Start with Show (eye) icon
    };
    _pbRevealAll = pbRevealAll;

    var addPanel = new DarkPanel
    {
        Dock = DockStyle.Bottom,
        Height = 76,
        BackColor = bgDark,
        Padding = new Padding(12, 4, 12, 8)
    };

    addHeader.Controls.Add(lblSelectedTop);
    addHeader.Controls.Add(pbRevealAll);
    addHeader.Controls.Add(lblAddTitle);
    
    pbRevealAll.Click += (s, e) =>
    {
        _allRevealed = !_allRevealed;
        // Update icon based on state
        if (_showImage != null && _hideImage != null)
        {
            pbRevealAll.Image = _allRevealed ? _hideImage : _showImage;  // Hide icon when revealed, Show icon when hidden
        }
        foreach (Control accountItem in flowAccounts.Controls)
        {
            var label = accountItem.Controls.OfType<DarkLabel>().FirstOrDefault();
            if (label != null && accountItem.Tag is (string username, string displayText))
            {
                label.Text = _allRevealed ? username : displayText;
            }
        }
        // Save reveal state to config
        _config.Contents["_allRevealed"] = _allRevealed.ToString();
        _config.Save();
    };
    
    addHeader.Click += (s, e) => ToggleAddPanel(addPanelContainer, addPanel, lblAddTitle);
    lblAddTitle.Click += (s, e) => ToggleAddPanel(addPanelContainer, addPanel, lblAddTitle);

    tbNewUsername = new DarkTextBox
    {
        Width = 150,
        Height = 26,
        Text = "Username",
        ForeColor = System.Drawing.Color.FromArgb(150, 150, 150),
        Margin = new Padding(0, 0, 4, 0)
    };
    tbNewUsername.GotFocus += (s, e) =>
    {
        if (tbNewUsername.Text == "Username")
        {
            tbNewUsername.Text = "";
            tbNewUsername.ForeColor = System.Drawing.Color.FromArgb(200, 200, 200);
        }
    };
    tbNewUsername.LostFocus += (s, e) =>
    {
        if (string.IsNullOrWhiteSpace(tbNewUsername.Text))
        {
            tbNewUsername.Text = "Username";
            tbNewUsername.ForeColor = System.Drawing.Color.FromArgb(150, 150, 150);
        }
    };

    tbNewPassword = new DarkTextBox
    {
        Width = 150,
        Height = 26,
        Text = "Password",
        ForeColor = System.Drawing.Color.FromArgb(150, 150, 150),
        UseSystemPasswordChar = false,
        Margin = new Padding(0, 0, 4, 0)
    };
    tbNewPassword.GotFocus += (s, e) =>
    {
        if (tbNewPassword.Text == "Password")
        {
            tbNewPassword.Text = "";
            tbNewPassword.ForeColor = System.Drawing.Color.FromArgb(200, 200, 200);
            tbNewPassword.UseSystemPasswordChar = true;
        }
    };
    tbNewPassword.LostFocus += (s, e) =>
    {
        if (string.IsNullOrWhiteSpace(tbNewPassword.Text))
        {
            tbNewPassword.Text = "Password";
            tbNewPassword.ForeColor = System.Drawing.Color.FromArgb(150, 150, 150);
            tbNewPassword.UseSystemPasswordChar = false;
        }
    };

    tbNewDisplayName = new DarkTextBox
    {
        Width = 150,
        Height = 26,
        Text = "Display Name",
        ForeColor = System.Drawing.Color.FromArgb(150, 150, 150),
        Margin = new Padding(0, 0, 4, 0)
    };
    tbNewDisplayName.GotFocus += (s, e) =>
    {
        if (tbNewDisplayName.Text == "Display Name")
        {
            tbNewDisplayName.Text = "";
            tbNewDisplayName.ForeColor = System.Drawing.Color.FromArgb(200, 200, 200);
        }
    };
    tbNewDisplayName.LostFocus += (s, e) =>
    {
        if (string.IsNullOrWhiteSpace(tbNewDisplayName.Text))
        {
            tbNewDisplayName.Text = "Display Name";
            tbNewDisplayName.ForeColor = System.Drawing.Color.FromArgb(150, 150, 150);
        }
    };

    btnAddAccount = new DarkButton
    {
        Width = 73,
        Height = 28,
        Text = "Add",
        Font = new System.Drawing.Font("Segoe UI", 9f, System.Drawing.FontStyle.Bold),
        Margin = new Padding(0, 0, 4, 0)
    };
    btnAddAccount.Click += BtnAddAccount_Click;

    btnRemoveSelected = new DarkButton
    {
        Width = 73,
        Height = 28,
        Text = "Remove",
        Font = new System.Drawing.Font("Segoe UI", 9f, System.Drawing.FontStyle.Bold),
        Margin = new Padding(0, 0, 0, 0)
    };
    btnRemoveSelected.Click += BtnRemoveSelected_Click;

    var addInputPanel = new FlowLayoutPanel
    {
        Dock = DockStyle.Top,
        Height = 32,
        BackColor = bgDark,
        FlowDirection = FlowDirection.LeftToRight,
        AutoScroll = false,
        WrapContents = false,
        Padding = new Padding(0)
    };
    addInputPanel.Controls.Add(tbNewUsername);
    addInputPanel.Controls.Add(tbNewPassword);
    addInputPanel.Controls.Add(tbNewDisplayName);

    var addButtonPanel = new FlowLayoutPanel
    {
        Dock = DockStyle.Top,
        Height = 32,
        BackColor = bgDark,
        FlowDirection = FlowDirection.LeftToRight,
        AutoScroll = false,
        WrapContents = false,
        Padding = new Padding(0)
    };
    addButtonPanel.Controls.Add(btnAddAccount);
    addButtonPanel.Controls.Add(btnRemoveSelected);

    var lblColumns = new DarkLabel
    {
        Width = 60,
        Height = 20,
        Text = "Columns:",
        ForeColor = System.Drawing.Color.FromArgb(200, 200, 200),
        Font = new System.Drawing.Font("Segoe UI", 9f),
        TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
        Margin = new Padding(10, 0, 4, 0)
    };

    nudColumns = new DarkNumericUpDown
    {
        Width = 50,
        Height = 28,
        Minimum = 1,
        Maximum = 10,
        Value = 2,
        Margin = new Padding(0)
    };
    nudColumns.ValueChanged += (s, ev) => UpdateCardSizes();

    addButtonPanel.Controls.Add(lblColumns);
    addButtonPanel.Controls.Add(nudColumns);

    addPanel.Controls.Add(addButtonPanel);
    addPanel.Controls.Add(addInputPanel);

    addPanelContainer.Controls.Add(addHeader);
    addPanelContainer.Controls.Add(addPanel);
    addHeader.BringToFront();

    // Script selector panel
    var scriptPanel = new DarkPanel
    {
        Dock = DockStyle.Bottom,
        Height = 280,
        BackColor = bgDark,
        Padding = new Padding(12, 8, 12, 8),
        BorderStyle = BorderStyle.FixedSingle,
        ForeColor = outlineColor
    };

    var lblScript = new DarkLabel
    {
        Text = "Script Path:",
        Dock = DockStyle.Top,
        Height = 20,
        ForeColor = textColor,
        Font = new System.Drawing.Font("Segoe UI", 9f, System.Drawing.FontStyle.Bold),
        TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
        Padding = new Padding(0, 0, 0, 2)
    };

    var scriptInputPanel = new DarkPanel
    {
        Dock = DockStyle.Top,
        Height = 23,
        BackColor = bgDark,
        Padding = new Padding(0)
    };

    btnBrowseScript = new DarkButton
    {
        Dock = DockStyle.Right,
        Width = 90,
        Height = 23,
        Text = "Browse",
        Font = new System.Drawing.Font("Segoe UI", 9f)
    };
    btnBrowseScript.Click += BtnBrowseScript_Click;

    var btnSpacer = new DarkPanel
    {
        Dock = DockStyle.Right,
        Width = 5,
        BackColor = bgDark
    };

    btnSetScriptDir = new DarkButton
    {
        Dock = DockStyle.Right,
        Width = 90,
        Height = 23,
        Text = "Set",
        Font = new System.Drawing.Font("Segoe UI", 9f)
    };
    btnSetScriptDir.Click += BtnSetScriptDir_Click;

    // Create a fixed-height container for the textbox
    var textboxContainer = new Panel
    {
        Dock = DockStyle.Fill,
        Height = 23,
        Padding = new Padding(0, 0, 4, 0),
        BackColor = bgDark
    };

    tbScriptPath = new DarkTextBox
    {
        Width = textboxContainer.Width - 4,
        Top = 0,
        Left = 0,
        Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top,
        Font = new System.Drawing.Font("Segoe UI", 9f)
    };

    // Force textbox to resize with container
    textboxContainer.Resize += (s, e) => 
    {
        if (tbScriptPath != null && !tbScriptPath.IsDisposed)
            tbScriptPath.Width = Math.Max(50, textboxContainer.Width - 4);
    };

    textboxContainer.Controls.Add(tbScriptPath);

    // Add in right-to-left order
    scriptInputPanel.Controls.Add(textboxContainer);
    scriptInputPanel.Controls.Add(btnSetScriptDir);
    scriptInputPanel.Controls.Add(btnSpacer);
    scriptInputPanel.Controls.Add(btnBrowseScript);

    // TreeView for script files
    treeScripts = new TreeView
    {
        Dock = DockStyle.Fill,
        BackColor = System.Drawing.Color.FromArgb(46, 46, 56),
        ForeColor = System.Drawing.Color.Gainsboro,
        LineColor = System.Drawing.Color.DarkGray,
        BorderStyle = BorderStyle.None,
        Font = new System.Drawing.Font("Segoe UI", 9f)
    };
    treeScripts.AfterSelect += TreeScripts_AfterSelect;
    treeScripts.AfterExpand += TreeScripts_AfterExpand;

    var treePanel = new DarkPanel
    {
        Dock = DockStyle.Fill,
        BackColor = bgDark,
        Padding = new Padding(0, 8, 0, 0)
    };
    treePanel.Controls.Add(treeScripts);

    scriptPanel.Controls.Add(treePanel);

    cbStartWithScript = new DarkCheckBox
    {
        Dock = DockStyle.Fill,
        Text = "Start with Script",
        Font = new System.Drawing.Font("Segoe UI", 9f)
    };

    var cbHost = new DarkPanel
    {
        Dock = DockStyle.Top,
        Height = 26,
        BackColor = bgDark,
        BorderStyle = BorderStyle.None
    };
    cbHost.Controls.Add(cbStartWithScript);

    scriptPanel.Controls.Add(cbHost);
    scriptPanel.Controls.Add(scriptInputPanel);
    scriptPanel.Controls.Add(lblScript);

    // Bottom action buttons
    var bottomBar = new DarkPanel
    {
        Dock = DockStyle.Bottom,
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        BackColor = bgDark,
        Padding = new Padding(12, 8, 12, 8),
        BorderStyle = BorderStyle.FixedSingle,
        ForeColor = outlineColor
    };

    var bottomLayout = new TableLayoutPanel
    {
        Dock = DockStyle.Fill,
        ColumnCount = 2,
        RowCount = 1,
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        BackColor = bgDark,
        Padding = new Padding(0)
    };
    bottomLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
    bottomLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
    bottomLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

    btnStartAll = new DarkButton
    {
        Dock = DockStyle.Fill,
        Height = 36,
        Text = "▶ Start All",
        Font = new System.Drawing.Font("Segoe UI", 9.5f, System.Drawing.FontStyle.Bold)
    };
    btnStartAll.Click += BtnStartAll_Click;

    btnStartSelected = new DarkButton
    {
        Dock = DockStyle.Fill,
        Height = 36,
        Text = "Start Selected",
        Font = new System.Drawing.Font("Segoe UI", 9f)
    };
    btnStartSelected.Click += BtnStartSelected_Click;

    bottomLayout.Controls.Add(btnStartAll, 0, 0);
    bottomLayout.Controls.Add(btnStartSelected, 1, 0);

    bottomBar.Controls.Add(bottomLayout);

    rightPanel.Controls.Add(scriptPanel);
    rightPanel.Controls.Add(addPanelContainer);

    // Add flowAccounts and buttons to leftPanel
    leftPanel.Controls.Add(bottomBar);
    leftPanel.Controls.Add(flowAccounts);

    mainSplit.Panel1.Controls.Add(leftPanel);
    mainSplit.Panel2.Controls.Add(rightPanel);

    Controls.Add(mainSplit);
    
    // Now that the control is added to the form, set minimums and splitter distance
    try
    {
        mainSplit.Panel1MinSize = 250;
        mainSplit.Panel2MinSize = 260;
        mainSplit.SplitterDistance = 400;
    }
    catch
    {
        // If sizing fails, fallback is auto-calculated
    }
    
    // Hook resize event for splitter clamping
    mainSplit.SizeChanged += (s, e) => ClampSplitter(mainSplit);

    _toolTip = new ToolTip();

    LoadAccounts();
    LoadScriptDirectory();
    // Don't load defaults here - let the API load real data first

    // Setup auto-refresh timer for server player counts (every 30 seconds)
    serverRefreshTimer = new System.Windows.Forms.Timer();
    serverRefreshTimer.Interval = 30000; // 30 seconds
    serverRefreshTimer.Tick += async (s, e) =>
    {
        try
        {
            await TryFetchServersAsync();
        }
        catch { }
    };
    serverRefreshTimer.Start();

    this.Load += async (s, e) =>
    {
        try
        {
            Console.WriteLine("[FORM LOAD] Starting server load on form load");
            
            // Show loading state while fetching
            if (cbServers != null) cbServers.Text = "Loading servers...";
            
            // Fetch servers immediately and wait for result
            bool ok = await TryFetchServersAsync();
            Console.WriteLine($"[FORM LOAD] API fetch result: {ok}");
            
            if (!ok)
            {
                // Only load defaults if API fetch failed
                Console.WriteLine("[FORM LOAD] API fetch failed, loading defaults");
                LoadDefaultServers();
            }
            
            // Cleanup
            await Task.Run(() =>
            {
                try { AutoRelogin.ResetServers(); } catch { }
            });
            
            Console.WriteLine("[FORM LOAD] Server load completed");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FORM LOAD] Exception during server fetch: {ex.Message}");
            Console.WriteLine($"[FORM LOAD] Stack: {ex.StackTrace}");
            // Load defaults on exception too
            try { LoadDefaultServers(); } catch { }
        }
    };
}

        private System.Windows.Forms.Timer _animationTimer;
        private int _targetHeight = 0;
        private int _startHeight = 0;
        private bool _isExpanding = false;
        private DarkPanel _animatingContainer;
        private DarkPanel _animatingPanel;
        private DarkLabel _animatingHeader;
        private DateTime _animationStartTime;
        private const int AnimationDurationMs = 250; // 250ms animation duration

        private void ToggleAddPanel(DarkPanel container, DarkPanel panel, DarkLabel header)
        {
            // Stop any existing animation
            if (_animationTimer != null)
            {
                _animationTimer.Stop();
                _animationTimer.Dispose();
            }

            _animatingContainer = container;
            _animatingPanel = panel;
            _animatingHeader = header;
            _startHeight = container.Height;

            if (container.Height > 32) // More than just header height
            {
                // Collapse animation - servers move up
                _isExpanding = false;
                _targetHeight = 32; // Just the header
                _addAccountExpanded = false;  // Save state
            }
            else
            {
                // Expand animation - servers move down
                _isExpanding = true;
                _targetHeight = 108; // Full height
                _addAccountExpanded = true;  // Save state
            }
            
            // Save state to config
            _config.Contents["_addAccountExpanded"] = _addAccountExpanded.ToString();
            _config.Save();

            _animationStartTime = DateTime.Now;
            _animationTimer = new System.Windows.Forms.Timer();
            _animationTimer.Interval = 16; // ~60fps
            _animationTimer.Tick += AnimatePanel;
            _animationTimer.Start();
        }

        private void AnimatePanel(object sender, EventArgs e)
        {
            if (_animatingContainer == null) return;

            // Calculate progress (0 to 1) based on elapsed time
            int elapsedMs = (int)(DateTime.Now - _animationStartTime).TotalMilliseconds;
            float progress = Math.Min(elapsedMs / (float)AnimationDurationMs, 1f);

            // Easing function for smooth animation
            float easedProgress = progress < 0.5f 
                ? 2f * progress * progress 
                : 1f - (float)Math.Pow(-2f * progress + 2f, 2f) / 2f;

            // Calculate new height based on progress
            int newHeight = (int)(_startHeight + (_targetHeight - _startHeight) * easedProgress);
            _animatingContainer.Height = newHeight;

            if (_isExpanding)
            {
                _animatingPanel.Visible = true; // Show panel during expansion
            }
            else
            {
                _animatingPanel.Visible = true; // Keep panel visible during collapse for smooth disappearing
            }

            // Check if animation is complete
            if (progress >= 1f)
            {
                _animatingContainer.Height = _targetHeight;
                
                if (_isExpanding)
                {
                    _animatingContainer.BorderStyle = BorderStyle.FixedSingle; // Restore border when expanded
                    _animatingPanel.Visible = true;
                    _animatingHeader.Text = "▼ Add Account";
                }
                else
                {
                    _animatingPanel.Visible = false;
                    _animatingContainer.BorderStyle = BorderStyle.None; // Remove border when collapsed
                    _animatingHeader.Text = "► Add Account";
                }

                _animationTimer.Stop();
                _animationTimer.Dispose();
            }
        }

        private System.Windows.Forms.Timer _serverAnimationTimer;
        private int _serverTargetHeight = 0;
        private bool _serverIsExpanding = false;
        private DarkPanel _serverAnimatingContainer;

        private void ToggleServerPanel(DarkPanel container)
        {
            // Stop any existing animation
            if (_serverAnimationTimer != null)
            {
                _serverAnimationTimer.Stop();
                _serverAnimationTimer.Dispose();
            }

            _serverAnimatingContainer = container;

            if (container.Height > 24)
            {
                // Collapse animation
                _serverIsExpanding = false;
                _serverTargetHeight = 24;
            }
            else
            {
                // Expand animation to fill available space
                _serverIsExpanding = true;
                _serverTargetHeight = container.Parent.ClientSize.Height - 350; // Leave room for Add Account and Script panel
            }

            _serverAnimationTimer = new System.Windows.Forms.Timer();
            _serverAnimationTimer.Interval = 10;
            _serverAnimationTimer.Tick += AnimateServerPanel;
            _serverAnimationTimer.Start();
        }

        private void AnimateServerPanel(object sender, EventArgs e)
        {
            if (_serverAnimatingContainer == null) return;

            const int animationSpeed = 8; // pixels per frame
            int currentHeight = _serverAnimatingContainer.Height;

            if (_serverIsExpanding)
            {
                if (currentHeight < _serverTargetHeight)
                {
                    _serverAnimatingContainer.Height = Math.Min(currentHeight + animationSpeed, _serverTargetHeight);
                }
                else
                {
                    _serverAnimatingContainer.Height = _serverTargetHeight;
                    _serverAnimationTimer.Stop();
                    _serverAnimationTimer.Dispose();
                }
            }
            else
            {
                if (currentHeight > _serverTargetHeight)
                {
                    _serverAnimatingContainer.Height = Math.Max(currentHeight - animationSpeed, _serverTargetHeight);
                }
                else
                {
                    _serverAnimatingContainer.Height = _serverTargetHeight;
                    _serverAnimationTimer.Stop();
                    _serverAnimationTimer.Dispose();
                }
            }
        }

        private void ClampSplitter(SplitContainer sc)
        {
            try
            {
                if (sc.Width <= 0 || sc.Height <= 0)
                    return;

                int min1 = sc.Panel1MinSize;
                int min2 = sc.Panel2MinSize;
                int splitterWidth = sc.SplitterWidth;
                
                // Calculate valid range
                int minDistance = min1;
                int maxDistance = sc.Width - min2 - splitterWidth;
                
                // If window is too small, reduce panel minimums proportionally
                if (maxDistance < minDistance)
                {
                    // Adjust to best-effort split
                    minDistance = Math.Max(1, sc.Width / 3);
                    maxDistance = sc.Width - minDistance - splitterWidth;
                }
                
                int current = sc.SplitterDistance;
                if (current < minDistance || current > maxDistance)
                {
                    int newDistance = Math.Max(minDistance, Math.Min(current, maxDistance));
                    sc.SplitterDistance = newDistance;
                }
            }
            catch
            {
                // Silently ignore any splitter errors
            }
        }

        private void LoadDefaultServers()
        {
            try
            {
                var defaultServers = new Server[]
                {
                    new Server { Name = "Twilly", PlayerCount = 0, IsOnline = true, Port = 443, IsMemberOnly = false, Ip = "game.aq.com" },
                    new Server { Name = "Artix", PlayerCount = 0, IsOnline = true, Port = 443, IsMemberOnly = false, Ip = "game.aq.com" },
                    new Server { Name = "Gravelyn", PlayerCount = 0, IsOnline = true, Port = 443, IsMemberOnly = false, Ip = "game.aq.com" },
                    new Server { Name = "Sir Ver", PlayerCount = 0, IsOnline = true, Port = 443, IsMemberOnly = true, Ip = "game.aq.com" },
                    new Server { Name = "Galanoth", PlayerCount = 0, IsOnline = true, Port = 443, IsMemberOnly = false, Ip = "game.aq.com" },
                    new Server { Name = "Yorumi", PlayerCount = 0, IsOnline = true, Port = 443, IsMemberOnly = false, Ip = "game.aq.com" },
                    new Server { Name = "Espada", PlayerCount = 0, IsOnline = true, Port = 443, IsMemberOnly = false, Ip = "game.aq.com" },
                    new Server { Name = "Twig", PlayerCount = 0, IsOnline = true, Port = 443, IsMemberOnly = false, Ip = "game.aq.com" },
                    new Server { Name = "Sepulchure", PlayerCount = 0, IsOnline = true, Port = 443, IsMemberOnly = false, Ip = "game.aq.com" },
                    new Server { Name = "Safiria", PlayerCount = 0, IsOnline = true, Port = 443, IsMemberOnly = false, Ip = "game.aq.com" },
                    new Server { Name = "Swordhaven (EU)", PlayerCount = 0, IsOnline = true, Port = 443, IsMemberOnly = false, Ip = "game.aq.com" },
                    new Server { Name = "Alteon", PlayerCount = 0, IsOnline = true, Port = 443, IsMemberOnly = false, Ip = "game.aq.com" },
                    new Server { Name = "Yokai (SEA)", PlayerCount = 0, IsOnline = true, Port = 443, IsMemberOnly = false, Ip = "game.aq.com" }
                };
                
                // Don't cache player counts - fetch fresh from API each time

                OnServersLoaded(defaultServers);
            }
            catch { }
        }

        private void LoadAccounts()
        {
            // Load UI settings from ClientConfig.cfg
            _config = Config.Load(Application.StartupPath + "\\ClientConfig.cfg");
            
            // Load accounts from CharSelect.cfg
            _charSelectConfig = Config.Load(Application.StartupPath + "\\CharSelect.cfg");
            _accounts.Clear();
            flowAccounts.Controls.Clear();

            foreach (var kvp in _charSelectConfig.Contents.OrderBy(k => int.TryParse(k.Key, out int i) ? i : int.MaxValue))
            {
                var parts = kvp.Value.Split(',');
                if (parts.Length >= 2)
                {
                    var username = parts[0];
                    var password = parts[1];
                    var displayName = parts.Length > 2 ? parts[2] : username;
                    var useDisplayName = parts.Length > 3 ? bool.TryParse(parts[3], out var b) && b : false;
                    _accounts.Add((username, password, displayName, useDisplayName));
                }
            }
            RenderAccounts();
            UpdateSelectedCount();
        }

        private void RenderAccounts()
        {
            flowAccounts.Controls.Clear();
            // Load reveal state from config
            _allRevealed = _config.Contents.ContainsKey("_allRevealed") && 
                          bool.TryParse(_config.Contents["_allRevealed"], out var savedRevealed) && 
                          savedRevealed;
            
            // Load Add Account expanded state from config
            _addAccountExpanded = !(_config.Contents.ContainsKey("_addAccountExpanded") && 
                                  bool.TryParse(_config.Contents["_addAccountExpanded"], out var savedAddExpanded) && 
                                  !savedAddExpanded);
            
            // Apply saved Add Account state
            if (_addPanelContainer != null)
            {
                _addPanelContainer.Height = _addAccountExpanded ? 108 : 32;
            }
            
            // Update icon based on loaded state
            if (_pbRevealAll != null && _showImage != null && _hideImage != null)
            {
                _pbRevealAll.Image = _allRevealed ? _hideImage : _showImage;  // Hide icon when revealed, Show icon when hidden
            }
            
            for (int i = 0; i < _accounts.Count; i++)
            {
                var acc = _accounts[i];
                var item = CreateAccountListItem(acc.Username, acc.DisplayName, acc.UseDisplayName, i);
                // Apply saved reveal state
                if (_allRevealed)
                {
                    var label = item.Controls.OfType<DarkLabel>().FirstOrDefault();
                    if (label != null)
                    {
                        label.Text = acc.Username;
                    }
                }
                flowAccounts.Controls.Add(item);
            }
            UpdateCardSizes();
        }

        private void SaveAccounts()
        {
            try
            {
                var newDict = new Dictionary<string, string>();
                for (int i = 0; i < _accounts.Count; i++)
                {
                    var acc = _accounts[i];
                    newDict[i.ToString()] = acc.Username + "," + acc.Password + "," + acc.DisplayName + "," + acc.UseDisplayName;
                }
                _config.Contents = newDict;
                _config.Save();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Save accounts error: {ex}");
            }
        }

        private Control CreateAccountListItem(string username, string displayName, bool useDisplayName, int index)
        {
            var item = new DarkPanel
            {
                Height = 32,
                BackColor = System.Drawing.Color.FromArgb(50, 50, 62),
                Tag = (username, displayName),
                Margin = new Padding(2, 2, 2, 2),
                Cursor = System.Windows.Forms.Cursors.Hand,
                BorderStyle = BorderStyle.None,
                Width = 200
            };

            // Display text (username or display name depending on toggle)
            var displayText = useDisplayName && !string.IsNullOrEmpty(displayName) ? displayName : username;

            var lbl = new DarkLabel
            {
                Left = 12,
                Top = 0,
                Width = 90,
                Height = 28,
                Text = displayText,
                ForeColor = System.Drawing.Color.FromArgb(200, 200, 200),
                AutoSize = false,
                Font = new System.Drawing.Font("Segoe UI", 9f),
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft
            };

            item.Controls.Add(lbl);

            item.Click += (s, e) => ToggleCardSelection(item, index);
            lbl.Click += (s, e) => ToggleCardSelection(item, index);

            var cms = new ContextMenuStrip();
            var removeMenu = new ToolStripMenuItem("Remove");
            removeMenu.Click += (s, e) =>
            {
                if (!_selected.Contains(index))
                {
                    _selected.Clear();
                    _selected.Add(index);
                    UpdateSelectedCount();
                }
                BtnRemoveSelected_Click(this, EventArgs.Empty);
            };
            cms.Items.Add(removeMenu);
            item.ContextMenuStrip = cms;

            item.MouseEnter += (s, e) =>
            {
                if (!_selected.Contains(index))
                    item.BackColor = System.Drawing.Color.FromArgb(55, 55, 70);
            };
            item.MouseLeave += (s, e) =>
            {
                if (!_selected.Contains(index))
                    item.BackColor = System.Drawing.Color.FromArgb(50, 50, 62);
            };

            return item;
        }

        private void UpdateAccountItemWidths()
        {
            try
            {
                if (flowAccounts == null || flowAccounts.IsDisposed || flowAccounts.ClientSize.Width <= 0)
                    return;
                    
                int targetWidth = Math.Max(100, flowAccounts.ClientSize.Width - 4);
                
                foreach (Control c in flowAccounts.Controls)
                {
                    if (c is DarkPanel p)
                    {
                        p.Width = targetWidth;
                        foreach (Control ch in p.Controls)
                        {
                            if (ch is DarkLabel lbl && !lbl.Dock.HasFlag(DockStyle.Fill))
                            {
                                lbl.Width = Math.Max(32, p.Width - 24);
                            }
                        }
                    }
                }
            }
            catch { }
        }


        private async Task<bool> TryFetchServersAsync()
        {
            const int maxRetries = 3;
            int retryCount = 0;
            int delayMs = 1000; // Start with 1 second delay
            
            while (retryCount < maxRetries)
            {
                try
                {
                    var url = "http://content.aq.com/game/api/data/servers";
                    Console.WriteLine($"[SERVER FETCH] Starting fetch from: {url} (Attempt {retryCount + 1}/{maxRetries})");
                    
                    var resp = await _httpClient.GetAsync(url);
                    Console.WriteLine($"[SERVER FETCH] Response status: {resp.StatusCode}");
                    
                    if (!resp.IsSuccessStatusCode)
                    {
                        Console.WriteLine($"[SERVER FETCH] Failed with status: {resp.StatusCode}");
                        retryCount++;
                        
                        if (retryCount < maxRetries)
                        {
                            Console.WriteLine($"[SERVER FETCH] Retrying in {delayMs}ms...");
                            await Task.Delay(delayMs);
                            delayMs *= 2; // Exponential backoff
                        }
                        continue;
                    }

                    var txt = await resp.Content.ReadAsStringAsync();
                    Console.WriteLine($"[SERVER FETCH] Response length: {txt?.Length ?? 0} characters");
                    
                    if (string.IsNullOrWhiteSpace(txt))
                    {
                        Console.WriteLine("[SERVER FETCH] Response was empty or whitespace");
                        retryCount++;
                        
                        if (retryCount < maxRetries)
                        {
                            Console.WriteLine($"[SERVER FETCH] Retrying in {delayMs}ms...");
                            await Task.Delay(delayMs);
                            delayMs *= 2;
                        }
                        continue;
                    }

                    // Show first 300 chars of response
                    Console.WriteLine($"[SERVER FETCH] Response preview: {txt.Substring(0, Math.Min(300, txt.Length))}");

                    try
                    {
                        var apiServers = JsonConvert.DeserializeObject<List<ServerApiResponse>>(txt);
                        Console.WriteLine($"[SERVER FETCH] Parsed {apiServers?.Count ?? 0} servers");
                        
                        if (apiServers != null && apiServers.Count > 0)
                        {
                            // Log first server details
                            var first = apiServers[0];
                            Console.WriteLine($"[SERVER FETCH] First server - Name: {first.Name}, Players: {first.PlayerCount}, Online: {first.IsOnline}");
                            
                            // Convert to Server objects
                            var servers = apiServers.Select(s => new Server
                            {
                                Name = s.Name,
                                PlayerCount = s.PlayerCount,
                                IsOnline = s.IsOnline,
                                Ip = string.IsNullOrWhiteSpace(s.Ip) ? "game.aq.com" : s.Ip,
                                Port = s.Port > 0 ? s.Port : 443,
                                IsMemberOnly = s.IsMemberOnly,
                                IsChatRestricted = s.ChatLevel == 0,
                                Language = s.Language
                            }).ToArray();

                            Console.WriteLine($"[SERVER FETCH] Converted {servers.Length} servers");
                            Console.WriteLine($"[SERVER FETCH] First converted server - Name: {servers[0].Name}, Players: {servers[0].PlayerCount}");
                            
                            OnServersLoaded(servers);
                            UpdateLastRefreshTime();
                            Console.WriteLine("[SERVER FETCH] Successfully loaded servers");
                            return true;
                        }
                        else
                        {
                            Console.WriteLine("[SERVER FETCH] API servers list was null or empty");
                            retryCount++;
                            
                            if (retryCount < maxRetries)
                            {
                                Console.WriteLine($"[SERVER FETCH] Retrying in {delayMs}ms...");
                                await Task.Delay(delayMs);
                                delayMs *= 2;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[SERVER FETCH] Parse error: {ex.Message}");
                        Console.WriteLine($"[SERVER FETCH] Stack trace: {ex.StackTrace}");
                        retryCount++;
                        
                        if (retryCount < maxRetries)
                        {
                            Console.WriteLine($"[SERVER FETCH] Retrying in {delayMs}ms...");
                            await Task.Delay(delayMs);
                            delayMs *= 2;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SERVER FETCH] Fetch error: {ex.Message}");
                    Console.WriteLine($"[SERVER FETCH] Stack trace: {ex.StackTrace}");
                    retryCount++;
                    
                    if (retryCount < maxRetries)
                    {
                        Console.WriteLine($"[SERVER FETCH] Retrying in {delayMs}ms...");
                        await Task.Delay(delayMs);
                        delayMs *= 2;
                    }
                }
            }

            Console.WriteLine("[SERVER FETCH] All retry attempts exhausted, returning false");
            return false;
        }

        private void UpdateLastRefreshTime()
        {
            try
            {
                if (lblLastRefresh != null && !lblLastRefresh.IsDisposed)
                {
                    var now = DateTime.Now;
                    lblLastRefresh.Text = $"Last refresh: {now:HH:mm:ss}";
                }
            }
            catch { }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try
                {
                    if (serverRefreshTimer != null)
                    {
                        serverRefreshTimer.Stop();
                        serverRefreshTimer.Dispose();
                    }
                    if (_toolTip != null)
                    {
                        _toolTip.Dispose();
                    }
                }
                catch { }
            }
            base.Dispose(disposing);
        }

        private Control CreateAccountCard(string username, int index)
        {
            var cardBg = System.Drawing.Color.FromArgb(50, 50, 62);
            var cardHover = System.Drawing.Color.FromArgb(55, 55, 70);
            var cardSelect = System.Drawing.Color.FromArgb(70, 100, 140);
            
            var card = new DarkPanel
            {
                Width = 240,
                Height = 100,
                BackColor = cardBg,
                Tag = index,
                Margin = new Padding(6),
                Cursor = System.Windows.Forms.Cursors.Hand,
                BorderStyle = BorderStyle.None
            };

            var lbl = new DarkLabel
            {
                Left = 12,
                Top = 12,
                Width = card.Width - 24,
                Height = 24,
                Text = username,
                ForeColor = System.Drawing.Color.FromArgb(230, 230, 240),
                AutoSize = false,
                Font = new System.Drawing.Font("Segoe UI", 11f, System.Drawing.FontStyle.Bold)
            };

            var lblStatus = new DarkLabel
            {
                Left = 12,
                Top = 42,
                Width = card.Width - 24,
                Height = 40,
                Text = "Status: Idle",
                ForeColor = System.Drawing.Color.FromArgb(130, 180, 130),
                AutoSize = false,
                Font = new System.Drawing.Font("Segoe UI", 9f),
                TextAlign = System.Drawing.ContentAlignment.TopLeft
            };

            card.Controls.Add(lbl);
            card.Controls.Add(lblStatus);

            card.Click += (s, e) => ToggleCardSelection(card, index);
            lbl.Click += (s, e) => ToggleCardSelection(card, index);
            lblStatus.Click += (s, e) => ToggleCardSelection(card, index);

            card.MouseEnter += (s, e) =>
            {
                if (!_selected.Contains(index))
                    card.BackColor = cardHover;
            };
            card.MouseLeave += (s, e) =>
            {
                if (!_selected.Contains(index))
                    card.BackColor = cardBg;
            };

            return card;
        }

        public void OnServersLoaded(Server[] servers)
        {
            Console.WriteLine($"[ON SERVERS LOADED] Called with {servers?.Length ?? 0} servers");
            
            if (InvokeRequired)
            {
                Console.WriteLine("[ON SERVERS LOADED] InvokeRequired - marshalling to UI thread");
                Invoke((Action)(() => OnServersLoaded(servers)));
                return;
            }

            if (servers == null || servers.Length == 0)
            {
                Console.WriteLine("[ON SERVERS LOADED] Servers null or empty, returning");
                return;
            }

            try
            {
                Console.WriteLine($"[ON SERVERS LOADED] Clearing {cbServers.Items.Count} combo items and {flowServers.Controls.Count} flow items");
                
                cbServers.Items.Clear();
                flowServers.Controls.Clear();

                cbServers.Items.AddRange(servers);
                Console.WriteLine($"[ON SERVERS LOADED] Added {cbServers.Items.Count} items to combo");

                foreach (var s in servers)
                {
                    Console.WriteLine($"[ON SERVERS LOADED] Creating item for: {s.Name} - {s.PlayerCount} players");
                    var item = CreateServerItem(s);
                    flowServers.Controls.Add(item);
                }
                
                flowServers.PerformLayout();
                flowServers.Refresh();
                
                // Delay UpdateServerSizes to allow layout engine to calculate proper panel size
                BeginInvoke((Action)(() => UpdateServerSizes()));
                
                Console.WriteLine($"[ON SERVERS LOADED] Flow panel now has {flowServers.Controls.Count} controls");

                if (cbServers.SelectedIndex < 0 && cbServers.Items.Count > 0)
                {
                    Console.WriteLine("[ON SERVERS LOADED] Setting selected index to 0");
                    cbServers.SelectedIndex = 0;
                }

                if (flowServers.Controls.Count > 0 && cbServers.SelectedItem is Server sel)
                {
                    foreach (Control c in flowServers.Controls)
                        c.BackColor = (c.Tag as Server) == sel ? System.Drawing.Color.FromArgb(60, 80, 120) : System.Drawing.Color.FromArgb(50, 50, 62);
                }
                
                Console.WriteLine("[ON SERVERS LOADED] Completed successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ON SERVERS LOADED] Error: {ex.Message}");
                Console.WriteLine($"[ON SERVERS LOADED] Stack: {ex.StackTrace}");
            }
        }

        private Control CreateServerItem(Server s)
        {
            Console.WriteLine($"[CREATE SERVER ITEM] Creating for {s.Name} - {s.PlayerCount} players");
            
            var panel = new DarkPanel
            {
                Width = 190,
                Height = 56,
                BackColor = System.Drawing.Color.FromArgb(50, 50, 62),
                Margin = new Padding(5, 5, 5, 5),
                Cursor = Cursors.Hand,
                Tag = s,
                BorderStyle = BorderStyle.None,
                AutoSize = false
            };

            var statusDot = new DarkPanel
            {
                Left = 6,
                Top = 18,
                Width = 8,
                Height = 8,
                BackColor = s.IsOnline ? System.Drawing.Color.FromArgb(80, 200, 120) : System.Drawing.Color.FromArgb(120, 120, 120),
                Margin = new Padding(0)
            };

            var lblName = new DarkLabel
            {
                Left = 24,
                Top = 6,
                Width = 100,
                Height = 18,
                Text = s.Name,
                ForeColor = System.Drawing.Color.FromArgb(220, 220, 220),
                Font = new System.Drawing.Font("Segoe UI", 9f, System.Drawing.FontStyle.Bold),
                AutoSize = false,
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft
            };

            var lblCount = new DarkLabel
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Left = 130,
                Top = 6,
                Width = 50,
                Height = 18,
                Text = s.PlayerCount >= 0 ? s.PlayerCount.ToString() : "-",
                ForeColor = System.Drawing.Color.FromArgb(160, 180, 200),
                Font = new System.Drawing.Font("Segoe UI", 9f, System.Drawing.FontStyle.Bold),
                AutoSize = false,
                TextAlign = System.Drawing.ContentAlignment.MiddleRight
            };

            // Determine server type text based on properties
            string serverType = "";
            if (s.IsMemberOnly)
                serverType = "Member";
            else if (s.IsChatRestricted)
                serverType = "Canned-Chat";
            else if (!string.IsNullOrWhiteSpace(s.Language))
            {
                if (s.Language.Equals("en", StringComparison.OrdinalIgnoreCase))
                    serverType = "English";
                else if (s.Language.Equals("pt", StringComparison.OrdinalIgnoreCase))
                    serverType = "Portuguese";
                else
                    serverType = s.Language.ToUpper();
            }
            else
                serverType = "Global";

            var lblType = new DarkLabel
            {
                Left = 24,
                Top = 28,
                Width = 130,
                Height = 20,
                Text = serverType,
                ForeColor = System.Drawing.Color.FromArgb(140, 150, 160),
                Font = new System.Drawing.Font("Segoe UI", 7.5f),
                AutoSize = false,
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft
            };
            
            Console.WriteLine($"[CREATE SERVER ITEM] Label text set to: '{lblCount.Text}'");

            panel.Controls.Add(lblName);
            panel.Controls.Add(lblType);
            panel.Controls.Add(statusDot);
            panel.Controls.Add(lblCount);

            try
            {
                var tip = $"{s.Name} ({serverType})\nIP: {s.Ip}:{s.Port}\nPlayers: {s.PlayerCount}\nOnline: {(s.IsOnline ? "Yes" : "No")}";
                _toolTip.SetToolTip(panel, tip);
                _toolTip.SetToolTip(lblName, tip);
                _toolTip.SetToolTip(lblType, tip);
                _toolTip.SetToolTip(lblCount, tip);
                _toolTip.SetToolTip(statusDot, tip);
            }
            catch { }

            panel.Click += (se, ev) =>
            {
                cbServers.SelectedItem = s;
            };

            lblName.Click += (se, ev) => { cbServers.SelectedItem = s; flowServers.ScrollControlIntoView(panel); };
            lblType.Click += (se, ev) => { cbServers.SelectedItem = s; flowServers.ScrollControlIntoView(panel); };
            lblCount.Click += (se, ev) => { cbServers.SelectedItem = s; flowServers.ScrollControlIntoView(panel); };

            panel.MouseEnter += (se, ev) => { if (cbServers.SelectedItem as Server != s) panel.BackColor = System.Drawing.Color.FromArgb(55, 55, 70); };
            panel.MouseLeave += (se, ev) => { if (cbServers.SelectedItem as Server != s) panel.BackColor = System.Drawing.Color.FromArgb(50, 50, 62); };

            return panel;
        }

        private void ToggleCardSelection(Control card, int index)
        {
            var cardBg = System.Drawing.Color.FromArgb(50, 50, 62);
            var cardSelect = System.Drawing.Color.FromArgb(70, 100, 140);
            
            if (_selected.Contains(index))
            {
                _selected.Remove(index);
                card.BackColor = cardBg;
            }
            else
            {
                _selected.Add(index);
                card.BackColor = cardSelect;
            }
            UpdateSelectedCount();
        }

        private void UpdateCardSizes()
        {
            try
            {
                if (flowAccounts == null || flowAccounts.IsDisposed || flowAccounts.Controls.Count == 0)
                    return;
                
                int cols = (int)nudColumns.Value;
                int available = flowAccounts.ClientSize.Width;
                
                if (available <= 0)
                    return;
                
                // Each card has 4px total margin (2px left + 2px right)
                // Add extra padding for scrollbar and panel spacing
                int totalMarginSpace = (cols * 4) + 20; // Extra buffer for safety
                
                // Calculate ideal width for the number of columns
                int idealWidth = (available - totalMarginSpace) / cols;
                
                // If window is too small for multiple columns, reduce to single column
                int actualCols = cols;
                if (idealWidth < 150)
                {
                    actualCols = Math.Max(1, available / 154); // 150 + 4 margin
                    totalMarginSpace = (actualCols * 4) + 20;
                    idealWidth = (available - totalMarginSpace) / actualCols;
                }
                
                // Minimum of 100px for very small windows, but allow wrapping
                int cardWidth = Math.Max(100, idealWidth);
                
                // If even 100px doesn't fit, use full available width minus margins
                if (cardWidth > available - 24)
                {
                    cardWidth = Math.Max(80, available - 24);
                }
                
                foreach (Control c in flowAccounts.Controls)
                {
                    c.Width = cardWidth;
                    foreach (Control child in c.Controls)
                    {
                        if (child is DarkLabel)
                        {
                            child.Width = Math.Max(32, cardWidth - 24);
                        }
                    }
                }
            }
            catch { }
        }

        private void UpdateServerSizes()
        {
            try
            {
                if (flowServers == null || flowServers.IsDisposed || flowServers.Controls.Count == 0)
                    return;
                
                int available = flowServers.ClientSize.Width;
                
                if (available <= 20)
                    return;
                
                int minCardWidth = 190;
                int cardMargin = 10;  // 5px per side
                int usableWidth = available - 20; // Leave 20px for scrollbar
                
                // Calculate how many cards fit with minimum width
                int cardsPerRow = Math.Max(1, usableWidth / (minCardWidth + cardMargin));
                
                // Expand cards to fill available width evenly
                int expandedWidth = (usableWidth - (cardMargin * cardsPerRow)) / cardsPerRow;
                
                // Suspend layout during width changes
                flowServers.SuspendLayout();
                
                foreach (Control c in flowServers.Controls)
                {
                    c.Width = expandedWidth;
                }
                
                flowServers.ResumeLayout(true);
            }
            catch { }
        }

        private void UpdateSelectedCount()
        {
            var text = $"Selected: {_selected.Count}";
            if (lblSelectedTop != null)
                lblSelectedTop.Text = text;
        }

        private async void BtnStartSelected_Click(object sender, EventArgs e)
        {
            if (_selected.Count == 0)
            {
                MessageBox.Show("Please select at least one account.");
                return;
            }

            try
            {
                if (cbServers == null || cbServers.IsDisposed)
                {
                    MessageBox.Show("Server selector not available.");
                    return;
                }

                var server = cbServers.SelectedItem as Server;
                if (server == null)
                {
                    MessageBox.Show("Please select a server first.");
                    return;
                }

                var selected = _selected.ToList();
                int accountIndex = 0;
                
                foreach (int idx in selected)
                {
                    if (idx >= _accounts.Count)
                        continue;

                    var acc = _accounts[idx];
                    var username = acc.Username;
                    var password = acc.Password;

                    // First account logs into current instance
                    if (accountIndex == 0)
                    {
                        Debug.WriteLine($"Logging first account into current instance: {username}");
                        
                        // Set credentials in OptionsManager first
                        OptionsManager.LoginUsername = username;
                        OptionsManager.LoginPassword = password;
                        
                        // Delay login to let UI message loop process first
                        Task.Delay(500).ContinueWith(_ =>
                        {
                            AutoRelogin.Login(server, 15000, new System.Threading.CancellationTokenSource(), ensureSuccess: false);
                            Debug.WriteLine($"Login request sent for first account: {username}");
                        }, TaskScheduler.Default);
                        
                        // Load script in background once logged in (non-blocking)
                        if (cbStartWithScript.Checked && !string.IsNullOrEmpty(tbScriptPath.Text) && File.Exists(tbScriptPath.Text))
                        {
                            var scriptPath = tbScriptPath.Text;
                            Task.Run(async () =>
                            {
                                try
                                {
                                    // Poll for login completion (in background)
                                    for (int i = 0; i < 60 && !Player.IsLoggedIn; i++)
                                    {
                                        await Task.Delay(500);
                                    }
                                    
                                    if (Player.IsLoggedIn)
                                    {
                                        // Wait for character to be fully loaded (Cell not empty and Health > 0)
                                        Debug.WriteLine($"First account: waiting for character load complete...");
                                        for (int i = 0; i < 40 && (string.IsNullOrEmpty(Player.Cell) || Player.Health <= 0); i++)
                                        {
                                            await Task.Delay(500);
                                        }
                                        
                                        // Extra delay to ensure inventory and skills are loaded
                                        await Task.Delay(2000);
                                        
                                        Debug.WriteLine($"First account: character loaded, loading script");
                                        Configuration cfg = JsonConvert.DeserializeObject<Configuration>(File.ReadAllText(scriptPath), new JsonSerializerSettings
                                        {
                                            TypeNameHandling = TypeNameHandling.All
                                        });
                                        
                                        Root.Instance.Invoke((MethodInvoker)delegate
                                        {
                                            Root.Instance.ShowForm(BotManager.Instance);
                                            BotManager.Instance.ApplyConfiguration(cfg);
                                            
                                            // Add delay before starting bot to allow configuration to settle
                                            Task.Delay(1000).ContinueWith(_ =>
                                            {
                                                Root.Instance.Invoke((MethodInvoker)delegate
                                                {
                                                    if (Root.Instance != null && !Root.Instance.IsDisposed)
                                                        Root.Instance.chkStartBot.Checked = true;
                                                });
                                            });
                                        });
                                        
                                        Debug.WriteLine($"Script loaded and queued to start for first account");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine($"Error loading script: {ex.Message}");
                                }
                            });
                        }
                    }
                    else
                    {
                        // Launch additional accounts in new instances (no delay between launches)
                        try
                        {
                            var exePath = Application.ExecutablePath;
                            var startInfo = new System.Diagnostics.ProcessStartInfo(exePath);
                            startInfo.Arguments = $"--username=\"{username}\" --password=\"{password}\" --server=\"{server.Name}\"";
                            
                            if (cbStartWithScript.Checked && !string.IsNullOrEmpty(tbScriptPath.Text) && File.Exists(tbScriptPath.Text))
                            {
                                startInfo.Arguments += $" --script=\"{tbScriptPath.Text}\"";
                            }
                            
                            startInfo.UseShellExecute = false;
                            var process = System.Diagnostics.Process.Start(startInfo);
                            
                            if (process != null)
                            {
                                Debug.WriteLine($"Launched instance (PID: {process.Id}) for account: {username}");
                            }
                            else
                            {
                                Debug.WriteLine($"Failed to launch instance for {username} - Process.Start returned null");
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Failed to launch instance for {username}: {ex.Message}");
                            MessageBox.Show($"Failed to launch instance for {username}: {ex.Message}");
                        }
                    }
                    
                    accountIndex++;
                }
                
                Debug.WriteLine($"Started {accountIndex} account(s)");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Start selected error: {ex}");
                MessageBox.Show("Failed to start selected account(s): " + ex.Message);
            }
        }

        private async void BtnStartAll_Click(object sender, EventArgs e)
        {
            try
            {
                if (cbServers == null || cbServers.IsDisposed)
                {
                    MessageBox.Show("Server selector not available.");
                    return;
                }

                var server = cbServers.SelectedItem as Server;
                if (server == null)
                {
                    MessageBox.Show("Please select a server first.");
                    return;
                }

                if (_accounts.Count == 0)
                {
                    MessageBox.Show("No accounts to start.");
                    return;
                }

                int accountIndex = 0;
                
                foreach (var acc in _accounts)
                {
                    var username = acc.Username;
                    var password = acc.Password;

                    // First account logs into current instance
                    if (accountIndex == 0)
                    {
                        Debug.WriteLine($"Logging first account into current instance: {username}");
                        
                        // Set credentials in OptionsManager first
                        OptionsManager.LoginUsername = username;
                        OptionsManager.LoginPassword = password;
                        
                        // Delay login to let UI message loop process first
                        Task.Delay(500).ContinueWith(_ =>
                        {
                            AutoRelogin.Login(server, 15000, new System.Threading.CancellationTokenSource(), ensureSuccess: false);
                            Debug.WriteLine($"Login request sent for first account: {username}");
                        }, TaskScheduler.Default);
                        
                        // Load script in background once logged in (non-blocking)
                        if (cbStartWithScript.Checked && !string.IsNullOrEmpty(tbScriptPath.Text) && File.Exists(tbScriptPath.Text))
                        {
                            var scriptPath = tbScriptPath.Text;
                            Task.Run(async () =>
                            {
                                try
                                {
                                    // Poll for login completion (in background)
                                    for (int i = 0; i < 60 && !Player.IsLoggedIn; i++)
                                    {
                                        await Task.Delay(500);
                                    }
                                    
                                    if (Player.IsLoggedIn)
                                    {
                                        // Wait for character to be fully loaded (Cell not empty and Health > 0)
                                        Debug.WriteLine($"First account: waiting for character load complete...");
                                        for (int i = 0; i < 40 && (string.IsNullOrEmpty(Player.Cell) || Player.Health <= 0); i++)
                                        {
                                            await Task.Delay(500);
                                        }
                                        
                                        // Extra delay to ensure inventory and skills are loaded
                                        await Task.Delay(2000);
                                        
                                        Debug.WriteLine($"First account: character loaded, loading script");
                                        Configuration cfg = JsonConvert.DeserializeObject<Configuration>(File.ReadAllText(scriptPath), new JsonSerializerSettings
                                        {
                                            TypeNameHandling = TypeNameHandling.All
                                        });
                                        
                                        Root.Instance.Invoke((MethodInvoker)delegate
                                        {
                                            Root.Instance.ShowForm(BotManager.Instance);
                                            BotManager.Instance.ApplyConfiguration(cfg);
                                            
                                            // Add delay before starting bot to allow configuration to settle
                                            Task.Delay(1000).ContinueWith(_ =>
                                            {
                                                Root.Instance.Invoke((MethodInvoker)delegate
                                                {
                                                    if (Root.Instance != null && !Root.Instance.IsDisposed)
                                                        Root.Instance.chkStartBot.Checked = true;
                                                });
                                            });
                                        });
                                        
                                        Debug.WriteLine($"Script loaded and queued to start for first account");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine($"Error loading script: {ex.Message}");
                                }
                            });
                        }
                    }
                    else
                    {
                        // Launch additional accounts in new instances (no delay between launches)
                        try
                        {
                            var exePath = Application.ExecutablePath;
                            var startInfo = new System.Diagnostics.ProcessStartInfo(exePath);
                            startInfo.Arguments = $"--username=\"{username}\" --password=\"{password}\" --server=\"{server.Name}\"";
                            
                            if (cbStartWithScript.Checked && !string.IsNullOrEmpty(tbScriptPath.Text) && File.Exists(tbScriptPath.Text))
                            {
                                startInfo.Arguments += $" --script=\"{tbScriptPath.Text}\"";
                            }
                            
                            startInfo.UseShellExecute = false;
                            var process = System.Diagnostics.Process.Start(startInfo);
                            
                            if (process != null)
                            {
                                Debug.WriteLine($"Launched instance (PID: {process.Id}) for account: {username}");
                            }
                            else
                            {
                                Debug.WriteLine($"Failed to launch instance for {username} - Process.Start returned null");
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Failed to launch instance for {username}: {ex.Message}");
                            MessageBox.Show($"Failed to launch instance for {username}: {ex.Message}");
                        }
                    }
                    
                    accountIndex++;
                }
                
                Debug.WriteLine($"Started {accountIndex} account(s)");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Start all error: {ex}");
                MessageBox.Show("Failed to start all accounts: " + ex.Message);
            }
        }

        private void BtnRemoveSelected_Click(object sender, EventArgs e)
        {
            if (_selected.Count == 0)
            {
                MessageBox.Show("Please select at least one account to remove.");
                return;
            }

            try
            {
                // Ensure charSelectConfig is loaded
                if (_charSelectConfig == null)
                {
                    _charSelectConfig = Config.Load(Application.StartupPath + "\\CharSelect.cfg");
                }

                var toRemove = _selected.OrderByDescending(i => i).ToList();
                foreach (int sel in toRemove)
                {
                    if (sel < _accounts.Count)
                        _accounts.RemoveAt(sel);
                }
                _selected.Clear();

                var newDict = new Dictionary<string, string>();
                for (int i = 0; i < _accounts.Count; i++)
                {
                    var acc = _accounts[i];
                    newDict[i.ToString()] = acc.Username + "," + acc.Password + "," + acc.DisplayName + "," + acc.UseDisplayName;
                }
                _charSelectConfig.Contents = newDict;
                _charSelectConfig.Save();

                LoadAccounts();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Remove error: {ex}");
                MessageBox.Show("Error removing accounts: " + ex.Message);
            }
        }

        private void BtnAddAccount_Click(object sender, EventArgs e)
        {
            try
            {
                // Ensure charSelectConfig is loaded
                if (_charSelectConfig == null)
                {
                    _charSelectConfig = Config.Load(Application.StartupPath + "\\CharSelect.cfg");
                }

                var user = tbNewUsername.Text?.Trim();
                var pass = tbNewPassword.Text?.Trim();
                var displayName = tbNewDisplayName.Text?.Trim();
                
                bool hasUsername = !string.IsNullOrEmpty(user) && user != "Username";
                bool hasPassword = !string.IsNullOrEmpty(pass) && pass != "Password";
                
                if (!hasUsername || !hasPassword)
                {
                    MessageBox.Show("Please enter both username and password.");
                    return;
                }

                // Display name defaults to username if empty
                if (string.IsNullOrEmpty(displayName) || displayName == "Display Name")
                    displayName = user;

                // Check if account already exists
                foreach (var kvp in _charSelectConfig.Contents)
                {
                    var parts = kvp.Value.Split(',');
                    if (parts.Length >= 1 && parts[0].Equals(user, StringComparison.OrdinalIgnoreCase))
                    {
                        MessageBox.Show("An account with this username already exists.");
                        return;
                    }
                }

                int nextKey = 0;
                while (_charSelectConfig.Contents.ContainsKey(nextKey.ToString()))
                    nextKey++;
                bool useDisplayName = !string.IsNullOrEmpty(displayName) && displayName != user;
                _charSelectConfig.Contents[nextKey.ToString()] = user + "," + pass + "," + displayName + "," + useDisplayName;
                _charSelectConfig.Save();

                tbNewUsername.Text = "Username";
                tbNewUsername.ForeColor = System.Drawing.Color.FromArgb(150, 150, 150);
                tbNewPassword.Text = "Password";
                tbNewPassword.ForeColor = System.Drawing.Color.FromArgb(150, 150, 150);
                tbNewPassword.UseSystemPasswordChar = false;
                tbNewDisplayName.Text = "Display Name";
                tbNewDisplayName.ForeColor = System.Drawing.Color.FromArgb(150, 150, 150);
                
                MessageBox.Show("Account added successfully!");
                LoadAccounts();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Add account error: {ex}");
                MessageBox.Show("Error adding account: " + ex.Message);
            }
        }

        private void BtnBrowseScript_Click(object sender, EventArgs e)
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Filter = "Script files (*.grm, *.txt, *.gbot)|*.grm;*.txt;*.gbot|All files (*.*)|*.*";
                dlg.InitialDirectory = string.IsNullOrEmpty(_scriptBaseDir) ? Application.StartupPath : _scriptBaseDir;
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    tbScriptPath.Text = dlg.FileName;
                    // Update base directory from file's parent directory
                    _scriptBaseDir = Path.GetDirectoryName(dlg.FileName);
                    RefreshScriptTree();
                }
            }
        }

        private void BtnSetScriptDir_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog folderDialog = new FolderBrowserDialog())
            {
                folderDialog.Description = "Choose your scripts directory";
                folderDialog.ShowNewFolderButton = false;
                folderDialog.SelectedPath = string.IsNullOrEmpty(_scriptBaseDir) ? Application.StartupPath : _scriptBaseDir;
                
                if (folderDialog.ShowDialog() == DialogResult.OK)
                {
                    _scriptBaseDir = folderDialog.SelectedPath;
                    tbScriptPath.Text = folderDialog.SelectedPath;
                    ClientConfig.SetValue(ClientConfig.C_SCRIPT_DIR, _scriptBaseDir);
                    RefreshScriptTree();
                }
            }
        }

        private void TreeScripts_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (string.IsNullOrEmpty(_scriptBaseDir) || !Directory.Exists(_scriptBaseDir))
                return;

            string path = Path.Combine(_scriptBaseDir, e.Node.FullPath);
            if (File.Exists(path))
            {
                tbScriptPath.Text = path;
            }
        }

        private void TreeScripts_AfterExpand(object sender, TreeViewEventArgs e)
        {
            if (string.IsNullOrEmpty(_scriptBaseDir) || !Directory.Exists(_scriptBaseDir))
                return;

            string path = Path.Combine(_scriptBaseDir, e.Node.FullPath);
            if (Directory.Exists(path))
            {
                // Remove "Loading..." placeholder first if it exists
                if (e.Node.Nodes.Count > 0 && e.Node.Nodes[0].Text == "Loading...")
                {
                    e.Node.Nodes.RemoveAt(0);
                }
                
                // Now add the actual contents
                AddScriptTreeNodes(e.Node, path);
            }
        }

        private void RefreshScriptTree()
        {
            treeScripts.Nodes.Clear();
            if (!string.IsNullOrEmpty(_scriptBaseDir) && Directory.Exists(_scriptBaseDir))
            {
                AddScriptTreeNodes(treeScripts, _scriptBaseDir);
            }
        }

        private void LoadScriptDirectory()
        {
            try
            {
                string savedDir = ClientConfig.GetValue(ClientConfig.C_SCRIPT_DIR);
                if (!string.IsNullOrEmpty(savedDir) && Directory.Exists(savedDir))
                {
                    _scriptBaseDir = savedDir;
                    tbScriptPath.Text = savedDir;
                    RefreshScriptTree();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading script directory: {ex.Message}");
            }
        }

        private void AddScriptTreeNodes(TreeNode node, string path)
        {
            try
            {
                foreach (string dir in Directory.EnumerateDirectories(path, "*", SearchOption.TopDirectoryOnly))
                {
                    string folderName = Path.GetFileName(dir);
                    if (node.Nodes.Cast<TreeNode>().All(n => n.Text != folderName))
                    {
                        node.Nodes.Add(folderName).Nodes.Add("Loading...");
                    }
                }
                foreach (string file in Directory.EnumerateFiles(path, "*.*", SearchOption.TopDirectoryOnly)
                    .Where(f => f.EndsWith(".grm") || f.EndsWith(".txt") || f.EndsWith(".gbot")))
                {
                    string fileName = Path.GetFileName(file);
                    if (node.Nodes.Cast<TreeNode>().All(n => n.Text != fileName))
                    {
                        node.Nodes.Add(fileName);
                    }
                }
            }
            catch { }
        }

        private void AddScriptTreeNodes(TreeView tree, string path)
        {
            try
            {
                foreach (string dir in Directory.EnumerateDirectories(path, "*", SearchOption.TopDirectoryOnly))
                {
                    string folderName = Path.GetFileName(dir);
                    if (tree.Nodes.Cast<TreeNode>().All(n => n.Text != folderName))
                    {
                        tree.Nodes.Add(folderName).Nodes.Add("Loading...");
                    }
                }
                foreach (string file in Directory.EnumerateFiles(path, "*.*", SearchOption.TopDirectoryOnly)
                    .Where(f => f.EndsWith(".grm") || f.EndsWith(".txt") || f.EndsWith(".gbot")))
                {
                    string fileName = Path.GetFileName(file);
                    if (tree.Nodes.Cast<TreeNode>().All(n => n.Text != fileName))
                    {
                        tree.Nodes.Add(fileName);
                    }
                }
            }
            catch { }
        }

        private Tuple<string, string> PromptForCredentials()
        {
            using (var f = new Form())
            {
                f.Text = "Add Account";
                f.FormBorderStyle = FormBorderStyle.FixedDialog;
                f.StartPosition = FormStartPosition.CenterParent;
                f.ClientSize = new System.Drawing.Size(360, 120);
                f.MinimizeBox = false;
                f.MaximizeBox = false;

                var lblUser = new Label { Left = 12, Top = 12, Text = "Username", Width = 320 };
                var tbUser = new TextBox { Left = 12, Top = 30, Width = 320 };
                var lblPass = new Label { Left = 12, Top = 56, Text = "Password", Width = 320 };
                var tbPass = new TextBox { Left = 12, Top = 74, Width = 320, UseSystemPasswordChar = true };

                var btnOk = new Button { Text = "Add", Left = 180, Width = 70, Top = 96, DialogResult = DialogResult.OK };
                var btnCancel = new Button { Text = "Cancel", Left = 260, Width = 70, Top = 96, DialogResult = DialogResult.Cancel };
                f.Controls.AddRange(new Control[] { lblUser, tbUser, lblPass, tbPass, btnOk, btnCancel });
                f.AcceptButton = btnOk;

                if (f.ShowDialog(this) == DialogResult.OK)
                {
                    return Tuple.Create(tbUser.Text.Trim(), tbPass.Text.Trim());
                }
            }
            return null;
        }
    }
}