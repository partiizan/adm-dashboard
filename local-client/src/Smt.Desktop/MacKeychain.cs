using System.Runtime.InteropServices;
using System.Text;
using Smt.Core;
namespace Smt.Desktop;

// Native Security.framework APIs keep refresh tokens out of files, command lines and logs.
public sealed class MacKeychain : ITokenVault
{
    const string Framework="/System/Library/Frameworks/Security.framework/Security";
    static readonly byte[] Service=Encoding.UTF8.GetBytes("local.smt.macbeta.eve-sso");
    [DllImport(Framework)]static extern int SecKeychainFindGenericPassword(IntPtr keychain,uint serviceLength,byte[] service,uint accountLength,byte[] account,out uint length,out IntPtr data,out IntPtr item);
    [DllImport(Framework)]static extern int SecKeychainAddGenericPassword(IntPtr keychain,uint serviceLength,byte[] service,uint accountLength,byte[] account,uint length,byte[] data,out IntPtr item);
    [DllImport(Framework)]static extern int SecKeychainItemModifyAttributesAndData(IntPtr item,IntPtr attrs,uint length,byte[] data);
    [DllImport(Framework)]static extern int SecKeychainItemDelete(IntPtr item);
    [DllImport(Framework)]static extern int SecKeychainItemFreeContent(IntPtr attrs,IntPtr data);
    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]static extern void CFRelease(IntPtr item);
    static void Check(int status){if(status!=0)throw new IOException($"macOS Keychain returned {status}. Allow access or unlock your login Keychain.");}
    static void RequireMac(){if(!OperatingSystem.IsMacOS())throw new PlatformNotSupportedException("Persistent login requires macOS Keychain.");}
    public string? Read(string key)
    {
        RequireMac();var account=Encoding.UTF8.GetBytes(key);var status=SecKeychainFindGenericPassword(IntPtr.Zero,(uint)Service.Length,Service,(uint)account.Length,account,out var length,out var data,out var item);
        if(status==-25300)return null;Check(status);
        try{var bytes=new byte[length];Marshal.Copy(data,bytes,0,(int)length);try{return Encoding.UTF8.GetString(bytes);}finally{Array.Clear(bytes);}}
        finally{SecKeychainItemFreeContent(IntPtr.Zero,data);if(item!=IntPtr.Zero)CFRelease(item);}
    }
    public void Write(string key,string value)
    {
        RequireMac();var account=Encoding.UTF8.GetBytes(key);var bytes=Encoding.UTF8.GetBytes(value);IntPtr item=IntPtr.Zero;
        try
        {
            var status=SecKeychainFindGenericPassword(IntPtr.Zero,(uint)Service.Length,Service,(uint)account.Length,account,out _,out var data,out item);
            if(status==0){SecKeychainItemFreeContent(IntPtr.Zero,data);Check(SecKeychainItemModifyAttributesAndData(item,IntPtr.Zero,(uint)bytes.Length,bytes));}
            else if(status==-25300)Check(SecKeychainAddGenericPassword(IntPtr.Zero,(uint)Service.Length,Service,(uint)account.Length,account,(uint)bytes.Length,bytes,out item));
            else Check(status);
        }
        finally{Array.Clear(bytes);if(item!=IntPtr.Zero)CFRelease(item);}
    }
    public void Delete(string key)
    {
        RequireMac();var account=Encoding.UTF8.GetBytes(key);var status=SecKeychainFindGenericPassword(IntPtr.Zero,(uint)Service.Length,Service,(uint)account.Length,account,out _,out var data,out var item);
        if(status==-25300)return;Check(status);try{SecKeychainItemFreeContent(IntPtr.Zero,data);Check(SecKeychainItemDelete(item));}finally{if(item!=IntPtr.Zero)CFRelease(item);}
    }
}
