using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Drawing;
using System.Windows.Forms;

public class FixtureHandler : HttpMessageHandler {
    public HttpStatusCode Status=HttpStatusCode.OK;
    public string Body="{\"remainingPercent\":92,\"resetAt\":null,\"queriedAt\":\"2026-09-13T12:00:00Z\",\"windowSeconds\":604800}";
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r,CancellationToken c) {
        if(r.RequestUri.AbsoluteUri!="https://example.test/v1/quota/weekly" || r.Headers.Authorization.Parameter!="fixture-key") throw new Exception("request differs");
        return Task.FromResult(new HttpResponseMessage(Status) {Content=new StringContent(Body)});
    }
}
public class ConnectionTests {
    [STAThread] public static void Main() {
        var name="RyomcQuota/Test/"+Guid.NewGuid().ToString("N");
        var cfg=CodexConnection.FromInput("https://example.test/v1","gpt-6-astra","fixture-key");
        try {
            if(CredentialStore.Load(name)!=null) throw new Exception("test target collision");
            CredentialStore.Save(cfg,name);
            var read=CredentialStore.Load(name);
            if(read.readToken!="fixture-key" || read.endpoint!=cfg.endpoint || read.model!=cfg.model) throw new Exception("credential roundtrip");
            CredentialStore.Remove(name);
            if(CredentialStore.Load(name)!=null) throw new Exception("credential not removed");
        } finally {CredentialStore.Remove(name);}
        var handler=new FixtureHandler();
        using(var client=new HttpClient(handler)) {
            if(QuotaClient.Query(client,cfg).GetAwaiter().GetResult().remainingPercent!=92) throw new Exception("quota parsing");
            foreach(var code in new[]{401,403,404,409,503}) {
                handler.Status=(HttpStatusCode)code; handler.Body="do-not-expose-secret";
                bool failed=false;
                try {QuotaClient.Query(client,cfg).GetAwaiter().GetResult();}
                catch(ConnectionProblem e) {failed=e.Message.Contains(code.ToString()) && !e.Message.Contains(handler.Body);}
                if(!failed) throw new Exception("HTTP error classification");
            }
            handler.Status=HttpStatusCode.OK; handler.Body="{\"remainingPercent\":999}";
            bool rejected=false;
            try {QuotaClient.Query(client,cfg).GetAwaiter().GetResult();} catch(ConnectionProblem) {rejected=true;}
            if(!rejected) throw new Exception("invalid response accepted");
        }
        if(QuotaClient.Error(new HttpRequestException("private-secret"),"query").Contains("private-secret")) throw new Exception("error leaked");
        if(!QuotaClient.Error(new TaskCanceledException(),"query").Contains("超时")) throw new Exception("timeout classification");
        var root=Path.Combine(Path.GetTempPath(),"ryomc-settings-test-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root); Environment.SetEnvironmentVariable("CODEX_HOME",root);
        try {
            File.WriteAllText(Path.Combine(root,"config.toml"),"model_provider='custom'\nmodel='gpt-6-astra'\n[model_providers.custom]\nbase_url='https://example.test/v1'\n");
            Application.EnableVisualStyles();
            using(var form=new ConnectionSettings()) {
                int masked=0;
                foreach(Control c in form.Controls) if(c is TextBox && ((TextBox)c).UseSystemPasswordChar) {masked++; if(c.Text!="") throw new Exception("key prefilled");}
                if(masked!=1) throw new Exception("missing masked input");
                form.Show(); Application.DoEvents();
                using(var image=new Bitmap(form.Width,form.Height)) {form.DrawToBitmap(image,new Rectangle(0,0,form.Width,form.Height)); image.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"settings-preview.png"));}
                form.Close();
            }
            if(File.Exists(Path.Combine(root,"auth.json"))) throw new Exception("Codex auth file created");
        } finally {Directory.Delete(root,true);}
        Console.WriteLine("PASS: Windows credential roundtrip/delete; request and quota parsing; five HTTP errors; invalid response; redaction/timeout; masked settings form; no auth.json creation.");
    }
}
