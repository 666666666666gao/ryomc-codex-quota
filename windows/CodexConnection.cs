using System;
using System.IO;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using Tommy;

public class OverlayConfig {
    public string endpoint;
    public string readToken;
    public string model;
    public string envKey;
}

public class ConnectionProblem : Exception { public ConnectionProblem(string message) : base(message) {} }

public static class CodexConnection {
    static string StringValue(TomlNode table, string key) {
        if(!table.HasKey(key)) return null;
        if(!table[key].IsString) throw new FormatException();
        return table[key].AsString.Value;
    }
    public static string ConfigRoot() {
        var root=Environment.GetEnvironmentVariable("CODEX_HOME");
        return String.IsNullOrEmpty(root) ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),".codex") : root;
    }
    public static OverlayConfig Load(bool shared) {
        var home=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var json=new JavaScriptSerializer();
        if(shared) {
            var c=json.Deserialize<OverlayConfig>(File.ReadAllText(Path.Combine(home,".config","ryomc-codex-quota","reader.json")));
            Validate(c);
            return c;
        }
        var result=ReadSettings();
        string key;
        if(result.envKey!=null) {
            key=Environment.GetEnvironmentVariable(result.envKey);
            if(String.IsNullOrWhiteSpace(key)) throw new ConnectionProblem("指定的密钥环境变量为空；请点连接设置");
        } else {
            var path=Path.Combine(ConfigRoot(),"auth.json");
            if(!File.Exists(path)) throw new ConnectionProblem("已读取站点配置，但缺少 auth.json；请点连接设置");
            var auth=json.Deserialize<Dictionary<string,object>>(File.ReadAllText(path));
            key=auth.ContainsKey("OPENAI_API_KEY") ? auth["OPENAI_API_KEY"] as string : null;
            if(String.IsNullOrWhiteSpace(key)) throw new ConnectionProblem("auth.json 中没有 API 密钥；请点连接设置");
        }
        result.readToken=key.Trim();
        Validate(result);
        return result;
    }
    // Read non-secret settings separately so the form can prefill even when auth.json is absent.
    public static OverlayConfig ReadSettings() {
        var root=ConfigRoot();
        if(!File.Exists(Path.Combine(root,"config.toml"))) throw new ConnectionProblem("未找到 config.toml；请点连接设置");
        TomlTable config;
        using(var reader=File.OpenText(Path.Combine(root,"config.toml"))) config=TOML.Parse(reader);
        var profileName=StringValue(config,"profile");
        TomlNode profile=new TomlTable();
        if(profileName!=null) {
            if(!config.HasKey("profiles") || !config["profiles"].HasKey(profileName)) throw new FormatException();
            profile=config["profiles"][profileName];
        }
        var providerName=StringValue(profile,"model_provider") ?? StringValue(config,"model_provider");
        if(providerName==null || !config.HasKey("model_providers") || !config["model_providers"].HasKey(providerName)) throw new ConnectionProblem("未配置中转提供方；请点连接设置");
        var provider=config["model_providers"][providerName];
        var baseUrl=StringValue(provider,"base_url");
        var envKey=StringValue(provider,"env_key");
        return FromInput(baseUrl,StringValue(profile,"model") ?? StringValue(config,"model"),null,envKey);
    }
    public static OverlayConfig FromInput(string baseUrl,string model,string key,string envKey=null) {
        ValidateUrl(baseUrl);
        if(String.IsNullOrWhiteSpace(model)) throw new ConnectionProblem("未填写模型名称");
        return new OverlayConfig {endpoint=new Uri(baseUrl).AbsoluteUri.TrimEnd('/')+"/quota/weekly",model=model.Trim(),readToken=key==null ? null : key.Trim(),envKey=envKey};
    }
    static void ValidateUrl(string url) {
        Uri u;
        if(!Uri.TryCreate(url,UriKind.Absolute,out u) || u.Scheme!="https" || !String.IsNullOrEmpty(u.UserInfo) || !String.IsNullOrEmpty(u.Query) || !String.IsNullOrEmpty(u.Fragment)) throw new ConnectionProblem("Base URL 必须是无凭据和查询参数的 HTTPS 地址");
    }
    public static void Validate(OverlayConfig c) {
        if(c==null) throw new ConnectionProblem("没有连接配置");
        ValidateUrl(c.endpoint);
        if(String.IsNullOrWhiteSpace(c.readToken)) throw new ConnectionProblem("未填写 API 密钥");
    }
}
