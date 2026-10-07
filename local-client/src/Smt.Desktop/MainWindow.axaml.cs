using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Smt.Core;

namespace Smt.Desktop;

public sealed record SearchItem(StarSystem System)
{
    public override string ToString() => $"{System.Name}  ·  {System.Region}";
}
public sealed record RouteItem(int Index, StarSystem System)
{
    public override string ToString() => $"{Index:00}   {System.Name}   {System.Security:0.0}";
}
public sealed record IntelItem(IntelReport Report)
{
    public string Summary => string.Join(" · ", Report.Systems) + (Report.Clear ? " / CLEAR REPORTED" : " / REPORT");
    public string Body => Report.Message;
    public string Meta => $"{Report.Time:HH:mm:ss} UTC · {Report.Speaker} · {Report.Source}";
}

public partial class MainWindow : Window
{
    private readonly SovereigntyClient sovereignty = new(Path.Combine(SettingsStore.Folder,"sovereignty.json"));
    private readonly CancellationTokenSource closing = new();
    private bool refreshingAdm;
    private readonly Universe universe;
    private readonly IntelParser parser;
    private readonly List<IntelReport> reports = [];
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(2) };
    private Settings settings;
    private LogTailer? tailer;
    private bool polling, paused, demo;
    private string? selected;
    private T Get<T>(string name) where T : Control => this.FindControl<T>(name)!;

    public MainWindow()
    {
        InitializeComponent();
        var dataPath=Path.Combine(AppContext.BaseDirectory,"data","universe.json");
        if(!File.Exists(dataPath)) dataPath=Path.Combine(AppContext.BaseDirectory,"..","Resources","data","universe.json");
        universe = new Universe(dataPath);
        parser = new IntelParser(universe);
        settings = SettingsStore.Load();
        Get<TextBlock>("DataSummary").Text = $"{universe.Systems.Count:N0} systems · {universe.Data.Regions.Length} regional maps · SMT {universe.Data.Commit[..7]}";
        var picker=Get<ComboBox>("RegionPicker"); picker.ItemsSource=universe.Data.Regions.OrderBy(r=>r.Name).ToArray();
        picker.SelectionChanged+=(_,_)=>ChangeRegion();
        picker.SelectedItem=universe.Data.Regions.FirstOrDefault(r=>r.Name==settings.Region) ?? universe.Data.Regions[0];
        Get<TextBox>("ChannelFilter").Text=settings.ChannelFilter;
        Get<CheckBox>("HighSecOnly").IsChecked=settings.HighSecOnly;
        tailer=new LogTailer(LogFolderLocator.Resolve(settings.LogFolder));
        Get<TextBox>("SearchBox").TextChanged+=(_,_)=>
        {
            var items=universe.Search(Get<TextBox>("SearchBox").Text ?? "").Select(s=>new SearchItem(s)).ToArray();
            Get<ListBox>("SearchResults").ItemsSource=items;
            Get<ListBox>("SearchResults").IsVisible=items.Length>0;
        };
        Get<ListBox>("SearchResults").SelectionChanged+=(_,_)=>{ if(Get<ListBox>("SearchResults").SelectedItem is SearchItem i) SelectSystem(i.System.Name); };
        Map.SystemSelected+=name=>SelectSystem(name);
        Get<Button>("FromSelected").Click+=(_,_)=> { if(selected!=null) Get<TextBox>("FromBox").Text=selected; };
        Get<Button>("ToSelected").Click+=(_,_)=> { if(selected!=null) Get<TextBox>("ToBox").Text=selected; };
        Get<Button>("PlanRoute").Click+=(_,_)=>Plan();
        Get<Button>("ClearRoute").Click+=(_,_)=> { Map.Route=[]; Map.RouteLegs=[]; Map.InvalidateVisual(); Get<ListBox>("RouteList").ItemsSource=null; Get<TextBlock>("RouteSummary").Text="Route cleared."; };
        Get<ListBox>("RouteList").SelectionChanged+=(_,_)=> { if(Get<ListBox>("RouteList").SelectedItem is LiveItem i) SelectSystem(i.System); };
        Get<Button>("FitMap").Click+=(_,_)=>Map.Fit();
        Get<Button>("ZoomIn").Click+=(_,_)=>Map.Zoom(1.25);
        Get<Button>("ZoomOut").Click+=(_,_)=>Map.Zoom(.8);
        Get<Button>("ChooseLogs").Click+=async (_,_)=>await ChooseLogFolder();
        Get<Button>("AutoLogs").Click+=async (_,_)=>
        {
            settings=settings with {LogFolder=""};
            if(demo) ToggleDemo();
            tailer=new LogTailer(LogFolderLocator.Resolve());reports.Clear();RefreshIntel();
            paused=false;Get<Button>("ToggleWatch").Content="Pause";Save();UpdateWatchState();await PollLogs();
        };
        Get<Button>("ToggleWatch").Click+=(_,_)=> { paused=!paused; Get<Button>("ToggleWatch").Content=paused?"Resume":"Pause"; UpdateWatchState(); };
        Get<Button>("DemoButton").Click+=(_,_)=>ToggleDemo();
        Get<TextBox>("ChannelFilter").TextChanged+=(_,_)=>
        {
            settings=settings with {ChannelFilter=Get<TextBox>("ChannelFilter").Text ?? ""};
            if(!demo) { reports.Clear(); if(tailer!=null) tailer=new LogTailer(tailer.Folder); RefreshIntel(); }
            Save();
        };
        Get<Button>("AddIntel").Click+=(_,_)=>AddManual();
        Get<TextBox>("ManualIntel").KeyDown+=(_,e)=> { if(e.Key==Avalonia.Input.Key.Enter) AddManual(); };
        Get<ListBox>("IntelList").SelectionChanged+=(_,_)=> { if(Get<ListBox>("IntelList").SelectedItem is IntelItem i) SelectSystem(i.Report.Systems[0]); };
        Get<CheckBox>("ShowAdm").IsCheckedChanged+=(_,_)=> { Map.ShowAdm=Get<CheckBox>("ShowAdm").IsChecked==true; Map.InvalidateVisual(); };
        Get<Button>("RefreshAdm").Click+=async (_,_)=>await UpdateAdmAsync();
        Opened+=async (_,_)=> { await PollLogs(); await UpdateAdmAsync(); };
        timer.Tick+=async (_,_)=> { await PollLogs(); await UpdateAdmAsync(); }; timer.Start();
        ApplyAdm();
        InitializeOperations();
        Closed+=(_,_)=> { timer.Stop(); closing.Cancel(); sovereignty.Dispose(); StopOperations(); Save(); };
        UpdateWatchState(); SetStatus("Ready · Static gate map with public ESI sovereignty data.");
    }
    public async Task UpdateAdmAsync()
    {
        if(refreshingAdm || closing.IsCancellationRequested) return;
        refreshingAdm=true;
        Get<Button>("RefreshAdm").IsEnabled=false;
        try { await sovereignty.RefreshAsync(closing.Token); if(!closing.IsCancellationRequested) ApplyAdm(); }
        finally { refreshingAdm=false; if(!closing.IsCancellationRequested) Get<Button>("RefreshAdm").IsEnabled=true; }
    }
    private void ApplyAdm()
    {
        Map.Sovereignty=sovereignty.Systems; Map.AdmStale=sovereignty.IsStale; Map.InvalidateVisual();
        string checkedText=sovereignty.CheckedAt is {} t ? $"Checked {t:MMM d HH:mm} UTC" : "No ADM data yet";
        Get<TextBlock>("AdmStatus").Text=(sovereignty.Error!=null?"ESI unavailable · ":sovereignty.FromDisk?"Cached · ":"Public ESI · ")+checkedText+(sovereignty.IsStale && sovereignty.CheckedAt!=null?" · * cached/old values":"")+" · 5m refresh";
        ToolTip.SetTip(Get<TextBlock>("AdmStatus"),sovereignty.Error ?? "Activity Defense Multiplier from CCP ESI. N/A = no applicable sovereignty ADM; — = not reported. Refresh respects the server cache.");
        UpdateAdmDetails();
    }
    private void UpdateAdmDetails()
    {
        if(selected==null) { Get<TextBlock>("AdmDetails").Text="Select a system to see ADM."; return; }
        var sys=universe.Systems[selected];
        if(!sovereignty.Systems.TryGetValue(sys.Id,out var sov)) { Get<TextBlock>("AdmDetails").Text="ADM · unavailable / not reported"; return; }
        string number(int? n)=>n?.ToString() ?? "—";
        Get<TextBlock>("AdmDetails").Text=sov.Adm is {} adm
            ? $"ADM {adm:0.0}×"+(sovereignty.IsStale?" · cached/old":"")+$"\nMilitary {number(sov.Military)} · Industry {number(sov.Industrial)}\nStrategic {number(sov.Strategic)}"+(sov.Capital?" · Capital":"")
            : sov.Kind is "Faction" or "Unclaimed" ? "ADM · N/A (no applicable sov ADM)" : "ADM · not reported by ESI";
    }
    private void SetStatus(string text)=>Get<TextBlock>("Status").Text=text;
    private void Save()
    {
        try { SettingsStore.Save(settings); }
        catch(Exception e) when(e is IOException or UnauthorizedAccessException) { SetStatus("Settings could not be saved: "+e.Message); }
    }
    private void ChangeRegion()
    {
        if(Get<ComboBox>("RegionPicker").SelectedItem is not Region region) return;
        Map.SetRegion(universe,region);
        Get<TextBlock>("RegionTitle").Text=region.Name;
        Get<TextBlock>("RegionSubtitle").Text=$"{region.Nodes.Count(n=>!n.Outside)} systems · Stargate network";
        settings=settings with {Region=region.Name}; Save();
    }
    public void SelectSystem(string name)
    {
        if(!universe.Systems.TryGetValue(name,out var sys)) return;
        selected=sys.Name; Map.Selected=sys.Name;
        if(Get<ComboBox>("RegionPicker").SelectedItem is not Region current || !current.Nodes.Any(n=>n.Name==sys.Name))
        {
            var region=universe.Data.Regions.FirstOrDefault(r=>r.Name==sys.Region) ?? universe.Data.Regions.FirstOrDefault(r=>r.Nodes.Any(n=>n.Name==sys.Name));
            if(region!=null) Get<ComboBox>("RegionPicker").SelectedItem=region;
        }
        Get<TextBlock>("SystemName").Text=sys.Name;
        Get<TextBlock>("SystemInfo").Text=$"{sys.Region}\nSecurity {sys.Security:0.00} · {sys.Jumps.Length} gates" + (sys.Station?"\nNPC station present":"");
        UpdateAdmDetails();
        UpdateOperationalDetails();
        Map.InvalidateVisual();
    }
    private async Task ChooseLogFolder()
    {
        try
        {
            var folders=await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions {Title="Choose EVE's Chatlogs folder",AllowMultiple=false});
            var path=folders.FirstOrDefault()?.TryGetLocalPath(); if(path==null) return;
            foreach(var leaf in new[]{"chatlogs","Chatlogs","ChatLogs"}) if(Directory.Exists(Path.Combine(path,leaf))) { path=Path.Combine(path,leaf); break; }
            settings=settings with {LogFolder=path}; tailer=new LogTailer(path); reports.Clear();
            if(demo) ToggleDemo(); paused=false; Get<Button>("ToggleWatch").Content="Pause";
            Save(); UpdateWatchState(); await PollLogs();
        }
        catch(Exception e) { SetStatus("Could not open folder: "+e.Message); }
    }
    private void UpdateWatchState()
    {
        string mode=string.IsNullOrWhiteSpace(settings.LogFolder)?"Automatic":"Custom folder";
        Get<TextBlock>("IntelState").Text=demo?"Demo mode · live monitoring suspended":paused?"Paused · reports still expire":$"{mode} · scanning every 2 seconds\n{tailer?.Folder}";
    }
    private async Task PollLogs()
    {
        if(polling || paused || demo) { Map.InvalidateVisual(); return; }
        var resolved=LogFolderLocator.Resolve(settings.LogFolder);
        if(tailer==null || tailer.Folder!=resolved) tailer=new LogTailer(resolved);
        var current=tailer; var filter=settings.ChannelFilter; polling=true;
        try
        {
            var parsed=await Task.Run(()=>current.Poll()
                .Where(x=>string.IsNullOrWhiteSpace(filter)||x.Source.Contains(filter,StringComparison.OrdinalIgnoreCase))
                .Select(x=>parser.Parse(x.Line,x.Source,DateTimeOffset.UtcNow))
                .Where(r=>r!=null && r.Time>=DateTimeOffset.UtcNow.AddMinutes(-15) && r.Time<=DateTimeOffset.UtcNow.AddMinutes(1)).Cast<IntelReport>().ToArray());
            if(current!=tailer || demo || paused || closing.IsCancellationRequested) return;
            foreach(var r in parsed) AddReport(r);
            RefreshIntel(); UpdateWatchState();
            SetStatus($"Monitoring · checked {DateTime.Now:HH:mm:ss} · {parsed.Length} matching lines this check"+(current.ReadErrors.Count>0?$" · {current.ReadErrors.Count} unreadable file(s), retrying":""));
        }
        catch(DirectoryNotFoundException)
        { Get<TextBlock>("IntelState").Text="Waiting for EVE chat logs · checking every 2 seconds\n"+current.Folder; SetStatus("Open EVE with chat logging enabled. Use Change folder only if your logs are stored elsewhere."); }
        catch(UnauthorizedAccessException)
        { Get<TextBlock>("IntelState").Text="Documents access needed · retrying\n"+current.Folder; SetStatus("Allow SMT Mac Beta to read Documents when macOS asks, or grant Documents access in System Settings → Privacy & Security → Files and Folders."); }
        catch(Exception e) when(e is IOException or System.Text.RegularExpressions.RegexMatchTimeoutException)
        { Get<TextBlock>("IntelState").Text="Log access error · retrying\n"+current.Folder; SetStatus(e.Message); }
        finally { polling=false; }
    }
    private void AddReport(IntelReport report)
    {
        if(reports.Any(r=>r.Time==report.Time && r.Speaker==report.Speaker && r.Message==report.Message && r.Source==report.Source)) return;
        reports.Add(report); if(reports.Count>500) reports.RemoveRange(0,reports.Count-500);
    }
    private void RefreshIntel()
    {
        Get<ListBox>("IntelList").ItemsSource=reports.OrderByDescending(r=>r.Time).Take(100).Select(r=>new IntelItem(r)).ToArray();
        Get<TextBlock>("IntelCount").Text=$"{reports.Count} REPORTS";
        var latest=new Dictionary<string,IntelReport>();
        foreach(var r in reports.OrderBy(r=>r.Time)) foreach(var name in r.Systems) latest[name]=r;
        Map.Reports=latest; Map.InvalidateVisual();
    }
    private void AddManual()
    {
        var text=Get<TextBox>("ManualIntel").Text ?? "";
        var report=parser.Parse(text,demo?"Demo / manual":"Manual",DateTimeOffset.UtcNow,true);
        if(report==null) { SetStatus("No exact system name found in the report."); return; }
        AddReport(report); RefreshIntel(); Get<TextBox>("ManualIntel").Text="";
    }
    public void ToggleDemo()
    {
        demo=!demo; reports.Clear();
        Get<Border>("DemoBanner").IsVisible=demo; Get<Button>("DemoButton").Content=demo?"Exit demo":"Try demo";
        if(demo)
        {
            Get<ComboBox>("RegionPicker").SelectedItem=universe.Data.Regions.First(r=>r.Name=="Delve");
            var now=DateTimeOffset.UtcNow;
            foreach(var (msg,age) in new[]{("1DQ1-A 3 neutrals on the gate",1),("T5ZI-S clear",3),("N-8YET interceptor moving towards 1DQ1-A",5)})
            {
                var r=parser.Parse(msg,"DEMO",now.AddMinutes(-age),true); if(r!=null) AddReport(r with {Speaker="Demo scout"});
            }
            Get<TextBox>("FromBox").Text="1DQ1-A"; Get<TextBox>("ToBox").Text="N-8YET"; Get<CheckBox>("HighSecOnly").IsChecked=false; Plan();
        }
        else if(tailer!=null) tailer=new LogTailer(tailer.Folder);
        RefreshIntel(); UpdateWatchState(); SetStatus(demo?"Synthetic reports for demonstration only. No live intel is mixed into this view.":"Demo ended. Automatic live monitoring resumes.");
    }
}
