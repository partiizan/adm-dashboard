using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace Smt.Core;

public interface ITokenVault { string? Read(string key); void Write(string key,string value); void Delete(string key); }
public sealed record CharacterIdentity(long Id,string Name,string ClientId);
public sealed record Pilot(long Id,string Name,long SystemId,long ShipType,bool Fleet,DateTimeOffset Checked,string Status);
public sealed class CharacterService(LiveApi api,ITokenVault vault,string folder)
{
    public const string Callback="http://localhost:17386/callback/";
    public const string Scopes="esi-location.read_location.v1 esi-location.read_ship_type.v1 esi-location.read_online.v1 esi-fleets.read_fleet.v1";
    public List<CharacterIdentity> Characters {get; private set;}=[];
    private readonly Dictionary<long,(string Access,DateTimeOffset Expiry)> access=[];
    public void Load()
    {
        try {Characters=JsonSerializer.Deserialize<List<CharacterIdentity>>(File.ReadAllText(Path.Combine(folder,"characters.json")))??[];}
        catch(Exception e) when(e is IOException or JsonException) {Characters=[];}
    }
    private void Save() {Directory.CreateDirectory(folder);var path=Path.Combine(folder,"characters.json");File.WriteAllText(path+".tmp",JsonSerializer.Serialize(Characters));File.Move(path+".tmp",path,true);}
    public void Remove(CharacterIdentity c){vault.Delete(Key(c));Characters.Remove(c);access.Remove(c.Id);Save();}
    private static string Key(CharacterIdentity c)=>$"{c.ClientId}:{c.Id}";
    public static string Base64Url(byte[] bytes)=>Convert.ToBase64String(bytes).TrimEnd('=').Replace('+','-').Replace('/','_');
    public static string Challenge(string verifier)=>Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
    public static bool ValidState(string? received,string expected)=>received!=null && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(received),Encoding.UTF8.GetBytes(expected));
    public async Task Login(string clientId,Action<string> openBrowser,CancellationToken ct)
    {
        if(string.IsNullOrWhiteSpace(clientId))throw new ArgumentException("Enter a registered EVE application client ID first. See the included SSO setup guide.");
        var verifier=Base64Url(RandomNumberGenerator.GetBytes(32));var state=Base64Url(RandomNumberGenerator.GetBytes(32));
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(TimeSpan.FromMinutes(4));
        using var listener=new HttpListener();listener.Prefixes.Add(Callback);listener.Start();
        var query=new Dictionary<string,string>{{"response_type","code"},{"client_id",clientId},{"redirect_uri",Callback},{"scope",Scopes},{"state",state},{"code_challenge",Challenge(verifier)},{"code_challenge_method","S256"}};
        openBrowser("https://login.eveonline.com/v2/oauth/authorize/?"+string.Join('&',query.Select(k=>$"{k.Key}={Uri.EscapeDataString(k.Value)}")));
        string code;
        while(true)
        {
            var context=await listener.GetContextAsync().WaitAsync(timeout.Token);
            var valid=context.Request.RemoteEndPoint!=null && IPAddress.IsLoopback(context.Request.RemoteEndPoint.Address) && ValidState(context.Request.QueryString["state"],state);
            context.Response.StatusCode=valid?200:400;
            var bytes=Encoding.UTF8.GetBytes(valid?"EVE authorization received. Return to SMT Mac Beta.":"Invalid login state. Return to the app and try again.");
            context.Response.ContentType="text/plain; charset=utf-8";await context.Response.OutputStream.WriteAsync(bytes,timeout.Token);context.Response.Close();
            if(!valid)continue;
            if(context.Request.QueryString["error"]!=null)throw new InvalidOperationException("EVE login was declined.");
            code=context.Request.QueryString["code"]??throw new InvalidOperationException("EVE did not return an authorization code.");break;
        }
        var response=await api.Token(new(){{"grant_type","authorization_code"},{"code",code},{"client_id",clientId},{"code_verifier",verifier},{"redirect_uri",Callback}},timeout.Token);
        var claims=await Validate(response.Str("access_token"),clientId,timeout.Token);
        var identity=new CharacterIdentity(long.Parse(claims.Str("sub").Split(':')[2]),claims.Str("name"),clientId);
        vault.Write(Key(identity),response.Str("refresh_token"));
        access[identity.Id]=(response.Str("access_token"),DateTimeOffset.FromUnixTimeSeconds(claims.Number("exp")));
        Characters.RemoveAll(c=>c.Id==identity.Id);Characters.Add(identity);Save();
    }
    private static byte[] Decode(string value)=>Convert.FromBase64String(value.Replace('-','+').Replace('_','/').PadRight((value.Length+3)/4*4,'='));
    public async Task<JsonElement> Validate(string token,string clientId,CancellationToken ct)
    {
        var parts=token.Split('.');if(parts.Length!=3)throw new CryptographicException("Malformed EVE token.");
        using var hd=JsonDocument.Parse(Decode(parts[0]));var header=hd.RootElement;
        if(header.Str("alg")!="RS256")throw new CryptographicException("Unsupported EVE signature algorithm.");
        var metadata=await api.Json("https://login.eveonline.com/.well-known/oauth-authorization-server",ct,cacheSeconds:300);
        var uri=new Uri(metadata.Str("jwks_uri"));if(uri.Scheme!="https" || uri.Host!="login.eveonline.com")throw new CryptographicException("Untrusted EVE signing-key endpoint.");
        var keys=await api.Json(uri.ToString(),ct,cacheSeconds:300);
        var key=keys.GetProperty("keys").EnumerateArray().First(k=>k.Str("kid")==header.Str("kid") && k.Str("kty")=="RSA");
        using var rsa=RSA.Create();rsa.ImportParameters(new RSAParameters{Modulus=Decode(key.Str("n")),Exponent=Decode(key.Str("e"))});
        if(!rsa.VerifyData(Encoding.ASCII.GetBytes(parts[0]+"."+parts[1]),Decode(parts[2]),HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1))throw new CryptographicException("Invalid EVE token signature.");
        using var doc=JsonDocument.Parse(Decode(parts[1]));var c=doc.RootElement;
        var audience=c.GetProperty("aud");var audiences=audience.ValueKind==JsonValueKind.Array?audience.EnumerateArray().Select(x=>x.GetString()).ToArray():[audience.GetString()];
        if(c.Str("iss") is not ("https://login.eveonline.com" or "https://login.eveonline.com/" or "login.eveonline.com") || !audiences.Contains(clientId) || !audiences.Contains("EVE Online") || c.Number("exp")<=DateTimeOffset.UtcNow.ToUnixTimeSeconds() || !c.Str("sub").StartsWith("CHARACTER:EVE:") || !long.TryParse(c.Str("sub")[14..],out _))throw new CryptographicException("EVE token claims failed validation.");
        return c.Clone();
    }
    private async Task<string> Access(CharacterIdentity c,CancellationToken ct)
    {
        if(access.TryGetValue(c.Id,out var token) && token.Expiry>DateTimeOffset.UtcNow.AddMinutes(1))return token.Access;
        var refresh=vault.Read(Key(c));if(string.IsNullOrEmpty(refresh))throw new InvalidOperationException("Reconnect this character: no Keychain token available.");
        var response=await api.Token(new(){{"grant_type","refresh_token"},{"refresh_token",refresh},{"client_id",c.ClientId}},ct);
        var claims=await Validate(response.Str("access_token"),c.ClientId,ct);
        if(claims.Str("sub")!=$"CHARACTER:EVE:{c.Id}")throw new CryptographicException("Refresh returned a different character.");
        if(response.Str("refresh_token") is {Length:>0} next)vault.Write(Key(c),next);
        access[c.Id]=(response.Str("access_token"),DateTimeOffset.FromUnixTimeSeconds(claims.Number("exp")));return response.Str("access_token");
    }
    public async Task<(Pilot Pilot,List<Pilot> Fleet,string FleetStatus)> Poll(CharacterIdentity c,CancellationToken ct)
    {
        var token=await Access(c,ct);var root=$"https://esi.evetech.net/latest/characters/{c.Id}/";
        var loc=await api.Json(root+"location/",ct,token,5);var ship=await api.Json(root+"ship/",ct,token,5);
        var online=await api.Json(root+"online/",ct,token,60);var now=DateTimeOffset.UtcNow;
        var pilot=new Pilot(c.Id,c.Name,loc.Number("solar_system_id"),ship.Number("ship_type_id"),false,now,online.Str("online").Equals("True",StringComparison.OrdinalIgnoreCase)?"Online":"Offline / last location");
        var fleet=new List<Pilot>();string status="Not in a fleet";
        try
        {
            var info=await api.Json(root+"fleet/",ct,token,60);
            var members=await api.Json($"https://esi.evetech.net/latest/fleets/{info.Number("fleet_id")}/members/",ct,token,5);
            foreach(var m in members.EnumerateArray())fleet.Add(new(m.Number("character_id"),$"Pilot {m.Number("character_id")}",m.Number("solar_system_id"),m.Number("ship_type_id"),true,now,m.Str("role")));
            status=$"Fleet {info.Number("fleet_id")} · {fleet.Count} members";
        }
        catch(HttpRequestException e) when(e.StatusCode==HttpStatusCode.NotFound){status="Not in a fleet";}
        catch(HttpRequestException e) when(e.StatusCode==HttpStatusCode.Forbidden){status="Fleet roster requires the fleet boss character / ESI permission.";}
        catch(HttpRequestException){status="Fleet data unavailable; retrying.";}
        return(pilot,fleet,status);
    }
}
