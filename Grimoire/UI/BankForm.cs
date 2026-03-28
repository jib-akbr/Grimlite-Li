using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Grimoire.Game;
using Grimoire;
using Grimoire.Game.Data;
using DarkUI.Forms;

namespace Grimoire.UI
{
    public partial class BankForm : DarkForm
    {
        public BankForm()
        {
            InitializeComponent();
        }

        private async void bankAll_ClickAsync(object sender, EventArgs e)
        {
            List<InventoryItem> inventory = Player.Inventory.Items;

            if (comboBox2.SelectedItem == null)
            {
                MessageBox.Show("Please select AC or Non-AC", "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            foreach (InventoryItem n in inventory)
            {
                if (n.IsAcItem && comboBox2.SelectedIndex == 0 && !n.IsEquipped)
                    Player.Bank.TransferToBank(n.Name);
                else if (Player.Bank.AvailableSlots > 0 && !n.IsAcItem && comboBox2.SelectedIndex == 1 && !n.IsEquipped)
                    Player.Bank.TransferToBank(n.Name);
                else continue;

                await Task.Delay(100);
            }
            return;
        }

        private async void button1_ClickAsync(object sender, EventArgs e)
        {
            List<InventoryItem> inventory = Player.Inventory.Items;
            string[] wep = new string[9]
            {
                "Sword",
                "Axe",
                "Dagger",
                "Gun",
                "Bow",
                "Mace",
                "Polearm",
                "Staff",
                "Wand",
            };
            object category = comboBox1.SelectedItem;
            object box2 = comboBox2.SelectedItem;
            bool isAC = false;
            if (category == null)
            {
                MessageBox.Show("Please select Item type", "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            else if (box2 == null)
            {
                MessageBox.Show("Please select AC or Non-AC", "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (comboBox2.SelectedIndex == 0)
            {
                isAC = true;
            }
            foreach (InventoryItem i in inventory)
            {
                bool flag = i.IsAcItem == isAC && !i.IsEquipped && i.Name.ToLower() != "treasure potion";

                if (cbAllExcept.Checked)
                {
                    if (category.ToString() == "Weapons" && !wep.Contains(i.Category) && flag)
                        Player.Bank.TransferToBank(i.Name);
                    else if (i.Category != category.ToString() && flag)
                        Player.Bank.TransferToBank(i.Name);
                    else continue;
                    await Task.Delay(70);
                }
                else
                {
                    if (category.ToString() == "Weapons" && wep.Contains(i.Category) && flag)
                        Player.Bank.TransferToBank(i.Name);
                    else if (i.Category == category.ToString() && flag)
                        Player.Bank.TransferToBank(i.Name);
                    else continue;
                    await Task.Delay(70);
                }
            }
            return;
        }

        private void BankForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
            }
        }
    }
}
