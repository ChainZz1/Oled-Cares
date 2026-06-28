using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace OledCares
{
    public class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            bool startMinimized = false;
            foreach (string arg in args)
            {
                if (arg == "--startup" || arg == "/startup")
                {
                    startMinimized = true;
                }
            }

            Application.Run(new OledCaresContext(startMinimized));
        }
    }

    public class OledCaresContext : ApplicationContext
    {
        private MainWindow mainWin;

        public OledCaresContext(bool startMinimized)
        {
            mainWin = new MainWindow();
            
            // Exit the thread (and app) when the main window is closed by ExitApp
            mainWin.FormClosed += (sender, e) => {
                ExitThread();
            };

            if (!startMinimized)
            {
                mainWin.Show();
            }
        }
    }

    public class MainWindow : Form
    {
        // WIN32 APIs
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HT_CAPTION = 0x2;

        // Settings variables
        private int currentMode = 1; // 0: Physical Sleep, 1: OLED Black Screen
        private uint currentModifier = 3; // 3: Ctrl + Alt
        private uint currentKey = 90; // 90: Z (Ctrl+Alt+Z)
        private int currentDelay = 500; // 500ms

        // Path for local settings
        private static readonly string ConfigPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "OledCares",
            "config.txt"
        );

        // GUI Components
        private System.ComponentModel.IContainer components = null;
        private NotifyIcon trayIcon;
        private Panel pnlTitleBar;
        private Label lblAppTitle;
        private Button btnMin;
        private Button btnClose;
        private Panel pnlContent;
        
        private Panel pnlCardMode;
        private Label lblCardModeTitle;
        private RadioButton rbSleep;
        private RadioButton rbBlack;

        private Panel pnlCardHotkey;
        private Label lblCardHotkeyTitle;
        private Label lblModifier;
        private ComboBox comboModifier;
        private Label lblKey;
        private ComboBox comboKey;
        private Label lblHotkeyStatus;

        private Panel pnlCardOptions;
        private Label lblCardOptionsTitle;
        private CheckBox chkStartup;
        private Label lblDelay;
        private ComboBox comboDelay;

        private Button btnTest;
        private Button btnSave;
        private Label lblCredit;
        private Button btnExit;

        private bool reallyClose = false;

        // Custom Helper Classes for list binding (C# 5 compatible struct)
        private struct KeyItem
        {
            public string Name { get; set; }
            public uint Value { get; set; }
            public KeyItem(string name, uint value) : this()
            {
                Name = name;
                Value = value;
            }
            public override string ToString() { return Name; }
        }

        private struct DelayItem
        {
            public string Name { get; set; }
            public int Value { get; set; }
            public DelayItem(string name, int value) : this()
            {
                Name = name;
                Value = value;
            }
            public override string ToString() { return Name; }
        }

        private List<KeyItem> modifiersList = new List<KeyItem>()
        {
            new KeyItem("Ctrl + Alt", 3),
            new KeyItem("Ctrl + Shift", 6),
            new KeyItem("Alt + Shift", 5),
            new KeyItem("Ctrl + Alt + Shift", 7)
        };

        private List<KeyItem> keysList = new List<KeyItem>();
        private List<DelayItem> delayList = new List<DelayItem>()
        {
            new DelayItem("Instant", 0),
            new DelayItem("0.2 seconds", 200),
            new DelayItem("0.5 seconds", 500),
            new DelayItem("1.0 seconds", 1000),
            new DelayItem("2.0 seconds", 2000)
        };

        public MainWindow()
        {
            InitializeComponent();
            
            // Force handle creation for hotkey registration
            IntPtr forceHandle = this.Handle;

            SetupDropdowns();
            LoadConfig();

            // Set UI Values from Config
            if (currentMode == 0) rbSleep.Checked = true;
            else rbBlack.Checked = true;

            comboModifier.SelectedValue = currentModifier;
            comboKey.SelectedValue = currentKey;
            comboDelay.SelectedValue = currentDelay;

            chkStartup.Checked = IsStartupEnabled();

            SetupTrayIcon();
            TryRegisterHotkey();
            ApplyModernStyles();
        }

        private void SetupDropdowns()
        {
            // Populate modifiers list
            comboModifier.DisplayMember = "Name";
            comboModifier.ValueMember = "Value";
            comboModifier.DataSource = modifiersList;

            // Populate keys list (A-Z)
            for (char c = 'A'; c <= 'Z'; c++)
            {
                keysList.Add(new KeyItem(c.ToString(), (uint)c));
            }
            // F1-F12 keys
            for (int i = 1; i <= 12; i++)
            {
                keysList.Add(new KeyItem("F" + i, (uint)(111 + i)));
            }
            keysList.Add(new KeyItem("Space", 32));
            keysList.Add(new KeyItem("Enter", 13));
            keysList.Add(new KeyItem("Escape", 27));

            comboKey.DisplayMember = "Name";
            comboKey.ValueMember = "Value";
            comboKey.DataSource = keysList;

            // Populate delay list
            comboDelay.DisplayMember = "Name";
            comboDelay.ValueMember = "Value";
            comboDelay.DataSource = delayList;
        }

        private void SetupTrayIcon()
        {
            trayIcon = new NotifyIcon(components);
            trayIcon.Icon = CreateTrayIcon();
            trayIcon.Text = "OLED Cares (Active)";
            trayIcon.Visible = true;

            // Create Context Menu
            ContextMenu contextMenu = new ContextMenu();

            MenuItem itemTitle = new MenuItem("OLED Cares v1.0");
            itemTitle.Enabled = false;
            contextMenu.MenuItems.Add(itemTitle);

            contextMenu.MenuItems.Add("-");

            MenuItem itemSettings = new MenuItem("Settings...", (s, e) => {
                this.Show();
                this.WindowState = FormWindowState.Normal;
                this.Focus();
            });
            itemSettings.DefaultItem = true;
            contextMenu.MenuItems.Add(itemSettings);

            MenuItem itemTest = new MenuItem("Turn Off Screen Now", (s, e) => {
                TriggerScreenOff();
            });
            contextMenu.MenuItems.Add(itemTest);

            contextMenu.MenuItems.Add("-");

            MenuItem itemExit = new MenuItem("Exit", (s, e) => {
                ExitApp();
            });
            contextMenu.MenuItems.Add(itemExit);

            trayIcon.ContextMenu = contextMenu;

            // Double click opens settings
            trayIcon.DoubleClick += (s, e) => {
                this.Show();
                this.WindowState = FormWindowState.Normal;
                this.Focus();
            };
        }

        private Icon CreateTrayIcon()
        {
            using (Bitmap bmp = new Bitmap(32, 32))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);

                // Draw glowing cyan circle outer ring
                using (Pen ringPen = new Pen(Color.FromArgb(0, 229, 255), 2))
                {
                    g.DrawEllipse(ringPen, 2, 2, 27, 27);
                }

                // Draw monitor stand
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(0, 168, 181)))
                {
                    g.FillRectangle(brush, 13, 21, 6, 5);
                    g.FillRectangle(brush, 9, 25, 14, 2);
                    
                    // Draw monitor screen frame
                    g.FillRectangle(brush, 6, 6, 20, 14);
                }

                // Inner black screen area
                using (SolidBrush dotBrush = new SolidBrush(Color.FromArgb(20, 20, 26)))
                {
                    g.FillRectangle(dotBrush, 8, 8, 16, 10);
                }

                // Neon green glowing dot (OLED health active indicator)
                using (SolidBrush greenBrush = new SolidBrush(Color.FromArgb(57, 255, 20)))
                {
                    g.FillEllipse(greenBrush, 14, 11, 4, 4);
                }

                IntPtr hIcon = bmp.GetHicon();
                return Icon.FromHandle(hIcon);
            }
        }

        private void ApplyModernStyles()
        {
            try
            {
                // Round corners on Windows 11
                int cornerPreference = 2; // DWMWCP_ROUND
                DwmSetWindowAttribute(this.Handle, 33, ref cornerPreference, sizeof(int));

                // Enable Mica/Acrylic backdrop
                int backdropType = 3; // DWMSBT_TRANSIENTWINDOW (Acrylic Blur)
                DwmSetWindowAttribute(this.Handle, 38, ref backdropType, sizeof(int));
            }
            catch { }
        }

        private bool TryRegisterHotkey()
        {
            UnregisterHotKey(this.Handle, 1);
            // 0x4000 is MOD_NOREPEAT (prevents rapid-fire trigger if key is held down)
            bool success = RegisterHotKey(this.Handle, 1, currentModifier | 0x4000, currentKey);

            if (success)
            {
                string modifierName = "Ctrl+Alt";
                foreach (var item in modifiersList)
                {
                    if (item.Value == currentModifier)
                    {
                        modifierName = item.Name.Replace(" ", "");
                        break;
                    }
                }
                
                string keyName = "Z";
                foreach (var item in keysList)
                {
                    if (item.Value == currentKey)
                    {
                        keyName = item.Name;
                        break;
                    }
                }

                lblHotkeyStatus.Text = "Shortcut active: " + modifierName + "+" + keyName;
                lblHotkeyStatus.ForeColor = Color.FromArgb(0, 229, 255);
            }
            else
            {
                lblHotkeyStatus.Text = "Hotkey conflict! Key combination already in use.";
                lblHotkeyStatus.ForeColor = Color.OrangeRed;
            }
            return success;
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_HOTKEY = 0x0312;
            if (m.Msg == WM_HOTKEY)
            {
                int id = m.WParam.ToInt32();
                if (id == 1)
                {
                    TriggerScreenOff();
                }
            }
            base.WndProc(ref m);
        }

        private void TriggerScreenOff()
        {
            // Close settings GUI if it was visible
            if (this.Visible)
            {
                this.Hide();
            }

            if (currentMode == 0)
            {
                // Physical sleep
                PhysicalSleep(currentDelay);
            }
            else
            {
                // OLED Black overlay
                if (currentDelay > 0)
                {
                    System.Threading.ThreadPool.QueueUserWorkItem((state) =>
                    {
                        System.Threading.Thread.Sleep(currentDelay);
                        this.Invoke((MethodInvoker)delegate
                        {
                            BlackScreenManager.Activate();
                        });
                    });
                }
                else
                {
                    BlackScreenManager.Activate();
                }
            }
        }

        private void PhysicalSleep(int delayMs)
        {
            System.Threading.ThreadPool.QueueUserWorkItem((state) =>
            {
                // Wait until all hotkey components (modifiers and the key itself) are fully released
                while (true)
                {
                    bool ctrlDown = (GetAsyncKeyState(0x11) & 0x8000) != 0;
                    bool altDown = (GetAsyncKeyState(0x12) & 0x8000) != 0;
                    bool shiftDown = (GetAsyncKeyState(0x10) & 0x8000) != 0;
                    bool winDown = (GetAsyncKeyState(0x5B) & 0x8000) != 0 || (GetAsyncKeyState(0x5C) & 0x8000) != 0;
                    bool keyDown = (GetAsyncKeyState((int)currentKey) & 0x8000) != 0;

                    if (!ctrlDown && !altDown && !shiftDown && !winDown && !keyDown)
                    {
                        break;
                    }
                    System.Threading.Thread.Sleep(50);
                }

                // Add a small safety buffer before sending the power down command
                System.Threading.Thread.Sleep(150);

                // Send SC_MONITORPOWER command (0xF170) with power off parameter (2) to our own window
                // to avoid blocking on hung external processes like RadeonSoftware.exe.
                if (this.InvokeRequired)
                {
                    this.Invoke((MethodInvoker)delegate
                    {
                        SendMessage(this.Handle, 0x0112, (IntPtr)0xF170, (IntPtr)2);
                    });
                }
                else
                {
                    SendMessage(this.Handle, 0x0112, (IntPtr)0xF170, (IntPtr)2);
                }
            });
        }

        private void LoadConfig()
        {
            if (File.Exists(ConfigPath))
            {
                try
                {
                    string[] lines = File.ReadAllLines(ConfigPath);
                    foreach (string line in lines)
                    {
                        string[] parts = line.Split('=');
                        if (parts.Length == 2)
                        {
                            string key = parts[0].Trim().ToLower();
                            string val = parts[1].Trim();
                            switch (key)
                            {
                                case "mode":
                                    currentMode = int.Parse(val);
                                    break;
                                case "modifier":
                                    currentModifier = uint.Parse(val);
                                    break;
                                case "key":
                                    currentKey = uint.Parse(val);
                                    break;
                                case "delay":
                                    currentDelay = int.Parse(val);
                                    break;
                            }
                        }
                    }
                }
                catch { }
            }
        }

        private void SaveConfig()
        {
            try
            {
                string dir = Path.GetDirectoryName(ConfigPath);
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                List<string> lines = new List<string>();
                lines.Add("mode=" + currentMode);
                lines.Add("modifier=" + currentModifier);
                lines.Add("key=" + currentKey);
                lines.Add("delay=" + currentDelay);
                File.WriteAllLines(ConfigPath, lines.ToArray());
            }
            catch { }
        }

        private void SetStartup(bool runAtStartup)
        {
            try
            {
                Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Run", true
                );
                if (runAtStartup)
                {
                    key.SetValue("OledCares", "\"" + Application.ExecutablePath + "\" --startup");
                }
                else
                {
                    if (key.GetValue("OledCares") != null)
                    {
                        key.DeleteValue("OledCares");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to update startup registry: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private bool IsStartupEnabled()
        {
            try
            {
                Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Run", false
                );
                return key.GetValue("OledCares") != null;
            }
            catch
            {
                return false;
            }
        }

        private void TitleBar_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, (IntPtr)HT_CAPTION, IntPtr.Zero);
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!reallyClose && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                this.Hide();
            }
            else
            {
                UnregisterHotKey(this.Handle, 1);
                base.OnFormClosing(e);
            }
        }

        private void ExitApp()
        {
            reallyClose = true;
            Application.Exit();
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            currentMode = rbSleep.Checked ? 0 : 1;
            currentModifier = (uint)comboModifier.SelectedValue;
            currentKey = (uint)comboKey.SelectedValue;
            currentDelay = (int)comboDelay.SelectedValue;

            SaveConfig();
            SetStartup(chkStartup.Checked);

            bool hotkeyOk = TryRegisterHotkey();
            if (hotkeyOk)
            {
                this.Hide();
                trayIcon.ShowBalloonTip(1500, "OLED Cares", "Saved! Hotkey registered.", ToolTipIcon.Info);
            }
            else
            {
                MessageBox.Show("Settings saved, but keyboard shortcut could not be registered (already in use). Please try another key combination.", "Shortcut Conflict", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void btnTest_Click(object sender, EventArgs e)
        {
            TriggerScreenOff();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            // Draw a subtle dark charcoal border on window edges
            using (Pen borderPen = new Pen(Color.FromArgb(40, 40, 45), 1.5f))
            {
                e.Graphics.DrawRectangle(borderPen, 0, 0, this.Width - 1, this.Height - 1);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (trayIcon != null)
                {
                    trayIcon.Visible = false;
                    trayIcon.Dispose();
                }
                if (components != null)
                {
                    components.Dispose();
                }
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            
            this.pnlTitleBar = new Panel();
            this.lblAppTitle = new Label();
            this.btnMin = new Button();
            this.btnClose = new Button();
            
            this.pnlContent = new Panel();
            
            this.pnlCardMode = new Panel();
            this.lblCardModeTitle = new Label();
            this.rbSleep = new RadioButton();
            this.rbBlack = new RadioButton();
            
            this.pnlCardHotkey = new Panel();
            this.lblCardHotkeyTitle = new Label();
            this.lblModifier = new Label();
            this.comboModifier = new ComboBox();
            this.lblKey = new Label();
            this.comboKey = new ComboBox();
            this.lblHotkeyStatus = new Label();
            
            this.pnlCardOptions = new Panel();
            this.lblCardOptionsTitle = new Label();
            this.chkStartup = new CheckBox();
            this.lblDelay = new Label();
            this.comboDelay = new ComboBox();
            
            this.btnTest = new Button();
            this.btnSave = new Button();
            this.lblCredit = new Label();
            this.btnExit = new Button();
            
            this.SuspendLayout();
            this.pnlTitleBar.SuspendLayout();
            this.pnlContent.SuspendLayout();
            this.pnlCardMode.SuspendLayout();
            this.pnlCardHotkey.SuspendLayout();
            this.pnlCardOptions.SuspendLayout();
            
            // Form MainWindow
            this.Size = new Size(500, 440);
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.Black;
            this.Text = "OLED Cares";
            
            // TitleBar Panel
            this.pnlTitleBar.Location = new Point(0, 0);
            this.pnlTitleBar.Size = new Size(500, 45);
            this.pnlTitleBar.BackColor = Color.Black;
            this.pnlTitleBar.MouseDown += new MouseEventHandler(this.TitleBar_MouseDown);
            
            this.lblAppTitle.Text = "OLED Cares";
            this.lblAppTitle.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
            this.lblAppTitle.ForeColor = Color.White;
            this.lblAppTitle.Location = new Point(15, 12);
            this.lblAppTitle.AutoSize = true;
            this.lblAppTitle.MouseDown += new MouseEventHandler(this.TitleBar_MouseDown); // Make title drag too
            
            this.btnMin.Text = "–";
            this.btnMin.Font = new Font("Segoe UI", 12f, FontStyle.Bold);
            this.btnMin.ForeColor = Color.White;
            this.btnMin.Location = new Point(430, 7);
            this.btnMin.Size = new Size(30, 30);
            this.btnMin.FlatStyle = FlatStyle.Flat;
            this.btnMin.FlatAppearance.BorderSize = 0;
            this.btnMin.FlatAppearance.MouseOverBackColor = Color.FromArgb(30, 30, 30);
            this.btnMin.Click += (s, e) => this.Hide();
            
            this.btnClose.Text = "×";
            this.btnClose.Font = new Font("Segoe UI", 14f, FontStyle.Bold);
            this.btnClose.ForeColor = Color.White;
            this.btnClose.Location = new Point(463, 7);
            this.btnClose.Size = new Size(30, 30);
            this.btnClose.FlatStyle = FlatStyle.Flat;
            this.btnClose.FlatAppearance.BorderSize = 0;
            this.btnClose.FlatAppearance.MouseOverBackColor = Color.FromArgb(200, 30, 30);
            this.btnClose.Click += (s, e) => this.Hide();
            
            // Content Panel
            this.pnlContent.Location = new Point(0, 45);
            this.pnlContent.Size = new Size(500, 395);
            this.pnlContent.BackColor = Color.Black;
            
            // Card Mode Panel
            this.pnlCardMode.Location = new Point(20, 15);
            this.pnlCardMode.Size = new Size(460, 90);
            this.pnlCardMode.BackColor = Color.FromArgb(12, 12, 12);
            
            this.lblCardModeTitle.Text = "Screen Protection Mode";
            this.lblCardModeTitle.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            this.lblCardModeTitle.ForeColor = Color.FromArgb(180, 180, 180);
            this.lblCardModeTitle.Location = new Point(15, 10);
            this.lblCardModeTitle.AutoSize = true;
            
            this.rbBlack.Text = "OLED Black Screen (Instant overlay, pixel off, window safe)";
            this.rbBlack.Font = new Font("Segoe UI", 9f);
            this.rbBlack.ForeColor = Color.FromArgb(220, 220, 220);
            this.rbBlack.Location = new Point(20, 32);
            this.rbBlack.Size = new Size(420, 22);
            
            this.rbSleep.Text = "Physical Screen Sleep (Windows native standby, power saving)";
            this.rbSleep.Font = new Font("Segoe UI", 9f);
            this.rbSleep.ForeColor = Color.FromArgb(220, 220, 220);
            this.rbSleep.Location = new Point(20, 58);
            this.rbSleep.Size = new Size(420, 22);
            
            // Card Hotkey Panel
            this.pnlCardHotkey.Location = new Point(20, 115);
            this.pnlCardHotkey.Size = new Size(460, 105);
            this.pnlCardHotkey.BackColor = Color.FromArgb(12, 12, 12);
            
            this.lblCardHotkeyTitle.Text = "Keyboard Shortcut";
            this.lblCardHotkeyTitle.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            this.lblCardHotkeyTitle.ForeColor = Color.FromArgb(180, 180, 180);
            this.lblCardHotkeyTitle.Location = new Point(15, 10);
            this.lblCardHotkeyTitle.AutoSize = true;
            
            this.lblModifier.Text = "Modifier Keys";
            this.lblModifier.Font = new Font("Segoe UI", 8.5f);
            this.lblModifier.ForeColor = Color.DarkGray;
            this.lblModifier.Location = new Point(20, 32);
            this.lblModifier.AutoSize = true;
            
            this.comboModifier.DropDownStyle = ComboBoxStyle.DropDownList;
            this.comboModifier.FlatStyle = FlatStyle.Flat;
            this.comboModifier.BackColor = Color.Black;
            this.comboModifier.ForeColor = Color.White;
            this.comboModifier.Font = new Font("Segoe UI", 9f);
            this.comboModifier.Location = new Point(20, 50);
            this.comboModifier.Size = new Size(160, 23);
            
            this.lblKey.Text = "Shortcut Key";
            this.lblKey.Font = new Font("Segoe UI", 8.5f);
            this.lblKey.ForeColor = Color.DarkGray;
            this.lblKey.Location = new Point(200, 32);
            this.lblKey.AutoSize = true;
            
            this.comboKey.DropDownStyle = ComboBoxStyle.DropDownList;
            this.comboKey.FlatStyle = FlatStyle.Flat;
            this.comboKey.BackColor = Color.Black;
            this.comboKey.ForeColor = Color.White;
            this.comboKey.Font = new Font("Segoe UI", 9f);
            this.comboKey.Location = new Point(200, 50);
            this.comboKey.Size = new Size(100, 23);
            
            this.lblHotkeyStatus.Text = "Shortcut active";
            this.lblHotkeyStatus.Font = new Font("Segoe UI", 8f, FontStyle.Italic);
            this.lblHotkeyStatus.ForeColor = Color.LightGray;
            this.lblHotkeyStatus.Location = new Point(20, 80);
            this.lblHotkeyStatus.AutoSize = true;
            
            // Card Options Panel
            this.pnlCardOptions.Location = new Point(20, 230);
            this.pnlCardOptions.Size = new Size(460, 85);
            this.pnlCardOptions.BackColor = Color.FromArgb(12, 12, 12);
            
            this.lblCardOptionsTitle.Text = "Startup && Delay Settings";
            this.lblCardOptionsTitle.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            this.lblCardOptionsTitle.ForeColor = Color.FromArgb(180, 180, 180);
            this.lblCardOptionsTitle.Location = new Point(15, 10);
            this.lblCardOptionsTitle.AutoSize = true;
            
            this.chkStartup.Text = "Start automatically with Windows";
            this.chkStartup.Font = new Font("Segoe UI", 9f);
            this.chkStartup.ForeColor = Color.FromArgb(220, 220, 220);
            this.chkStartup.Location = new Point(20, 32);
            this.chkStartup.Size = new Size(220, 22);
            this.chkStartup.FlatStyle = FlatStyle.Flat;
            
            this.lblDelay.Text = "Activation Delay";
            this.lblDelay.Font = new Font("Segoe UI", 8.5f);
            this.lblDelay.ForeColor = Color.DarkGray;
            this.lblDelay.Location = new Point(260, 32);
            this.lblDelay.AutoSize = true;
            
            this.comboDelay.DropDownStyle = ComboBoxStyle.DropDownList;
            this.comboDelay.FlatStyle = FlatStyle.Flat;
            this.comboDelay.BackColor = Color.Black;
            this.comboDelay.ForeColor = Color.White;
            this.comboDelay.Font = new Font("Segoe UI", 9f);
            this.comboDelay.Location = new Point(260, 50);
            this.comboDelay.Size = new Size(180, 23);
            
            // Test Button
            this.btnTest.Text = "Test Screen Off";
            this.btnTest.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            this.btnTest.BackColor = Color.Black;
            this.btnTest.ForeColor = Color.White;
            this.btnTest.Location = new Point(20, 328);
            this.btnTest.Size = new Size(130, 35);
            this.btnTest.FlatStyle = FlatStyle.Flat;
            this.btnTest.FlatAppearance.BorderSize = 1;
            this.btnTest.FlatAppearance.BorderColor = Color.FromArgb(50, 50, 50);
            this.btnTest.FlatAppearance.MouseOverBackColor = Color.FromArgb(20, 20, 20);
            this.btnTest.Click += new EventHandler(this.btnTest_Click);
            
            // Save Button
            this.btnSave.Text = "Apply Settings";
            this.btnSave.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            this.btnSave.BackColor = Color.Black;
            this.btnSave.ForeColor = Color.FromArgb(0, 229, 255);
            this.btnSave.Location = new Point(310, 328);
            this.btnSave.Size = new Size(170, 35);
            this.btnSave.FlatStyle = FlatStyle.Flat;
            this.btnSave.FlatAppearance.BorderSize = 1;
            this.btnSave.FlatAppearance.BorderColor = Color.FromArgb(0, 168, 181);
            this.btnSave.FlatAppearance.MouseOverBackColor = Color.FromArgb(0, 40, 50);
            this.btnSave.Click += new EventHandler(this.btnSave_Click);
            
            // Footer Credit Label
            this.lblCredit.Text = "OLED Cares v1.0 • Designed for OLED Panels";
            this.lblCredit.Font = new Font("Segoe UI", 8f);
            this.lblCredit.ForeColor = Color.Gray;
            this.lblCredit.Location = new Point(20, 375);
            this.lblCredit.AutoSize = true;
            
            // Exit Label Button
            this.btnExit.Text = "Exit App";
            this.btnExit.Font = new Font("Segoe UI", 8f, FontStyle.Underline);
            this.btnExit.ForeColor = Color.FromArgb(255, 99, 71);
            this.btnExit.BackColor = Color.Transparent;
            this.btnExit.Location = new Point(410, 372);
            this.btnExit.Size = new Size(70, 20);
            this.btnExit.FlatStyle = FlatStyle.Flat;
            this.btnExit.FlatAppearance.BorderSize = 0;
            this.btnExit.FlatAppearance.MouseOverBackColor = Color.FromArgb(40, 20, 20);
            this.btnExit.Click += (s, e) => this.ExitApp();
            
            // Assemble components
            this.pnlTitleBar.Controls.Add(this.lblAppTitle);
            this.pnlTitleBar.Controls.Add(this.btnMin);
            this.pnlTitleBar.Controls.Add(this.btnClose);
            
            this.pnlCardMode.Controls.Add(this.lblCardModeTitle);
            this.pnlCardMode.Controls.Add(this.rbSleep);
            this.pnlCardMode.Controls.Add(this.rbBlack);
            
            this.pnlCardHotkey.Controls.Add(this.lblCardHotkeyTitle);
            this.pnlCardHotkey.Controls.Add(this.lblModifier);
            this.pnlCardHotkey.Controls.Add(this.comboModifier);
            this.pnlCardHotkey.Controls.Add(this.lblKey);
            this.pnlCardHotkey.Controls.Add(this.comboKey);
            this.pnlCardHotkey.Controls.Add(this.lblHotkeyStatus);
            
            this.pnlCardOptions.Controls.Add(this.lblCardOptionsTitle);
            this.pnlCardOptions.Controls.Add(this.chkStartup);
            this.pnlCardOptions.Controls.Add(this.lblDelay);
            this.pnlCardOptions.Controls.Add(this.comboDelay);
            
            this.pnlContent.Controls.Add(this.pnlCardMode);
            this.pnlContent.Controls.Add(this.pnlCardHotkey);
            this.pnlContent.Controls.Add(this.pnlCardOptions);
            this.pnlContent.Controls.Add(this.btnTest);
            this.pnlContent.Controls.Add(this.btnSave);
            this.pnlContent.Controls.Add(this.lblCredit);
            this.pnlContent.Controls.Add(this.btnExit);
            
            this.Controls.Add(this.pnlTitleBar);
            this.Controls.Add(this.pnlContent);
            
            this.pnlTitleBar.ResumeLayout(false);
            this.pnlTitleBar.PerformLayout();
            this.pnlCardMode.ResumeLayout(false);
            this.pnlCardMode.PerformLayout();
            this.pnlCardHotkey.ResumeLayout(false);
            this.pnlCardHotkey.PerformLayout();
            this.pnlCardOptions.ResumeLayout(false);
            this.pnlCardOptions.PerformLayout();
            this.pnlContent.ResumeLayout(false);
            this.pnlContent.PerformLayout();
            this.ResumeLayout(false);
        }
    }

    public static class BlackScreenManager
    {
        private static List<Form> activeForms = new List<Form>();
        private static Point initialMousePos;
        private static DateTime activationTime;
        public static bool isActive = false;
        private static Timer sleepTimer = null;
        private static bool isDisplayPhysicallyOff = false;

        // WIN32 APIs for Idle Detection and Power Settings
        [StructLayout(LayoutKind.Sequential)]
        private struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }

        [DllImport("user32.dll")]
        private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("powrprof.dll")]
        private static extern uint PowerGetActiveScheme(IntPtr UserRootPowerKey, out IntPtr ActivePolicyGuid);

        [DllImport("powrprof.dll")]
        private static extern uint PowerReadACValueIndex(IntPtr RootPowerKey, ref Guid SchemeGuid, ref Guid SubGroupOfPowerSettingGuid, ref Guid PowerSettingGuid, out uint AcValueIndex);

        [DllImport("powrprof.dll")]
        private static extern uint PowerReadDCValueIndex(IntPtr RootPowerKey, ref Guid SchemeGuid, ref Guid SubGroupOfPowerSettingGuid, ref Guid PowerSettingGuid, out uint DcValueIndex);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr LocalFree(IntPtr hMem);



        private static readonly Guid GUID_VIDEO_SUBGROUP = new Guid("7516b95f-f776-4464-8c53-06167f40cc99");
        private static readonly Guid GUID_VIDEO_IDLE = new Guid("3c0bc021-c8a8-4e07-a973-6b14cbcb2b7e");

        private static uint GetIdleTime()
        {
            LASTINPUTINFO lastInputInfo = new LASTINPUTINFO();
            lastInputInfo.cbSize = (uint)Marshal.SizeOf(lastInputInfo);
            lastInputInfo.dwTime = 0;

            if (GetLastInputInfo(ref lastInputInfo))
            {
                uint envTicks = (uint)Environment.TickCount;
                uint lastInputTick = lastInputInfo.dwTime;

                if (envTicks >= lastInputTick)
                {
                    return envTicks - lastInputTick;
                }
                else
                {
                    return (uint.MaxValue - lastInputTick) + envTicks;
                }
            }
            return 0;
        }



        private static uint GetActiveDisplayTimeout()
        {
            IntPtr pActiveSchemeGuid = IntPtr.Zero;
            try
            {
                uint result = PowerGetActiveScheme(IntPtr.Zero, out pActiveSchemeGuid);
                if (result != 0) return 0;

                Guid activeSchemeGuid = (Guid)Marshal.PtrToStructure(pActiveSchemeGuid, typeof(Guid));
                uint timeoutSeconds = 0;
                uint readResult;

                Guid videoSubgroup = GUID_VIDEO_SUBGROUP;
                Guid videoIdle = GUID_VIDEO_IDLE;

                if (SystemInformation.PowerStatus.PowerLineStatus == PowerLineStatus.Online)
                {
                    readResult = PowerReadACValueIndex(IntPtr.Zero, ref activeSchemeGuid, ref videoSubgroup, ref videoIdle, out timeoutSeconds);
                }
                else
                {
                    readResult = PowerReadDCValueIndex(IntPtr.Zero, ref activeSchemeGuid, ref videoSubgroup, ref videoIdle, out timeoutSeconds);
                }

                if (readResult == 0)
                {
                    return timeoutSeconds;
                }
            }
            catch
            {
                // Fallback to 0 if failed
            }
            finally
            {
                if (pActiveSchemeGuid != IntPtr.Zero)
                {
                    LocalFree(pActiveSchemeGuid);
                }
            }
            return 0;
        }

        public static void Activate()
        {
            if (isActive) return;
            isActive = true;
            activeForms.Clear();
            initialMousePos = Cursor.Position;
            activationTime = DateTime.Now;
            isDisplayPhysicallyOff = false;

            HideCursor();

            foreach (Screen screen in Screen.AllScreens)
            {
                Form f = new Form();
                f.FormBorderStyle = FormBorderStyle.None;
                f.StartPosition = FormStartPosition.Manual;
                f.Bounds = screen.Bounds;
                f.BackColor = Color.Black;
                f.TopMost = true;
                f.ShowInTaskbar = false;

                // Mouse move threshold - ignore tiny movements and first 1.0s cooldown
                f.MouseMove += (s, e) => {
                    if ((DateTime.Now - activationTime).TotalMilliseconds < 1000) return;
                    Point currentPos = Cursor.Position;
                    if (Math.Abs(currentPos.X - initialMousePos.X) > 15 || 
                        Math.Abs(currentPos.Y - initialMousePos.Y) > 15)
                    {
                        Deactivate();
                    }
                };

                // Keyboard input dismisses the black screen (after cooldown)
                f.KeyDown += (s, e) => {
                    if ((DateTime.Now - activationTime).TotalMilliseconds < 1000) return;
                    Deactivate();
                };

                // Mouse clicks dismiss too
                f.MouseDown += (s, e) => {
                    if ((DateTime.Now - activationTime).TotalMilliseconds < 1000) return;
                    Deactivate();
                };

                f.Show();
                f.Focus();
                activeForms.Add(f);
            }

            // Start sleep monitor timer (every 5 seconds)
            sleepTimer = new Timer();
            sleepTimer.Interval = 5000;
            sleepTimer.Tick += SleepTimer_Tick;
            sleepTimer.Start();
        }

        private static void SleepTimer_Tick(object sender, EventArgs e)
        {
            if (!isActive) return;

            uint idleMs = GetIdleTime();
            uint idleSec = idleMs / 1000;

            uint displayTimeoutSec = GetActiveDisplayTimeout();

            // Check display timeout (physically turn off the monitor)
            if (displayTimeoutSec > 0 && idleSec >= displayTimeoutSec && !isDisplayPhysicallyOff)
            {
                isDisplayPhysicallyOff = true;
                if (activeForms.Count > 0)
                {
                    Form targetForm = activeForms[0];
                    if (targetForm.InvokeRequired)
                    {
                        targetForm.Invoke((MethodInvoker)delegate
                        {
                            SendMessage(targetForm.Handle, 0x0112, (IntPtr)0xF170, (IntPtr)2);
                        });
                    }
                    else
                    {
                        SendMessage(targetForm.Handle, 0x0112, (IntPtr)0xF170, (IntPtr)2);
                    }
                }
            }
        }

        public static void Deactivate()
        {
            if (!isActive) return;
            isActive = false;

            if (sleepTimer != null)
            {
                sleepTimer.Stop();
                sleepTimer.Dispose();
                sleepTimer = null;
            }

            isDisplayPhysicallyOff = false;

            ShowCursor();
            foreach (Form f in activeForms)
            {
                f.Close();
            }
            activeForms.Clear();
        }

        private static bool isCursorHidden = false;
        private static void HideCursor()
        {
            if (!isCursorHidden)
            {
                Cursor.Hide();
                isCursorHidden = true;
            }
        }
        private static void ShowCursor()
        {
            if (isCursorHidden)
            {
                Cursor.Show();
                isCursorHidden = false;
            }
        }
    }
}
