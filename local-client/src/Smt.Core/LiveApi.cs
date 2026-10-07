using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
namespace Smt.Core;

// All callers share server cache and backoff state. Failed refreshes never masquerade as fresh data.
public sealed class LiveApi : IDisposable
{
    private readonly HttpClient http;
    private readonly Dictionary<string,(string Body,DateTimeOffset Until)> cache=[];
    private readonly Dictionary<string,DateTimeOffset> backoff=[];
    public LiveApi(HttpMessageHandler? handler=null)
    {
        http=handler==null?new():new(handler);http.Timeout=TimeSpan.FromSeconds(20);
        http.DefaultRequestHeaders.UserAgent.ParseAdd("SMT-Mac-Beta/0.6 (+https://github.com/partiizan/adm-dashboard)");
    }
    public async Task<JsonElement> Json(string url,CancellationToken ct,string? token=null,int cacheSeconds=0)
        => JsonDocument.Parse(await Text(url,ct,token,cacheSeconds)).RootElement.Clone();
    public async Task<string> Text(string url,CancellationToken ct,string? token=null,int cacheSeconds=0)
    {
        var host=new Uri(url).Host; var key=url+"|"+(token==null?"public":Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token))));
        if(cache.TryGetValue(key,out var hit) && hit.Until>DateTimeOffset.UtcNow)return hit.Body;
        if(backoff.TryGetValue(host,out var until) && until>DateTimeOffset.UtcNow)throw new HttpRequestException($"{host}: retry after {until:HH:mm:ss} UTC.");
        using var req=new HttpRequestMessage(HttpMethod.Get,url);
        if(token!=null)req.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token);
        using var response=await http.SendAsync(req,ct);
        if(response.StatusCode==(HttpStatusCode)429 || response.StatusCode==(HttpStatusCode)420 || response.StatusCode==HttpStatusCode.ServiceUnavailable)
            backoff[host]=response.Headers.RetryAfter?.Date ?? DateTimeOffset.UtcNow+(response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(60));
        response.EnsureSuccessStatusCode();
        var body=await response.Content.ReadAsStringAsync(ct);
        var seconds=Math.Max(cacheSeconds,(response.Content.Headers.Expires-DateTimeOffset.UtcNow)?.TotalSeconds ?? response.Headers.CacheControl?.MaxAge?.TotalSeconds ?? 0);
        if(seconds>0)cache[key]=(body,DateTimeOffset.UtcNow.AddSeconds(seconds));
        return body;
    }
    public async Task<JsonElement> Token(Dictionary<string,string> values,CancellationToken ct)
    {
        using var response=await http.PostAsync("https://login.eveonline.com/v2/oauth/token",new FormUrlEncodedContent(values),ct);
        if(!response.IsSuccessStatusCode) throw new HttpRequestException($"EVE login returned HTTP {(int)response.StatusCode}. Check the client ID, callback and registered scopes; reconnect if access was revoked.");
        using var doc=JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));return doc.RootElement.Clone();
    }
    public void Dispose()=>http.Dispose();
}
public static class JsonFields
{
    public static long Number(this JsonElement e,string key)=>e.TryGetProperty(key,out var v) && v.ValueKind==JsonValueKind.Number?v.GetInt64():0;
    public static string Str(this JsonElement e,string key)=>e.TryGetProperty(key,out var v)?v.ToString():"";
}
