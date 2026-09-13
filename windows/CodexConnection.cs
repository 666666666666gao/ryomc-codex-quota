using System;
using System.IO;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using Tommy;

public class OverlayConfig {
    public string endpoint;
    public string readToken;
    public string model;
}

public static class CodexConnection {
    static string StringValue(TomlNode table, string key) {
        if(!table.HasKey(key)) return null;
        if(!table[key].IsString) throw new FormatException();
        return table[key].AsString.Value;
    }
    public static OverlayConfig Load(bool shared) {
        var home=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var json=new JavaScriptSerializer();
        if(shared) {
            var c=json.Deserialize<OverlayConfig>(File.ReadAllText(Path.Combine(home,".config","ryomc-codex-quota","reader.json")));
            Validate(c);
            return c;
        }
        var root=Environment.GetEnvironmentVariable("CODEX_HOME");
        if(String.IsNullOrEmpty(root)) root=Path.Combine(home,".codex");
        TomlTable config;
        using(var reader=File.OpenText(Path.Combine(root,"config.toml"))) config=TOML.Parse(reader);
        var profileName=StringValue(config,"profile");
        TomlNode profile=new TomlTable();
        if(profileName!=null) {
            if(!config.HasKey("profiles") || !config["profiles"].HasKey(profileName)) throw new FormatException();
            profile=config["profiles"][profileName];
        }
        var providerName=StringValue(profile,"model_provider") ?? StringValue(config,"model_provider");
        if(providerName==null || !config.HasKey("model_providers") || !config["model_providers"].HasKey(providerName)) throw new FormatException();
        var provider=config["model_providers"][providerName];
        var baseUrl=StringValue(provider,"base_url");
        var envKey=StringValue(provider,"env_key");
        string key;
        if(envKey!=null) key=Environment.GetEnvironmentVariable(envKey);
        else {
            var auth=json.Deserialize<Dictionary<string,object>>(File.ReadAllText(Path.Combine(root,"auth.json")));
            key=auth.ContainsKey("OPENAI_API_KEY") ? auth["OPENAI_API_KEY"] as string : null;
        }
        var result=new OverlayConfig {endpoint=baseUrl,readToken=key,model=StringValue(profile,"model") ?? StringValue(config,"model")};
        Validate(result);
        if(String.IsNullOrWhiteSpace(result.model)) throw new FormatException();
        result.endpoint=new Uri(result.endpoint).AbsoluteUri.TrimEnd('/')+"/quota/weekly";
        result.readToken=result.readToken.Trim();
        return result;
    }
    static void Validate(OverlayConfig c) {
        Uri u;
        if(c==null || !Uri.TryCreate(c.endpoint,UriKind.Absolute,out u) || u.Scheme!="https" || !String.IsNullOrEmpty(u.UserInfo) || !String.IsNullOrEmpty(u.Query) || !String.IsNullOrEmpty(u.Fragment) || String.IsNullOrWhiteSpace(c.readToken)) throw new FormatException();
    }
}
