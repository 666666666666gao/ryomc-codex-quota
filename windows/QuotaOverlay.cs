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

public class OverlayConfig { public string endpoint; public string readToken; }
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
    readonly OverlayConfig cfg;
    bool busy;
    IntPtr codex;
    int offsetX = -390, offsetY = -135;
    Point dragStart;
    bool dragging;
    protected override bool ShowWithoutActivation { get { return true; } }
    protected override CreateParams CreateParams { get { var p = base.CreateParams; p.ExStyle |= 0x08000000 | 0x80; return p; } }
    public QuotaOverlay(OverlayConfig config) {
        cfg = config;
        FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; TopMost = true;
        StartPosition = FormStartPosition.Manual; Size = new Size(370, 44);
        BackColor = Color.FromArgb(21, 44, 36); Text = "Ryomc 周额度悬浮条";
        text.Text = "线路周额度 · 正在查询…"; text.ForeColor = Color.FromArgb(174, 244, 207);
        text.Font = new Font("Microsoft YaHei UI", 9); text.Location = new Point(12, 10); text.Size = new Size(276, 25);
        var refresh = new Button { Text = "↻", Location = new Point(291, 6), Size = new Size(32, 31), FlatStyle = FlatStyle.Flat, ForeColor = Color.White, TabStop = false };
        var close = new Button { Text = "×", Location = new Point(330, 6), Size = new Size(32, 31), FlatStyle = FlatStyle.Flat, ForeColor = Color.White, TabStop = false };
        Controls.AddRange(new Control[] {text, refresh, close});
        refresh.Click += async (s,e) => await RefreshQuota(); close.Click += (s,e) => Close();
        text.MouseDown += (s,e) => { if(e.Button == MouseButtons.Left) { dragging = true; dragStart = Cursor.Position; } };
        text.MouseMove += (s,e) => { if(dragging) { var p=Cursor.Position; offsetX += p.X-dragStart.X; offsetY += p.Y-dragStart.Y; dragStart=p; MoveToCodex(); } };
        text.MouseUp += (s,e) => dragging=false;
        client.Timeout = TimeSpan.FromSeconds(35);
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", cfg.readToken);
        positionTimer.Interval = 500; positionTimer.Tick += (s,e) => TrackCodex(); positionTimer.Start();
        refreshTimer.Interval = 120000; refreshTimer.Tick += async (s,e) => { if(Visible) await RefreshQuota(); }; refreshTimer.Start();
        Shown += async (s,e) => { TrackCodex(); await RefreshQuota(); };
        FormClosed += (s,e) => { positionTimer.Dispose(); refreshTimer.Dispose(); client.Dispose(); tip.Dispose(); };
    }
    void TrackCodex() {
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
        if(busy) return; busy=true; text.Text="线路周额度 · 正在查询…";
        try {
            using(var response=await client.GetAsync(cfg.endpoint)) {
                if(!response.IsSuccessStatusCode) { text.Text="线路周额度 · 查询失败 HTTP "+(int)response.StatusCode; text.ForeColor=Color.LightSalmon; tip.SetToolTip(text,"未显示旧数据。401 请检查只读令牌，503 表示上游查询不可用。"); return; }
                var q=json.Deserialize<WeeklyQuota>(await response.Content.ReadAsStringAsync());
                if(q == null || q.windowSeconds != 604800 || !q.remainingPercent.HasValue || double.IsNaN(q.remainingPercent.Value) || q.remainingPercent < 0 || q.remainingPercent > 100) throw new FormatException();
                var reset=String.IsNullOrEmpty(q.resetAt) ? "重置时间未知" : TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.Parse(q.resetAt),"China Standard Time").ToString("MM/dd HH:mm")+" 重置";
                var queried=TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.Parse(q.queriedAt),"China Standard Time");
                text.Text="周剩余 "+q.remainingPercent.Value.ToString("0.##")+"% · "+reset;
                text.ForeColor=Color.FromArgb(174,244,207);
                tip.SetToolTip(text,"共享线路周额度，不是个人钱包余额。\n查询时间："+queried.ToString("yyyy-MM-dd HH:mm:ss")+" 北京时间\n拖动文字移动位置；每两分钟更新，服务端缓存最多两分钟。");
            }
        } catch(Exception) { text.Text="线路周额度 · 连接或数据异常"; text.ForeColor=Color.LightSalmon; tip.SetToolTip(text,"本次查询失败，未显示旧值或原始错误。"); }
        finally {busy=false;}
    }
    [STAThread] public static void Main() {
        bool created; using(var mutex = new System.Threading.Mutex(true,"Local\\RyomcQuotaOverlay",out created)) {
            if(!created) return;
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            try {
                string path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),".config","ryomc-codex-quota","reader.json");
                var c=new JavaScriptSerializer().Deserialize<OverlayConfig>(File.ReadAllText(path));
                Uri u;
                if(c==null || !Uri.TryCreate(c.endpoint,UriKind.Absolute,out u) || u.Scheme!="https" || !String.IsNullOrEmpty(u.UserInfo) || !String.IsNullOrEmpty(u.Query) || !String.IsNullOrEmpty(u.Fragment) || String.IsNullOrWhiteSpace(c.readToken)) throw new FormatException();
                ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;
                Application.Run(new QuotaOverlay(c));
            } catch(Exception) { MessageBox.Show("请先配置用户目录 .config/ryomc-codex-quota/reader.json（endpoint 和 readToken），不要填写 CPA 管理密钥。","Ryomc 额度悬浮条"); }
        }
    }
}
