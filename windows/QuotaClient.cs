using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

public static class QuotaClient {
    public static async Task<WeeklyQuota> Query(HttpClient client,OverlayConfig cfg) {
        CodexConnection.Validate(cfg);
        using(var request=new HttpRequestMessage(HttpMethod.Get,cfg.endpoint)) {
            request.Headers.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",cfg.readToken);
            if(cfg.model!=null) request.Headers.Add("X-Quota-Model",cfg.model);
            using(var response=await client.SendAsync(request)) {
                if(!response.IsSuccessStatusCode) {
                    int status=(int)response.StatusCode;
                    string reason;
                    switch(status) {
                        case 401: reason="密钥无效或已禁用"; break;
                        case 403: reason="访问受限，请检查令牌权限或站点防护"; break;
                        case 404: reason="站点没有额度接口或 Base URL 不正确"; break;
                        case 409: reason="无法唯一确认上游账号"; break;
                        case 503: reason="服务端或上游额度暂不可用"; break;
                        default: reason="额度请求失败"; break;
                    }
                    throw new ConnectionProblem("HTTP "+status+"："+reason);
                }
                var q=new JavaScriptSerializer().Deserialize<WeeklyQuota>(await response.Content.ReadAsStringAsync());
                DateTimeOffset parsed;
                if(q==null || q.windowSeconds!=604800 || !q.remainingPercent.HasValue || double.IsNaN(q.remainingPercent.Value) || q.remainingPercent<0 || q.remainingPercent>100 || !DateTimeOffset.TryParse(q.queriedAt,out parsed) || (q.resetAt!=null && !DateTimeOffset.TryParse(q.resetAt,out parsed))) throw new ConnectionProblem("响应不是有效的周额度数据");
                return q;
            }
        }
    }
    public static string Error(Exception e,string stage) {
        if(e is ConnectionProblem) return e.Message;
        if(e is TaskCanceledException) return "请求超时，请检查网络或代理";
        if(e is HttpRequestException) return "连接失败，请检查网络、代理或 HTTPS 证书";
        return stage+"失败（"+e.GetType().Name+"）；未显示原始错误或凭据";
    }
}
