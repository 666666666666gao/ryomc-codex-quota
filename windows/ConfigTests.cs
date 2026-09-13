using System;
using System.IO;

public class ConfigTests {
    public static void Main() {
        var root=Path.Combine(Path.GetTempPath(),"ryomc-config-tests-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        Environment.SetEnvironmentVariable("CODEX_HOME",root);
        string toml="model_provider='custom'\nmodel='gpt-6-astra'\n[model_providers.\"custom\"]\nbase_url='https://example.test/v1' # comment\n";
        File.WriteAllText(Path.Combine(root,"config.toml"),toml);
        File.WriteAllText(Path.Combine(root,"auth.json"),"{\"OPENAI_API_KEY\":\"fixture-api-key\"}");
        var c=CodexConnection.Load(false);
        if(c.endpoint!="https://example.test/v1/quota/weekly" || c.readToken!="fixture-api-key" || c.model!="gpt-6-astra") throw new Exception("default config");
        File.WriteAllText(Path.Combine(root,"config.toml"),"profile='work'\n"+toml+"[profiles.work]\nmodel='gpt-5.6-sol'\n");
        if(CodexConnection.Load(false).model!="gpt-5.6-sol") throw new Exception("profile config");
        File.WriteAllText(Path.Combine(root,"config.toml"),toml+"env_key='QUOTA_FIXTURE_KEY'\n");
        Environment.SetEnvironmentVariable("QUOTA_FIXTURE_KEY","test-env-key");
        if(CodexConnection.Load(false).readToken!="test-env-key") throw new Exception("env key");
        Environment.SetEnvironmentVariable("QUOTA_FIXTURE_KEY",null);
        bool failed=false;
        try {CodexConnection.Load(false);} catch(FormatException) {failed=true;}
        if(!failed) throw new Exception("must not fall back to auth.json");
        File.WriteAllText(Path.Combine(root,"config.toml"),"model='gpt-6-astra'");
        failed=false;
        try {CodexConnection.Load(false);} catch(FormatException) {failed=true;}
        if(!failed) throw new Exception("must not assume relay for OAuth mode");
        // Exact unique directory created above, no user configuration touched.
        Directory.Delete(root,true);
        Console.WriteLine("Windows config tests: 5 passed");
    }
}
