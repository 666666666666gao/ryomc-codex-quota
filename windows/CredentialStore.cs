using System;
using System.Text;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;

// Only this application's exact Generic Credential is read; never enumerate Codex credentials.
public static class CredentialStore {
    public const string Target = "RyomcQuota/ManualConnection/v1";
    [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)] struct Credential {
        public uint Flags, Type;
        public string TargetName, Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist, AttributeCount;
        public IntPtr Attributes;
        public string TargetAlias, UserName;
    }
    [DllImport("advapi32.dll", EntryPoint="CredWriteW", CharSet=CharSet.Unicode, SetLastError=true)] static extern bool Write(ref Credential c, uint flags);
    [DllImport("advapi32.dll", EntryPoint="CredReadW", CharSet=CharSet.Unicode, SetLastError=true)] static extern bool Read(string target, uint type, uint flags, out IntPtr p);
    [DllImport("advapi32.dll", EntryPoint="CredDeleteW", CharSet=CharSet.Unicode, SetLastError=true)] static extern bool Delete(string target, uint type, uint flags);
    [DllImport("advapi32.dll")] static extern void CredFree(IntPtr p);
    public static OverlayConfig Load(string target=Target) {
        IntPtr p;
        if(!Read(target,1,0,out p)) {
            if(Marshal.GetLastWin32Error()==1168) return null;
            throw new ConnectionProblem("无法读取本插件的 Windows 凭据");
        }
        try {
            var c=(Credential)Marshal.PtrToStructure(p,typeof(Credential));
            var value=Marshal.PtrToStringUni(c.CredentialBlob,(int)c.CredentialBlobSize/2);
            return new JavaScriptSerializer().Deserialize<OverlayConfig>(value);
        } finally {CredFree(p);}
    }
    public static void Save(OverlayConfig connection,string target=Target) {
        CodexConnection.Validate(connection);
        var value=new JavaScriptSerializer().Serialize(connection);
        if(Encoding.Unicode.GetByteCount(value)>2560) throw new ConnectionProblem("连接信息过长，无法保存到 Windows 凭据");
        var blob=Marshal.StringToCoTaskMemUni(value);
        try {
            var c=new Credential {Type=1,TargetName=target,UserName="RyomcQuota",CredentialBlob=blob,CredentialBlobSize=(uint)Encoding.Unicode.GetByteCount(value),Persist=2};
            if(!Write(ref c,0)) throw new ConnectionProblem("保存 Windows 凭据失败，未启用独立连接");
        } finally {Marshal.ZeroFreeCoTaskMemUnicode(blob);}
    }
    public static void Remove(string target=Target) {
        if(!Delete(target,1,0) && Marshal.GetLastWin32Error()!=1168) throw new ConnectionProblem("无法移除本插件保存的连接");
    }
}
