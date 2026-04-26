using System;
using System.Windows.Forms;
using System.IO;
using System.Text;
using System.Threading;
using System.Runtime.InteropServices;
using Gecko;

namespace GeckoBrowserApp
{
    static class Program
    {
        private static Mutex mutex = new Mutex(false, "GeckoBrowserApp-Unique-12345");

        [STAThread]
        static void Main()
        {
            string libPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "lib");
            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) => {
                string assemblyName = new System.Reflection.AssemblyName(args.Name).Name + ".dll";
                string assemblyPath = Path.Combine(libPath, assemblyName);
                if (File.Exists(assemblyPath)) return System.Reflection.Assembly.LoadFrom(assemblyPath);
                return null;
            };

            if (!mutex.WaitOne(0, false)) return;

            try
            {
                string stopFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "stop.data");
                if (File.Exists(stopFile)) { try { File.Delete(stopFile); } catch { } }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());
            }
            finally
            {
                mutex.ReleaseMutex();
            }
        }
    }

    public class MainForm : Form
    {
        private GeckoWebBrowser browser;
        private MenuStrip menuStrip;
        private System.Windows.Forms.Timer stopWatcher;

        [DllImport("kernel32", CharSet = CharSet.Unicode)]
        static extern int GetPrivateProfileString(string Section, string Key, string Default, StringBuilder RetVal, int Size, string FilePath);

        public MainForm()
        {
            this.Text = "My Custom App";
            CreateMenu();
            InitializeStopWatcher();

            string iniPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Program.ini");
            string targetUrl = ReadIni(iniPath, "Settings", "Url", "http://example.com");
            string widthStr = ReadIni(iniPath, "Settings", "Width", "1024");
            string heightStr = ReadIni(iniPath, "Settings", "Height", "768");

            int width, height;
            if (int.TryParse(widthStr, out width)) this.Width = width;
            if (int.TryParse(heightStr, out height)) this.Height = height;

            InitializeGecko(targetUrl);
        }

        private void CreateMenu()
        {
            menuStrip = new MenuStrip();
            ToolStripMenuItem actionMenu = new ToolStripMenuItem("操作(&O)");
            ToolStripMenuItem openUrlItem = new ToolStripMenuItem("URLを開く(&L)");
            openUrlItem.ShortcutKeys = Keys.Control | Keys.L;
            openUrlItem.Click += (s, e) => {
                string url = PromptForUrl(browser.Url != null ? browser.Url.ToString() : "");
                if (!string.IsNullOrEmpty(url)) browser.Navigate(url);
            };
            actionMenu.DropDownItems.Add(openUrlItem);
            menuStrip.Items.Add(actionMenu);
            this.MainMenuStrip = menuStrip;
            this.Controls.Add(menuStrip);
        }

        private void InitializeStopWatcher()
        {
            stopWatcher = new System.Windows.Forms.Timer();
            stopWatcher.Interval = 2000;
            stopWatcher.Tick += (s, e) => {
                if (File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "stop.data"))) this.Close();
            };
            stopWatcher.Start();
        }

        private string ReadIni(string path, string section, string key, string defaultValue)
        {
            if (!File.Exists(path)) return defaultValue;
            StringBuilder temp = new StringBuilder(1024);
            GetPrivateProfileString(section, key, defaultValue, temp, 1024, path);
            return temp.ToString();
        }

        private void InitializeGecko(string initialUrl)
        {
            // lib フォルダの中にある Runtime フォルダを指定
            string runtimePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "lib", "Runtime");
            Xpcom.Initialize(runtimePath);

            GeckoPreferences.User["security.mixed_content.block_active_content"] = false;
            GeckoPreferences.User["security.mixed_content.block_display_content"] = false;

            browser = new GeckoWebBrowser() { Dock = DockStyle.Fill, NoDefaultContextMenu = true };
            this.Controls.Add(browser);
            browser.BringToFront();
            browser.Navigate(initialUrl);
        }

        private string PromptForUrl(string currentUrl)
        {
            Form p = new Form() { Width = 500, Height = 130, FormBorderStyle = FormBorderStyle.FixedDialog, Text = "URLを開く", StartPosition = FormStartPosition.CenterParent };
            Label l = new Label() { Left = 15, Top = 15, Text = "新しいURLを入力してください:", Width = 450 };
            TextBox t = new TextBox() { Left = 15, Top = 40, Width = 450, Text = currentUrl };
            Button b = new Button() { Text = "移動", Left = 365, Top = 70, Width = 100, DialogResult = DialogResult.OK };
            p.AcceptButton = b;
            p.Controls.Add(l); p.Controls.Add(t); p.Controls.Add(b);
            p.Load += (s, e) => { t.SelectAll(); t.Focus(); };
            return p.ShowDialog(this) == DialogResult.OK ? t.Text : null;
        }
    }
}