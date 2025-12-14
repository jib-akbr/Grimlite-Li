namespace Grimoire.UI
{
    partial class DropUi
    {
        private System.ComponentModel.IContainer components = null;

        private System.Windows.Forms.ListView listView;
        private System.Windows.Forms.Label lblCount;
        private System.Windows.Forms.CheckBox cbRejectDrop;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(DropUi));
            this.listView = new System.Windows.Forms.ListView();
            this.lblCount = new System.Windows.Forms.Label();
            this.cbRejectDrop = new System.Windows.Forms.CheckBox();
            this.btnTakedrop = new DarkUI.Controls.DarkButton();
            this.darkButton1 = new DarkUI.Controls.DarkButton();
            this.darkButton2 = new DarkUI.Controls.DarkButton();
            this.SuspendLayout();
            // 
            // listView
            // 
            this.listView.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.listView.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(46)))), ((int)(((byte)(46)))), ((int)(((byte)(60)))));
            this.listView.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.listView.ForeColor = System.Drawing.Color.White;
            this.listView.FullRowSelect = true;
            this.listView.GridLines = true;
            this.listView.HideSelection = false;
            this.listView.Location = new System.Drawing.Point(0, 0);
            this.listView.Name = "listView";
            this.listView.Size = new System.Drawing.Size(401, 233);
            this.listView.TabIndex = 0;
            this.listView.UseCompatibleStateImageBehavior = false;
            // 
            // lblCount
            // 
            this.lblCount.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.lblCount.ForeColor = System.Drawing.Color.White;
            this.lblCount.Location = new System.Drawing.Point(301, 268);
            this.lblCount.Name = "lblCount";
            this.lblCount.Size = new System.Drawing.Size(90, 20);
            this.lblCount.TabIndex = 4;
            this.lblCount.Text = "Drop count : 0";
            // 
            // cbRejectDrop
            // 
            this.cbRejectDrop.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.cbRejectDrop.AutoSize = true;
            this.cbRejectDrop.BackColor = System.Drawing.Color.Transparent;
            this.cbRejectDrop.ForeColor = System.Drawing.Color.White;
            this.cbRejectDrop.Location = new System.Drawing.Point(146, 245);
            this.cbRejectDrop.Name = "cbRejectDrop";
            this.cbRejectDrop.Size = new System.Drawing.Size(118, 17);
            this.cbRejectDrop.TabIndex = 4;
            this.cbRejectDrop.Text = "Reject ingame drop";
            this.cbRejectDrop.UseVisualStyleBackColor = false;
            this.cbRejectDrop.CheckedChanged += new System.EventHandler(this.RejectIngameDrop_Checkedchanged);
            // 
            // btnTakedrop
            // 
            this.btnTakedrop.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnTakedrop.Checked = false;
            this.btnTakedrop.Location = new System.Drawing.Point(304, 239);
            this.btnTakedrop.Name = "btnTakedrop";
            this.btnTakedrop.Size = new System.Drawing.Size(85, 23);
            this.btnTakedrop.TabIndex = 5;
            this.btnTakedrop.Text = "Take Drop";
            this.btnTakedrop.Click += new System.EventHandler(this.btnTakeDrop_Click);
            // 
            // darkButton1
            // 
            this.darkButton1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.darkButton1.Checked = false;
            this.darkButton1.Location = new System.Drawing.Point(12, 239);
            this.darkButton1.Name = "darkButton1";
            this.darkButton1.Size = new System.Drawing.Size(128, 23);
            this.darkButton1.TabIndex = 6;
            this.darkButton1.Text = "Reject & Blacklist";
            this.darkButton1.Click += new System.EventHandler(this.btnRejectBlacklist_Click);
            // 
            // darkButton2
            // 
            this.darkButton2.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.darkButton2.Checked = false;
            this.darkButton2.Location = new System.Drawing.Point(12, 268);
            this.darkButton2.Name = "darkButton2";
            this.darkButton2.Size = new System.Drawing.Size(128, 23);
            this.darkButton2.TabIndex = 7;
            this.darkButton2.Text = "Clear";
            this.darkButton2.Click += new System.EventHandler(this.btnClear_Click);
            // 
            // DropUi
            // 
            this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(23)))), ((int)(((byte)(23)))), ((int)(((byte)(33)))));
            this.ClientSize = new System.Drawing.Size(401, 300);
            this.Controls.Add(this.darkButton2);
            this.Controls.Add(this.darkButton1);
            this.Controls.Add(this.btnTakedrop);
            this.Controls.Add(this.listView);
            this.Controls.Add(this.lblCount);
            this.Controls.Add(this.cbRejectDrop);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "DropUi";
            this.Text = "DropList";
            this.TopMost = true;
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.DropUi_FormClosing);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        private DarkUI.Controls.DarkButton btnTakedrop;
        private DarkUI.Controls.DarkButton darkButton1;
        private DarkUI.Controls.DarkButton darkButton2;
    }
}
