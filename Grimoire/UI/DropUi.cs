using Grimoire.Game;
using Grimoire.Game.Data;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Grimoire.UI
{
    public partial class DropUi : Form
    {
        public static DropUi instance { get; } = new DropUi();

        private readonly List<string> blacklisted = new List<string>();

        public DropUi()
        {
            InitializeComponent();
            InitListView();
        }
        #region ListView Related
        private void InitListView()
        {//Honestly OwnerDraw/Custom Draw section is very sensitive and causing a lot of Visual bugs

            listView.View = View.Details;
            listView.FullRowSelect = true;
            listView.GridLines = false;
            listView.MultiSelect = true;

            listView.HideSelection = false;

            listView.OwnerDraw = true;
            listView.HeaderStyle = ColumnHeaderStyle.Clickable;

            // Column init
            listView.Columns.Add("Name", 230);
            listView.Columns.Add("Qty", 30, HorizontalAlignment.Center);
            listView.Columns.Add("Category", 60, HorizontalAlignment.Center);
            listView.Columns.Add("AC | Member", 80, HorizontalAlignment.Center);
            #region Custom Draw
            // --- DRAW COLUMN HEADER ---
            listView.DrawColumnHeader += (s, e) =>
            {
                using (var brush = new SolidBrush(Color.FromArgb(35, 35, 45)))
                    e.Graphics.FillRectangle(brush, e.Bounds);

                TextRenderer.DrawText(e.Graphics, e.Header.Text, e.Font, e.Bounds, Color.White,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            };

            // --- DRAW ITEM ---
            /*listView.DrawItem += (s, e) =>
            {
                e.DrawDefault = false;
            }; Not needed currently*/

            // --- DRAW SUB ITEM (Column and row) ---
            listView.DrawSubItem += (s, e) =>
            {
                // PERBAIKAN KONSISTENSI:
                // Cek lagi e.Item.Selected agar sinkron dengan DrawItem
                bool isSelected = e.Item.Selected;

                Color backColor = isSelected ? Color.FromArgb(60, 120, 200) : Color.FromArgb(46, 46, 60);
                Color foreColor = isSelected ? Color.White : Color.Gainsboro;

                // 1. Gambar ulang background di SubItem (PENTING untuk menimpa sisa-sisa gambar lama)
                // Kita gunakan e.Bounds yang penuh agar tidak ada celah
                using (var b = new SolidBrush(backColor))
                    e.Graphics.FillRectangle(b, e.Bounds);

                // 2. Gambar Teks
                TextRenderer.DrawText(e.Graphics, e.SubItem.Text, e.SubItem.Font,
                    e.Bounds, foreColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

                // 3. Garis Pemisah (Opsional, bikin list terlihat rapi)
                // Garis vertikal
                using (var p = new Pen(Color.FromArgb(60, 60, 70)))
                    e.Graphics.DrawLine(p, e.Bounds.Right - 1, e.Bounds.Top, e.Bounds.Right - 1, e.Bounds.Bottom);

                // Garis horizontal bawah
                using (var p = new Pen(Color.FromArgb(60, 60, 70)))
                    e.Graphics.DrawLine(p, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
            };
            #endregion 
            // Sorting (Tidak Berubah)
            listView.ColumnClick += (s, e) =>
            {
                if (e.Column == lastColumn)
                    descending = !descending;  // klik kolom sama → toggle
                else
                    descending = false;        // kolom baru → start ascending

                lastColumn = e.Column;

                listView.ListViewItemSorter = new ListViewItemComparer(e.Column, descending);
                listView.Sort();

            };
            listView.Resize += (s, e) => AdjustColumnWidth();
            EnableDoubleBuffer(listView);
        }
        private int lastColumn = -1;
        private bool descending = false;
        private void AdjustColumnWidth()
        {
            if (listView.Columns.Count < 4)
                return;

            int fixedColumnsWidth = listView.Columns[1].Width +
                listView.Columns[2].Width +
                listView.Columns[3].Width;

            int availableWidth = listView.ClientRectangle.Width;

            int nameWidth = availableWidth - fixedColumnsWidth;

            if (nameWidth < 0) nameWidth = 0;

            listView.Columns[0].Width = nameWidth;
        }
        private void EnableDoubleBuffer(ListView lv)
        {
            lv.GetType().GetProperty("DoubleBuffered",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.SetValue(lv, true, null);
        }
        private List<ListViewItem> GetSelected()
        {
            return listView.SelectedItems.Cast<ListViewItem>().ToList();
        }
        #endregion

        public void RemoveItem(string itemName)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => RemoveItem(itemName)));
                return;
            }

            var item = listView.Items.Cast<ListViewItem>().FirstOrDefault(i => i.Text == itemName);
            if (item == null)
                return;
            listView.Items.Remove(item);
			UpdateDropCount();
        }

        public void AddItem(InventoryItem item)
        {
            if (blacklisted.Contains(item.Name))
                return;

            // check existing
            foreach (ListViewItem i in listView.Items)
            {
                if (i.Text.Equals(item.Name, StringComparison.OrdinalIgnoreCase))
                {
                    int qty = int.Parse(i.SubItems[1].Text);
                    i.SubItems[1].Text = (qty + item.Quantity).ToString();
                    i.ListView.Invalidate(i.Bounds);
                    UpdateDropCount();
                    return;
                }
            }

            // add new
            var lvi = new ListViewItem(item.Name);
            lvi.SubItems.Add(item.Quantity.ToString());
            lvi.SubItems.Add(item.Category);
            string acmember = (item.IsAcItem ? "AC" : "No") + " | " + (item.IsMemberOnly ? "Yes" : "Free");
            lvi.SubItems.Add(acmember);
            listView.Items.Add(lvi);

            UpdateDropCount();
            listView.Refresh();
        }

        private async void btnTakeDrop_Click(object sender, EventArgs e)
        {
            var selectedItems = GetSelected();
            // List<Task> removeTask = new List<Task>();
            foreach (var item in selectedItems)
            {
                await World.DropStack.GetDrop(item.Text);
                await Task.Delay(250);
            }
            // await Task.WhenAll(removeTask);
            UpdateDropCount();
        }

        private void btnRejectBlacklist_Click(object sender, EventArgs e)
        {
            foreach (var item in GetSelected())
            {
                blacklisted.Add(item.Text);
                listView.Items.Remove(item);
            }

            UpdateDropCount();
        }

        private void btnClear_Click(object sender, EventArgs e)
        {
            var result = MessageBox.Show(
                "This action will clear all Blacklist and Current drops within the UI.\n" +
                "Do you wish to proceed?",
                "Caution",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning
            );

            if (result == DialogResult.Yes)
            {
                clearDrop();
                blacklisted.Clear();
            }
        }
        public void clearDrop()
        {
            World.DropStack.Clear();
            listView.Items.Clear();
            UpdateDropCount();
        }

        private bool messageAlrShown = false;
        private void RejectIngameDrop_Checkedchanged(object sender, System.EventArgs e)
        {
            Player.isRejectingAllDrop = cbRejectDrop.Checked;
            if (messageAlrShown)
                return;

            MessageBox.Show("This option will not override Reject drop & whitelist system from bot manager",
                "Information",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            messageAlrShown = true;
        }

        private void UpdateDropCount()
        {
            lblCount.Text = $"Drop count : {listView.Items.Count}";
        }

        private void DropUi_FormClosing(object sender, FormClosingEventArgs e)
        {
            e.Cancel = true;
            Hide();
        }
    }

    // ⭐ Simple listview sorter
    public class ListViewItemComparer : System.Collections.IComparer
    {
        private readonly int col;
        private readonly bool descending;

        public ListViewItemComparer(int column, bool descending = false)
        {
            col = column;
            this.descending = descending;
        }

        public int Compare(object x, object y)
        {
            var a = ((ListViewItem)x).SubItems[col].Text;
            var b = ((ListViewItem)y).SubItems[col].Text;

            int result;

            // numeric sort
            if (int.TryParse(a, out int na) && int.TryParse(b, out int nb))
                result = na.CompareTo(nb);
            else
                result = string.Compare(a, b, StringComparison.OrdinalIgnoreCase);

            return descending ? -result : result;
        }
    }
}
