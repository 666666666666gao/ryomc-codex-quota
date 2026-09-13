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
    readonly bool shared;
    bool busy;
    IntPtr codex;
    int offsetX = -390, offsetY = -135;
    Point dragStart;
    bool dragging;
    protected override bool ShowWithoutActivation { get { return true; } }
    protected override CreateParams CreateParams { get { var p = base.CreateParams; p.ExStyle |= 0x08000000 | 0x80; return p; } }
    public QuotaOverlay(bool sharedMode) {
        shared = sharedMode;
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
            var cfg=CodexConnection.Load(shared);
            using(var request=new HttpRequestMessage(HttpMethod.Get,cfg.endpoint)) {
            request.Headers.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",cfg.readToken);
            if(cfg.model!=null) request.Headers.Add("X-Quota-Model",cfg.model);
            using(var response=await client.SendAsync(request)) {
                if(!response.IsSuccessStatusCode) { text.Text="线路周额度 · 查询失败 HTTP "+(int)response.StatusCode; text.ForeColor=Color.LightSalmon; tip.SetToolTip(text,"未显示旧数据。401：密钥无效/禁用；403：线路访问受限；404：此站不支持额度接口；409：上游不唯一；503：服务暂不可用。"); return; }
                var q=json.Deserialize<WeeklyQuota>(await response.Content.ReadAsStringAsync());
                if(q == null || q.windowSeconds != 604800 || !q.remainingPercent.HasValue || double.IsNaN(q.remainingPercent.Value) || q.remainingPercent < 0 || q.remainingPercent > 100) throw new FormatException();
                var reset=String.IsNullOrEmpty(q.resetAt) ? "重置时间未知" : TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.Parse(q.resetAt),"China Standard Time").ToString("MM/dd HH:mm")+" 重置";
                var queried=TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.Parse(q.queriedAt),"China Standard Time");
                text.Text="周剩余 "+q.remainingPercent.Value.ToString("0.##")+"% · "+reset;
                text.ForeColor=Color.FromArgb(174,244,207);
                tip.SetToolTip(text,"共享线路周额度，不是个人钱包余额。\n来源："+new Uri(cfg.endpoint).Host+"\n查询时间："+queried.ToString("yyyy-MM-dd HH:mm:ss")+" 北京时间\n读取已保存的用户级配置，不读取会话临时覆盖。每两分钟更新；拖动可移动。");
            }
            }
        } catch(Exception) { text.Text="线路周额度 · 配置或连接不可用"; text.ForeColor=Color.LightSalmon; tip.SetToolTip(text,"需要当前 Codex 用户配置中的中转 HTTPS Base URL 与 API 密钥。官方 OAuth 登录不能自动查询中转上游；不显示旧值或原始错误。"); }
        finally {busy=false;}
    }
    [STAThread] public static void Main(string[] args) {
        bool created; using(var mutex = new System.Threading.Mutex(true,"Local\\RyomcQuotaOverlay",out created)) {
            if(!created) return;
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;
            Application.Run(new QuotaOverlay(Array.IndexOf(args,"--shared")>=0));
        }
    }
}
