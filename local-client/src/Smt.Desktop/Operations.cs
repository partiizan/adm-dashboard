using System.Diagnostics;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Smt.Core;
namespace Smt.Desktop;
public sealed record PilotItem(Pilot Pilot,string System,string Ship){public override string ToString()=>$"{Pilot.Name}\n{System} · {Ship}\n{Pilot.Status} · {Pilot.Checked:HH:mm:ss} UTC";}
public sealed record LiveItem(string System,string Label,long Id=0){public override string ToString()=>Label;}
public partial class MainWindow
{
    private readonly LiveApi liveApi=new();
    private CharacterService characters=null!;
    private Situational situation=null!;
    private readonly DispatcherTimer operationTimer=new(){Interval=TimeSpan.FromSeconds(6)};
    private readonly Dictionary<long,Pilot> pilots=[];
    private readonly Dictionary<long,List<Pilot>> fleets=[];
    private readonly Dictionary<long,string> fleetStates=[];
    private readonly Dictionary<long,string> names=[];
    private readonly Dictionary<long,string> ships=[];
    private Bridge[] bridges=[];
    private bool liveBusy,characterBusy,routeBusy,refreshingPilotRows;
    private CancellationTokenSource? loginCancel;
    private long? activeCharacter;
    private static void Browse(string url)=>Process.Start(new ProcessStartInfo(url){UseShellExecute=true});
    private void InitializeOperations()
    {
        characters=new(liveApi,new MacKeychain(),SettingsStore.Folder);characters.Load();situation=new(liveApi,universe);
        Get<TextBox>("ClientIdBox").Text=settings.ClientId;
        Get<ComboBox>("RouteMode").ItemsSource=new[]{"Stargates", "Gates + Ansiblex", "Capital jumps"};Get<ComboBox>("RouteMode").SelectedIndex=0;
        Get<ComboBox>("ShipPicker").ItemsSource=Navigation.Ships;Get<ComboBox>("ShipPicker").SelectedIndex=0;
        Get<ComboBox>("RouteMode").SelectionChanged+=(_,_)=>UpdateRange();Get<ComboBox>("ShipPicker").SelectionChanged+=(_,_)=>UpdateRange();Get<NumericUpDown>("Calibration").ValueChanged+=(_,_)=>UpdateRange();UpdateRange();
        Get<ComboBox>("ActivityLayer").ItemsSource=new[]{"Activity layer off","Ship kills · last hour","Pod kills · last hour","NPC kills · last hour","Ship jumps · last hour"};Get<ComboBox>("ActivityLayer").SelectedIndex=0;
        Get<ComboBox>("ActivityLayer").SelectionChanged+=(_,_)=>ApplyLive();
        foreach(var n in new[]{"WormholeLayer","StormLayer","KillLayer"})Get<CheckBox>(n).IsCheckedChanged+=(_,_)=>ApplyLive();
        Get<Button>("ImportBridges").Click+=async(_,_)=>await ImportBridges();
        Get<Button>("ClearBridges").Click+=(_,_)=>{try{PersistBridges([]);bridges=[];UpdateBridges();}catch(Exception e){SetStatus(e.Message);}};
        try{var path=Path.Combine(SettingsStore.Folder,"ansiblex.csv");if(File.Exists(path))bridges=Navigation.ParseBridges(File.ReadAllText(path),universe);}catch(Exception e){SetStatus("Ansiblex import unavailable: "+e.Message);}UpdateBridges();
        Get<Button>("SsoHelp").Click+=async(_,_)=>
        {
            var dialog=new Window{Title="EVE SSO setup",Width=650,Height=440,Content=new TextBox{Text="Register a native / PKCE application at https://developers.eveonline.com/applications\n\nCallback URL (exact):\n"+CharacterService.Callback+"\n\nEnable these scopes:\n"+CharacterService.Scopes.Replace(" ","\n")+"\n\nCopy its public client ID into the Pilots tab. No client secret is used. Your browser handles EVE login; refresh tokens are stored in macOS Keychain. Repeat Log in for each character. Disconnect removes local access; revoke access on EVE's Authorized Applications page if required.",IsReadOnly=true,TextWrapping=Avalonia.Media.TextWrapping.Wrap,Margin=new(20)}};
            await dialog.ShowDialog(this);
        };
        Get<Button>("LoginCharacter").Click+=async(_,_)=>await Login();
        Get<Button>("CancelLogin").Click+=(_,_)=>loginCancel?.Cancel();
        Get<Button>("RemoveCharacter").Click+=(_,_)=>
        {
            if(Get<ListBox>("CharacterList").SelectedItem is not PilotItem item)return;
            var c=characters.Characters.FirstOrDefault(c=>c.Id==item.Pilot.Id);if(c==null)return;
            try{characters.Remove(c);pilots.Remove(c.Id);fleets.Remove(c.Id);fleetStates.Remove(c.Id);activeCharacter=null;RefreshPilots();ApplyLive();}catch(Exception e){Get<TextBlock>("CharacterStatus").Text=e.Message;}
        };
        Get<ListBox>("CharacterList").SelectionChanged+=(_,_)=>{if(Get<ListBox>("CharacterList").SelectedItem is PilotItem i){activeCharacter=i.Pilot.Id;if(!refreshingPilotRows)FocusPilot(i.Pilot);RefreshFleet();}};
        Get<ListBox>("FleetList").SelectionChanged+=(_,_)=>{if(Get<ListBox>("FleetList").SelectedItem is PilotItem i)FocusPilot(i.Pilot);};
        foreach(var name in new[]{"KillList","WormholeList","StormList"})Get<ListBox>(name).SelectionChanged+=(_,_)=>{if(Get<ListBox>(name).SelectedItem is LiveItem i)SelectSystem(i.System);};
        Get<ListBox>("KillList").DoubleTapped+=(_,_)=>{if(Get<ListBox>("KillList").SelectedItem is LiveItem {Id:>0} i)Browse($"https://zkillboard.com/kill/{i.Id}/");};
        Get<ComboBox>("RegionPicker").SelectionChanged+=(_,_)=>ApplyLive();
        operationTimer.Tick+=(_,_)=>{_ = RefreshLive();_ = RefreshCharacters();};
        Opened+=(_,_)=>{operationTimer.Start();_ = RefreshLive();_ = RefreshCharacters();};RefreshPilots();
    }
    private void StopOperations(){operationTimer.Stop();loginCancel?.Cancel();liveApi.Dispose();}
    private void UpdateRange()
    {
        bool capital=Get<ComboBox>("RouteMode").SelectedIndex==2;
        Get<ComboBox>("ShipPicker").IsVisible=capital;Get<NumericUpDown>("Calibration").IsVisible=capital;Get<TextBlock>("JumpRangeLabel").IsVisible=capital;
        Get<CheckBox>("HighSecOnly").IsEnabled=!capital;
        Get<TextBlock>("JumpRangeLabel").Text=$"Maximum {Navigation.Range(Math.Max(0,Get<ComboBox>("ShipPicker").SelectedIndex),(int)(Get<NumericUpDown>("Calibration").Value??5)):0.0} LY per jump";
    }
    private static string[] Names(string? value)=>(value??"").Split(',',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries);
    private async void Plan()
    {
        if(routeBusy)return;routeBusy=true;Get<Button>("PlanRoute").IsEnabled=false;
        try
        {
            var from=Get<TextBox>("FromBox").Text??"";var to=Get<TextBox>("ToBox").Text??"";
            var waypoints=Names(Get<TextBox>("WaypointsBox").Text);var avoid=new HashSet<string>(Names(Get<TextBox>("AvoidBox").Text),StringComparer.OrdinalIgnoreCase);
            int mode=Get<ComboBox>("RouteMode").SelectedIndex;bool high=mode!=2 && Get<CheckBox>("HighSecOnly").IsChecked==true;
            double? range=mode==2?Navigation.Range(Get<ComboBox>("ShipPicker").SelectedIndex,(int)(Get<NumericUpDown>("Calibration").Value??5)):null;
            var network=mode==1?bridges:[];var highsecStart=Get<ComboBox>("ShipPicker").SelectedIndex is 2 or 4;
            var route=await Task.Run(()=>Navigation.Plan(universe,from,to,waypoints,avoid,high,network,range,highsecStart));
            if(closing.IsCancellationRequested)return;
            Map.Route=route.Systems;Map.RouteLegs=route.Legs;
            Get<ListBox>("RouteList").ItemsSource=route.Systems.Select((s,i)=>new LiveItem(s.Name,i==0?$"START  {s.Name}":$"{i:00}  {s.Name}\n{route.Legs[i-1].Kind}"+(mode==2?$" · {route.Legs[i-1].LightYears:0.00} LY":""))).ToArray();
            Get<TextBlock>("RouteSummary").Text=route.Systems.Count==0?"No route found with these restrictions.":$"{route.Legs.Count} hops · {route.Legs.Count(l=>l.Kind=="Ansiblex")} Ansiblex"+(mode==2?$"\n{route.Legs.Sum(l=>l.LightYears):0.00} LY total. Cynos, fuel, fatigue and access must be checked in game.":"\nBridge access, fuel and online status are not verified.");
            Map.InvalidateVisual();if(route.Systems.Count>0)SelectSystem(route.Systems[0].Name);
        }
        catch(Exception e){SetStatus("Route: "+e.Message);}
        finally{routeBusy=false;Get<Button>("PlanRoute").IsEnabled=true;}
    }
    private void PersistBridges(Bridge[] value)
    {
        Directory.CreateDirectory(SettingsStore.Folder);var path=Path.Combine(SettingsStore.Folder,"ansiblex.csv");File.WriteAllText(path+".tmp",string.Join('\n',value.Select(b=>b.From+","+b.To)));File.Move(path+".tmp",path,true);
    }
    private async Task ImportBridges()
    {
        try
        {
            var files=await StorageProvider.OpenFilePickerAsync(new(){Title="Ansiblex CSV: FromSystem,ToSystem (one bidirectional link per line)",AllowMultiple=false});
            var path=files.FirstOrDefault()?.TryGetLocalPath();if(path==null)return;
            var parsed=Navigation.ParseBridges(await File.ReadAllTextAsync(path),universe);PersistBridges(parsed);bridges=parsed;UpdateBridges();
        }
        catch(Exception e){Get<TextBlock>("BridgeStatus").Text="Import rejected: "+e.Message;}
    }
    private void UpdateBridges(){Get<TextBlock>("BridgeStatus").Text=$"{bridges.Length} imported bidirectional links · access unverified";Map.Bridges=bridges;Map.InvalidateVisual();}
    private async Task Login()
    {
        if(loginCancel!=null)return;
        loginCancel=CancellationTokenSource.CreateLinkedTokenSource(closing.Token);Get<Button>("LoginCharacter").IsEnabled=false;Get<Button>("CancelLogin").IsEnabled=true;
        try
        {
            settings=settings with{ClientId=Get<TextBox>("ClientIdBox").Text?.Trim()??""};Save();Get<TextBlock>("CharacterStatus").Text="Complete login in your browser. Waiting up to 4 minutes…";
            await characters.Login(settings.ClientId,Browse,loginCancel.Token);RefreshPilots();Get<TextBlock>("CharacterStatus").Text="Character connected. Updating location…";await RefreshCharacters();
        }
        catch(OperationCanceledException){Get<TextBlock>("CharacterStatus").Text="Login cancelled or timed out. You can try again.";}
        catch(Exception e){Get<TextBlock>("CharacterStatus").Text="Login: "+e.Message;}
        finally{loginCancel.Dispose();loginCancel=null;Get<Button>("LoginCharacter").IsEnabled=true;Get<Button>("CancelLogin").IsEnabled=false;}
    }
    private async Task RefreshCharacters()
    {
        if(characterBusy || closing.IsCancellationRequested || characters.Characters.Count==0)return;characterBusy=true;
        var errors=new List<string>();
        try
        {
            foreach(var c in characters.Characters.ToArray())
            {
                try
                {
                    var result=await characters.Poll(c,closing.Token);if(!characters.Characters.Contains(c))continue;
                    pilots[c.Id]=result.Pilot;fleets[c.Id]=result.Fleet;fleetStates[c.Id]=result.FleetStatus;
                    // Resolve public labels once; failures retain numeric IDs and do not hide positions.
                    foreach(var pilot in result.Fleet.Prepend(result.Pilot).Where(p=>!ships.ContainsKey(p.ShipType) || p.Fleet && !names.ContainsKey(p.Id)).Take(20))
                    {
                        if(!ships.ContainsKey(pilot.ShipType))try{ships[pilot.ShipType]=(await liveApi.Json($"https://esi.evetech.net/latest/universe/types/{pilot.ShipType}/",closing.Token,cacheSeconds:86400)).Str("name");}catch(HttpRequestException){}
                        if(pilot.Fleet && !names.ContainsKey(pilot.Id))try{names[pilot.Id]=(await liveApi.Json($"https://esi.evetech.net/latest/characters/{pilot.Id}/",closing.Token,cacheSeconds:86400)).Str("name");}catch(HttpRequestException){}
                    }
                }
                catch(Exception e) when(e is not OutOfMemoryException){errors.Add(c.Name+": "+e.Message);if(pilots.TryGetValue(c.Id,out var old))pilots[c.Id]=old with{Status="STALE · refresh failed"};fleets.Remove(c.Id);fleetStates[c.Id]="Fleet data unavailable";}
            }
            if(closing.IsCancellationRequested)return;
            RefreshPilots();ApplyLive();Get<TextBlock>("CharacterStatus").Text=errors.Count>0?string.Join('\n',errors):$"{characters.Characters.Count} characters · checked {DateTimeOffset.UtcNow:HH:mm:ss} UTC";
            if(Get<CheckBox>("FollowCharacter").IsChecked==true && activeCharacter is {} id && pilots.TryGetValue(id,out var follow))FocusPilot(follow);
        }
        finally{characterBusy=false;}
    }
    private PilotItem PilotRow(Pilot p)=>new(p with{Name=names.GetValueOrDefault(p.Id,p.Name)},universe.ById.TryGetValue(p.SystemId,out var s)?s.Name:p.SystemId==0?"Location unavailable":$"System {p.SystemId}",ships.GetValueOrDefault(p.ShipType,$"Type {p.ShipType}"));
    private void RefreshPilots()
    {
        var rows=characters.Characters.Select(c=>PilotRow(pilots.GetValueOrDefault(c.Id)??new(c.Id,c.Name,0,0,false,DateTimeOffset.MinValue,"Awaiting ESI"))).ToArray();
        refreshingPilotRows=true;var keep=activeCharacter;Get<ListBox>("CharacterList").ItemsSource=rows;if(keep!=null)Get<ListBox>("CharacterList").SelectedItem=rows.FirstOrDefault(r=>r.Pilot.Id==keep);refreshingPilotRows=false;RefreshFleet();
    }
    private void RefreshFleet()
    {
        var id=activeCharacter??characters.Characters.FirstOrDefault()?.Id;
        Get<ListBox>("FleetList").ItemsSource=id is {} i && fleets.TryGetValue(i,out var f)?f.Select(PilotRow).ToArray():[];
        Get<TextBlock>("FleetStatus").Text=id is {} key?fleetStates.GetValueOrDefault(key,"Waiting for fleet data"):"Connect a character to check fleet access.";
    }
    private void FocusPilot(Pilot p){if(universe.ById.TryGetValue(p.SystemId,out var s))SelectSystem(s.Name);}
    private async Task RefreshLive()
    {
        if(liveBusy || closing.IsCancellationRequested)return;liveBusy=true;
        try{await situation.Refresh(closing.Token);if(!closing.IsCancellationRequested)ApplyLive();}
        catch(Exception e){if(!closing.IsCancellationRequested)Get<TextBlock>("FeedStatus").Text="Feed refresh: "+e.Message;}
        finally{liveBusy=false;}
    }
    private void ApplyLive()
    {
        if(situation==null)return;
        Map.Activity=situation.Activity;Map.ActivityMode=Get<ComboBox>("ActivityLayer").SelectedIndex;
        Map.Wormholes=Get<CheckBox>("WormholeLayer").IsChecked==true?situation.Wormholes:[];
        Map.StormAreas=Get<CheckBox>("StormLayer").IsChecked==true?situation.StormAreas():new();
        Map.Kills=Get<CheckBox>("KillLayer").IsChecked==true?situation.Kills:[];
        Map.Pilots=pilots.Values.Concat(fleets.Values.SelectMany(f=>f)).Where(p=>DateTimeOffset.UtcNow-p.Checked<TimeSpan.FromMinutes(2)).GroupBy(p=>p.Id).Select(g=>g.First()).ToArray();
        var region=(Get<ComboBox>("RegionPicker").SelectedItem as Region)?.Name;
        Get<ListBox>("KillList").ItemsSource=situation.Kills.Where(k=>universe.ById.TryGetValue(k.SystemId,out var s) && s.Region==region).Select(k=>new LiveItem(universe.ById[k.SystemId].Name,$"{universe.ById[k.SystemId].Name} · {k.Time:HH:mm} UTC\n{ships.GetValueOrDefault(k.ShipType,$"Type {k.ShipType}")} · {k.Value:N0} ISK",k.Id)).ToArray();
        Get<ListBox>("WormholeList").ItemsSource=situation.Wormholes.Select(w=>new LiveItem(w.System,$"{w.System} ↔ {w.Hub}\n{w.InSignature} / {w.OutSignature} · {w.Size}\nExpires {w.Expires:MMM d HH:mm} UTC")).ToArray();
        Get<ListBox>("StormList").ItemsSource=situation.Storms.Select(s=>new LiveItem(s.System,$"{s.System} · {s.Type}\n{s.Name}")).ToArray();
        Get<TextBlock>("FeedStatus").Text=string.Join('\n',situation.Status.Values);Map.InvalidateVisual();UpdateOperationalDetails();
    }
    private void UpdateOperationalDetails()
    {
        if(selected==null || situation==null)return;var sys=universe.Systems[selected];
        Get<TextBlock>("SystemInfo").Text=$"{sys.Region}\nSecurity {sys.Security:0.00} · {sys.Jumps.Length} gates"+(sys.Station?"\nNPC station present":"")+(situation.Activity.TryGetValue(sys.Id,out var a)?$"\nLast hour: {a.Ships} ships · {a.Pods} pods\n{a.Npcs} NPCs · {a.Jumps} jumps":"");
    }
}
