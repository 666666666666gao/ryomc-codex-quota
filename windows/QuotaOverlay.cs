using System;
using System.Drawing;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

public class WeeklyQuota { public double? remainingPercent; public string resetAt; public string queriedAt; public int windowSeconds; }
public class QuotaOverlay : Form {
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint p);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int Left, Top, Right, Bottom; }
    readonly Label text = new Label();
    readonly ToolTip tip = new ToolTip();
    readonly Timer positionTimer = new Timer();
    readonly Timer refreshTimer = new Timer();
    readonly HttpClient client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });
    readonly JavaScriptSerializer json = new JavaScriptSerializer();
    bool shared;
    bool busy, settingsOpen;
    IntPtr codex;
    int offsetX = -510, offsetY = -135;
    Point dragStart;
    bool dragging;
    protected override bool ShowWithoutActivation { get { return true; } }
    protected override CreateParams CreateParams { get { var p = base.CreateParams; p.ExStyle |= 0x08000000 | 0x80; return p; } }
    public QuotaOverlay(bool sharedMode) {
        shared = sharedMode;
        FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; TopMost = true;
        StartPosition = FormStartPosition.Manual; Size = new Size(490, 44);
        BackColor = Color.FromArgb(21, 44, 36); Text = "Ryomc 周额度悬浮条";
        text.Text = "线路周额度 · 正在查询…"; text.ForeColor = Color.FromArgb(174, 244, 207);
        text.Font = new Font("Microsoft YaHei UI", 9); text.Location = new Point(12, 10); text.Size = new Size(305, 25); text.AutoEllipsis=true;
        var settings = new Button { Text = "连接设置", Location = new Point(320, 6), Size = new Size(82, 31), FlatStyle = FlatStyle.Flat, ForeColor = Color.White, TabStop = false };
        var refresh = new Button { Text = "↻", Location = new Point(407, 6), Size = new Size(32, 31), FlatStyle = FlatStyle.Flat, ForeColor = Color.White, TabStop = false };
        var close = new Button { Text = "×", Location = new Point(446, 6), Size = new Size(32, 31), FlatStyle = FlatStyle.Flat, ForeColor = Color.White, TabStop = false };
        Controls.AddRange(new Control[] {text, settings, refresh, close});
        settings.Click += async (s,e) => {
            if(busy) return;
            settingsOpen=true;
            using(var dialog=new ConnectionSettings()) {dialog.ShowDialog(); if(dialog.Changed) shared=false;}
            settingsOpen=false; await RefreshQuota();
        };
        refresh.Click += async (s,e) => await RefreshQuota(); close.Click += (s,e) => Close();
        text.MouseDown += (s,e) => { if(e.Button == MouseButtons.Left) { dragging = true; dragStart = Cursor.Position; } };
        text.MouseMove += (s,e) => { if(dragging) { var p=Cursor.Position; offsetX += p.X-dragStart.X; offsetY += p.Y-dragStart.Y; dragStart=p; MoveToCodex(); } };
        text.MouseUp += (s,e) => dragging=false;
        client.Timeout = TimeSpan.FromSeconds(35);
        positionTimer.Interval = 500; positionTimer.Tick += (s,e) => TrackCodex(); positionTimer.Start();
        refreshTimer.Interval = 120000; refreshTimer.Tick += async (s,e) => { if(Visible) await RefreshQuota(); }; refreshTimer.Start();
        Shown += async (s,e) => { TrackCodex(); await RefreshQuota(); };
        FormClosed += (s,e) => { positionTimer.Dispose(); refreshTimer.Dispose(); client.Dispose(); tip.Dispose(); };
    }
    void TrackCodex() {
        if(settingsOpen) return;
        var h = GetForegroundWindow(); uint id; GetWindowThreadProcessId(h, out id);
        if(id == Process.GetCurrentProcess().Id) return;
        string name;
        try { using(var process=Process.GetProcessById((int)id)) name=process.ProcessName; }
        catch(ArgumentException) { Hide(); return; }
        if(name != "ChatGPT" && name != "Codex") { Hide(); return; }
        codex=h; MoveToCodex(); if(!Visible) { Show(); var refreshTask = RefreshQuota(); }
    }
    void MoveToCodex() {
        RECT r; if(codex == IntPtr.Zero || !GetWindowRect(codex,out r)) return;
        var screen=Screen.FromHandle(codex).WorkingArea;
        int x=Math.Max(screen.Left,Math.Min(screen.Right-Width,r.Right+offsetX));
        int y=Math.Max(screen.Top,Math.Min(screen.Bottom-Height,r.Bottom+offsetY));
        Location = new Point(x,y);
    }
    async Task RefreshQuota() {
        if(busy || settingsOpen) return; busy=true; text.Text="线路周额度 · 正在查询…";
        string stage="读取连接配置";
        try {
            var manual=shared ? null : CredentialStore.Load();
            var cfg=manual ?? CodexConnection.Load(shared);
            stage="解析额度响应";
            var q=await QuotaClient.Query(client,cfg);
                var reset=String.IsNullOrEmpty(q.resetAt) ? "重置时间未知" : TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.Parse(q.resetAt),"China Standard Time").ToString("MM/dd HH:mm")+" 重置";
                var queried=TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.Parse(q.queriedAt),"China Standard Time");
                text.Text=(manual!=null ? "独立 · " : "")+"周剩余 "+q.remainingPercent.Value.ToString("0.##")+"% · "+reset;
                text.ForeColor=Color.FromArgb(174,244,207);
                tip.SetToolTip(text,(manual!=null ? "模式：用户保存的独立连接" : shared ? "模式：显式共享只读" : "模式：自动读取 Codex")+"\n共享线路周额度，不是个人钱包余额。\n来源："+new Uri(cfg.endpoint).Host+"\n查询时间："+queried.ToString("yyyy-MM-dd HH:mm:ss")+" 北京时间\n每两分钟更新；拖动可移动。");
        } catch(Exception ex) { var message=QuotaClient.Error(ex,stage); text.Text=message; text.ForeColor=Color.LightSalmon; tip.SetToolTip(text,message+"\n可点击连接设置。未显示旧额度或原始凭据。"); }
        finally {busy=false;}
    }
    [STAThread] public static void Main(string[] args) {
        if(Array.IndexOf(args,"--settings")>=0) {
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;
            Application.Run(new ConnectionSettings()); return;
        }
        bool created; using(var mutex = new System.Threading.Mutex(true,"Local\\RyomcQuotaOverlay",out created)) {
            if(!created) return;
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;
            Application.Run(new QuotaOverlay(Array.IndexOf(args,"--shared")>=0));
        }
    }
}
