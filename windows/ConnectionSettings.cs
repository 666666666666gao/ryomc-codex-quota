using System;
using System.Drawing;
using System.Net.Http;
using System.Windows.Forms;

public class ConnectionSettings : Form {
    readonly TextBox url=new TextBox(), model=new TextBox(), key=new TextBox();
    readonly Label status=new Label();
    readonly Button save=new Button(), automatic=new Button();
    public bool Changed;
    public ConnectionSettings() {
        Text="Ryomc · 连接设置"; StartPosition=FormStartPosition.CenterScreen;
        ClientSize=new Size(600,350); FormBorderStyle=FormBorderStyle.FixedDialog; MaximizeBox=false; MinimizeBox=false;
        Font=new Font("Microsoft YaHei UI",9); BackColor=Color.White;
        var explanation=new Label {Text="独立连接：只用于额度悬浮条，不修改 Codex 配置。\n密钥保存在当前用户的 Windows 凭据管理器，不读取 Codex 系统凭据。",Location=new Point(20,15),Size=new Size(560,45)};
        Controls.Add(explanation);
        AddField("Base URL（包含 /v1）",url,70);
        AddField("模型",model,112);
        AddField("网站 API 密钥",key,154); key.UseSystemPasswordChar=true;
        status.SetBounds(20,204,560,65); status.ForeColor=Color.DarkSlateGray;
        save.Text="保存并测试"; save.SetBounds(340,288,120,36);
        automatic.Text="恢复自动读取"; automatic.SetBounds(20,288,140,36);
        var close=new Button {Text="关闭",Location=new Point(475,288),Size=new Size(100,36)};
        Controls.AddRange(new Control[] {status,save,automatic,close}); close.Click+=(s,e)=>Close();
        FormClosing+=(s,e)=>{if(!save.Enabled) e.Cancel=true;};
        save.Click+=async(s,e)=>{
            save.Enabled=automatic.Enabled=false; url.Enabled=model.Enabled=key.Enabled=false;
            string stage="保存连接";
            bool saved=false;
            try {
                var c=CodexConnection.FromInput(url.Text.Trim(),model.Text.Trim(),key.Text);
                CredentialStore.Save(c); Changed=true; saved=true; key.Clear();
                status.Text="已保存到 Windows 凭据管理器，正在测试…";
                stage="解析额度响应";
                using(var client=new HttpClient(new HttpClientHandler {AllowAutoRedirect=false})) {
                    client.Timeout=TimeSpan.FromSeconds(35);
                    var q=await QuotaClient.Query(client,c);
                    status.Text="连接成功 · 周剩余 "+q.remainingPercent.Value.ToString("0.##")+"%\n独立连接已启用。可关闭此窗口查看悬浮条。";
                }
            } catch(Exception ex) {status.Text=(saved ? "独立连接已保存；" : "本次未保存；")+QuotaClient.Error(ex,stage);}
            finally {save.Enabled=automatic.Enabled=true; url.Enabled=model.Enabled=key.Enabled=true;}
        };
        automatic.Click+=(s,e)=>{
            if(MessageBox.Show(this,"移除本插件保存的独立连接并恢复自动读取？不会修改 Codex 配置。","恢复自动读取",MessageBoxButtons.YesNo)!=DialogResult.Yes) return;
            try {CredentialStore.Remove(); Changed=true; key.Clear(); status.Text="独立连接已移除，已恢复自动读取。";}
            catch(Exception ex) {status.Text=QuotaClient.Error(ex,"恢复自动读取");}
        };
        try {
            var c=CredentialStore.Load(); bool manual=c!=null;
            if(c==null) c=CodexConnection.ReadSettings();
            const string suffix="/quota/weekly";
            url.Text=c.endpoint.EndsWith(suffix) ? c.endpoint.Substring(0,c.endpoint.Length-suffix.Length) : c.endpoint;
            model.Text=c.model;
            status.Text=manual ? "当前使用独立连接。密钥不回填；更改连接时请重新输入密钥。" : "已从 Codex 自动填入站点与模型。请在本机输入该站创建的 API 密钥。";
        } catch(Exception ex) {status.Text=QuotaClient.Error(ex,"读取站点配置")+"。可以手动填写。";}
    }
    void AddField(string label,TextBox input,int y) {
        Controls.Add(new Label {Text=label,Location=new Point(20,y+4),Size=new Size(175,25)});
        input.SetBounds(200,y,375,28); Controls.Add(input);
    }
}
