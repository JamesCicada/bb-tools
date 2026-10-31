using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using BBTools.LinuxCore;
using BBTools.Memory;

namespace BBTools.LinuxDesktop;

public partial class MainWindow
{
    private UserLibrary? library;
    private AppSettings preferences=new();
    private readonly Dictionary<string,CheckBox> switches=[];
    private readonly Dictionary<string,NumericUpDown> values=[];
    private readonly List<Control> editingControls=[];
    private readonly List<Action> refreshLists=[];
    private readonly List<Action> refreshPreferences=[];
    private TextBlock enemyText=new();
    private TextBlock timerText=new();
    private bool polling,startupApplied,preserveStatus;
    private PracticePreset? beforePractice;
    private string? retryBoss;
    private bool retryFirstEncounter;
    private long lastAutoConnect;
    private DiscordPresence? presence;
    private static T Localize<T>(T control,AvaloniaProperty property,string source)where T:Control
    {
        var key=Translations.English.FirstOrDefault(x=>x.Value==source).Key;
        if(key!=null)control.Bind(property,control.GetResourceObservable("L."+key));
        return control;
    }
    private static TextBlock Label(string text)=>Localize(new TextBlock{Text=Translations.Source(text),Margin=new(0,6,0,2)},TextBlock.TextProperty,text);
    private static StackPanel Column()=>new(){Spacing=8,Margin=new(0,12,0,12)};
    private static ScrollViewer Page(Control content)=>new(){Content=content};
    private static T Selected<T>(ComboBox box)=>box.SelectedItem is T item?item:throw new ArgumentException("Choose an entry first.");
    private static ComboBox Picker<T>(IEnumerable<T> items)
    {var box=new ComboBox{ItemsSource=items.ToArray(),HorizontalAlignment=HorizontalAlignment.Stretch};if(box.ItemCount>0)box.SelectedIndex=0;return box;}
    private static void Add(Panel p,params Control[] controls){foreach(var c in controls)p.Children.Add(c);}
    private Button Button(string text,Func<Task> action,bool edits=true)
    {
        var button=Localize(new Button{Content=Translations.Source(text)},ContentControl.ContentProperty,text);
        if(edits){editingControls.Add(button);button.IsEnabled=false;}
        button.Click+=async(_,_)=>{if(busy)return;busy=true;try{log?.Write("ACTION",text);await action();if(!preserveStatus)Status.Text=Translations.Source("Completed.");preserveStatus=false;}catch(Exception ex)when(Expected(ex)){Report(ex);}finally{busy=false;}};
        return button;
    }
    private CheckBox Switch(Panel panel,string key,string text,bool enabled=false)
    {var box=Localize(new CheckBox{Content=Translations.Source(text),IsChecked=enabled},ContentControl.ContentProperty,text);switches[key]=box;panel.Children.Add(box);return box;}
    private NumericUpDown Value(Panel panel,string key,string text,decimal initial,decimal min,decimal max,decimal step=1)
    {panel.Children.Add(Label(text));var input=new NumericUpDown{Value=initial,Minimum=min,Maximum=max,Increment=step,FormatString="0.###",HorizontalAlignment=HorizontalAlignment.Stretch};values[key]=input;panel.Children.Add(input);return input;}
    private bool On(string key)=>switches[key].IsChecked==true;
    private float V(string key)=>(float)(values[key].Value??throw new ArgumentException("Enter a number."));
    private Task Native(Func<LinuxGameFeatures,Task> action)=>action(Active().Features);
    private Task NativeAction(Action<LinuxGameFeatures> action)=>Native(f=>f.MutateAsync(()=>action(f)));
    private UserLibrary Library()=>library??throw new IOException("The library is unavailable in UI preview mode.");
    private void SavePreferences(){if(!preview)preferences.Save(settingsPath+".preferences");}
    private void InitFeatures()
    {
        if(!preview)
        {
            preferences=AppSettings.Load(settingsPath+".preferences");
            try{library=new UserLibrary();}catch(Exception ex)when(Expected(ex)){Report(ex);}
            GameDiagnostics.Log=(level,message)=>log?.Write(level,message);
            NativeCodeCompatibility.Diagnostic=message=>log?.Write("NATIVE",message);
            if(preferences.DiscordRichPresence)presence=new();
        }
        var tabs=Sections.Items.Cast<TabItem>().ToList();
        var player=(StackPanel)((ScrollViewer)tabs[1].Content!).Content!;
        var extras=Column();extras.IsEnabled=false;Add(player,extras);editingControls.Add(extras);
        var insight=Value(extras,"insight","Insight",0,0,99);
        Add(extras,Button("Read Insight",()=>{insight.Value=Active().Session.ReadInsight();return Task.CompletedTask;}),Button("Set Insight",()=>NativeAction(_=>Active().Session.SetInsight((int)insight.Value!))));
        Add(extras,Label("NG cycle edits the playthrough counter without resetting quests or starting a new journey. Difficulty scaling caps at NG+6."));
        NgCycleState? ng=null;var cycle=Value(extras,"ng","NG cycle (0 = first playthrough)",0,0,999);
        Add(extras,Button("Read NG cycle",()=>{ng=Active().Session.ReadNgCycle();cycle.Value=ng.Value;return Task.CompletedTask;}),Button("Apply NG cycle",()=>NativeAction(_=>{Active().Session.SetNgCycle((int)cycle.Value!,ng??throw new IOException("Read the current NG cycle first."));ng=null;})));
        var combat=(StackPanel)((ScrollViewer)tabs[2].Content!).Content!;
        var combatTabs=new TabControl();Add(combat,combatTabs);
        var tuning=Column();BuildTuning(tuning);
        var parry=Column();BuildParry(parry);
        foreach(var flag in new[]{PracticeFlag.OneShot,PracticeFlag.Stealth,PracticeFlag.Silence,PracticeFlag.NoAi})
        {var toggle=new CheckBox{Tag=flag.ToString(),Content=Translations.Source(flag switch{PracticeFlag.OneShot=>"One-shot kills",PracticeFlag.Stealth=>"Invisible to enemies",PracticeFlag.Silence=>"Silent footsteps",_=>"Disable enemy AI"})};toggle.IsCheckedChanged+=CombatChanged;NativeFlags.Children.Add(toggle);}
        combatTabs.ItemsSource=new[]{new TabItem{Header="Tuning",Content=Page(tuning)},new TabItem{Header="Parry practice",Content=Page(parry)}};
        var camera=Column();BuildCamera(camera);
        var items=BuildItems();var travel=BuildTravel();var enemy=Column();BuildEnemies(enemy);
        var settings=(StackPanel)((ScrollViewer)tabs[3].Content!).Content!;
        BuildSettings(settings);
        var about=Column();BuildAbout(about);
        ((ScrollViewer)tabs[3].Content!).Content=null;
        tabs[3].Content=new TabControl{ItemsSource=new[]{new TabItem{Header="Preferences",Content=Page(settings)},new TabItem{Header="About",Content=Page(about)}}};
        InitPlatform();
        Sections.Items.Clear();
        Sections.ItemsSource=new[]{tabs[0],tabs[1],tabs[2],new TabItem{Header="Items",Content=items},new TabItem{Header="Travel",Content=travel},new TabItem{Header="Camera",Content=Page(camera)},new TabItem{Header="Enemies",Content=Page(enemy)},tabs[3]};
    }
    private void BuildTuning(StackPanel panel)
    {
        Value(panel,"damage","Damage multiplier",1,.1m,100,.1m);Value(panel,"echoes","Blood Echoes multiplier",1,.1m,100,.1m);
        Switch(panel,"status","Keep status buildup empty");
        Switch(panel,"durability","Keep equipped weapons repaired");Switch(panel,"repair","Auto repair below threshold");Value(panel,"threshold","Repair threshold (%)",30,1,100);
        Switch(panel,"discovery","Discovery boost");Value(panel,"discoveryBonus","Discovery bonus",100,0,1000,10);Switch(panel,"drops","Guaranteed eligible drops (including chalice dungeons)");
        Switch(panel,"levels","Free level ups");Switch(panel,"upgrades","Free weapon upgrades");
        Add(panel,Button("Apply tuning",ApplyTuning),Button("Repair equipped weapons now",()=>Native(f=>f.RunAsync(async a=>{await a.RepairWeaponsNativeAsync(100);}))),Button("Cure active slow / rapid poison",()=>Native(f=>f.RunAsync(async a=>{await a.CureActivePoisonAsync();}))),Button("Clear status buildup",()=>NativeAction(_=>Active().Session.ClearStatusBuildup())));
    }
    private void BuildCamera(StackPanel panel)
    {
        Add(panel,Label("FOV and camera overrides can cause performance and visual issues and are unstable."));
        Switch(panel,"fov","Override field of view");Value(panel,"fov","Field of view",43,38,120);
        Switch(panel,"camera","Override camera distance and height");Value(panel,"distance","Camera distance",1,.5m,2,.05m);Value(panel,"height","Camera height",1,.5m,2,.05m);Value(panel,"shoulder","Horizontal camera offset",0,-2,2,.05m);
        Switch(panel,"speed","Override movement speed");Value(panel,"speed","Player movement speed",1,.1m,5,.1m);
        Switch(panel,"gravity","Override world gravity");Value(panel,"gravity","Gravity multiplier",1,.05m,2,.05m);Switch(panel,"hud","Hide HUD");
        Add(panel,Button("Apply camera and movement",ApplyTuning));
        var start=Switch(panel,"startupFov","Apply FOV on startup",preferences.StartupFovEnabled);
        Add(panel,Button("Save startup FOV",()=>{preferences=preferences with{StartupFovEnabled=start.IsChecked==true,StartupFov=V("fov")};SavePreferences();return Task.CompletedTask;},false));
        BuildExploration(panel);
    }
    private PracticePreset Capture(string name)=>new(name,Active().KeepHealth,Active().InfiniteStamina,Active().Flags.Select(x=>x.ToString()).ToArray(),V("damage"),V("echoes"),V("fov"),On("fov"),Preferences:preferences with{StartupPresetName=null},KeepDurability:On("durability"),AutoRepair:On("repair"),RepairThreshold:(int)V("threshold"),TuneCamera:On("camera"),CameraDistance:V("distance"),CameraHeight:V("height"),KeepStatusEmpty:On("status"),Advanced:new(On("speed"),V("speed"),On("gravity"),V("gravity"),On("hud"),On("discovery"),V("discoveryBonus"),On("drops"),On("levels"),On("upgrades")),CameraHorizontal:V("shoulder"));
    private Task ApplyTuning()=>Active().Features.ApplyPresetAsync(Capture("Current"));
    private void ShowPreset(PracticePreset p)
    {
        void Set(string key,bool b)=>switches[key].IsChecked=b;
        void N(string key,float n)=>values[key].Value=(decimal)n;
        N("damage",p.Damage);N("echoes",p.Echoes);N("fov",p.Fov);Set("fov",p.OverrideFov);Set("camera",p.TuneCamera);N("distance",p.CameraDistance);N("height",p.CameraHeight);N("shoulder",p.CameraHorizontal);
        Set("durability",p.KeepDurability);Set("repair",p.AutoRepair);N("threshold",p.RepairThreshold);Set("status",p.KeepStatusEmpty);
        var a=p.Advanced??new();Set("speed",a.MovementEnabled);N("speed",a.Movement);Set("gravity",a.GravityEnabled);N("gravity",a.Gravity);Set("hud",a.HideHud);Set("discovery",a.DiscoveryEnabled);N("discoveryBonus",a.DiscoveryBonus);Set("drops",a.GuaranteedDrops);Set("levels",a.FreeLevels);Set("upgrades",a.FreeUpgrades);
    }
    private void BuildParry(StackPanel panel)
    {
        Add(panel,Label("Parry practice"),Button("Enable practice setup",async()=>{beforePractice??=Capture("Before practice");var p=beforePractice with{Name="Parry practice",KeepHealth=true,Stamina=true,Flags=beforePractice.Flags.Where(x=>x is "NoFallDamage" or "NoEchoesLoss").Concat(new[]{"GodMode","InfiniteItems"}).ToArray(),Damage=.1f};await Active().Features.ApplyPresetAsync(p);ShowPreset(p);SyncCombat();}),Button("Restore previous practice setup",async()=>{var p=beforePractice??throw new IOException("No practice setup to restore.");await NativeAction(f=>f.TargetProtection.Restore());await Active().Features.ApplyPresetAsync(p);ShowPreset(p);SyncCombat();beforePractice=null;}),Button("Protect locked enemy from death",()=>NativeAction(f=>f.TargetProtection.ProtectLockedEnemy())),Button("Restore locked enemy health",()=>NativeAction(_=>Active().Session.RestoreLockedEnemyHealth())),Button("Release practice target",()=>NativeAction(f=>f.TargetProtection.Restore())));
    }
    private Control BuildItems()
    {
        var panel=Column();var all=(library?.Catalog()??[]).Concat(GameActions.CommonItems).DistinctBy(x=>(x.Id,x.Category)).ToArray();
        var picker=Picker(all);var search=new TextBox{PlaceholderText="Search item name or ID"};
        void Filter()=>picker.ItemsSource=all.Where(x=>x.Label.Contains(search.Text??"",StringComparison.OrdinalIgnoreCase)).ToArray();search.TextChanged+=(_,_)=>Filter();
        var id=Value(panel,"itemId","Item ID",7100000,0,int.MaxValue);var category=Picker(new[]{"Goods","Weapon","Armor","Rune / accessory"});var count=Value(panel,"itemCount","Quantity",1,1,600);
        uint Category()=>category.SelectedIndex switch{1=>0,2=>0x10000000,3=>0x20000000,_=>0x40000000};
        picker.SelectionChanged+=(_,_)=>{if(picker.SelectedItem is ItemChoice item){id.Value=item.Id;category.SelectedIndex=item.Category switch{0=>1,0x10000000=>2,0x20000000=>3,_=>0};}};
        Add(panel,search,picker,category,Button("Spawn item",()=>{log?.Write("ITEM",$"ID {(int)id.Value!}, category {Category():X8}, quantity {(int)count.Value!}");return Native(f=>f.RunAsync(a=>a.SpawnAsync((int)id.Value!,Category(),(int)count.Value!)));}));
        Add(panel,Button("Import item names from game files",async()=>{var folder=await Folder();if(folder==null)return;var loaded=await Task.Run(()=>GameItemCatalog.Load(folder));Library().SaveCatalog(loaded);all=loaded.Concat(GameActions.CommonItems).DistinctBy(x=>(x.Id,x.Category)).ToArray();Filter();},false));
        var favoriteName=new TextBox{PlaceholderText="Favorite name"};var favorites=Picker(library?.Favorites()??[]);
        void Favorites()=>favorites.ItemsSource=library?.Favorites()??[];refreshLists.Add(Favorites);
        Add(panel,favoriteName,Button("Save item favorite",()=>{Library().Save(new FavoriteItem(favoriteName.Text??"",(int)id.Value!,Category(),(int)count.Value!));Favorites();return Task.CompletedTask;},false),favorites,Button("Load favorite",()=>{var f=Selected<FavoriteItem>(favorites);id.Value=f.Id;count.Value=f.Quantity;category.SelectedIndex=f.Category switch{0=>1,0x10000000=>2,0x20000000=>3,_=>0};return Task.CompletedTask;},false));
        var gems=Column();BuildGems(gems);return new TabControl{ItemsSource=new[]{new TabItem{Header="Item spawn",Content=Page(panel)},new TabItem{Header="Blood gems",Content=Page(gems)}}};
    }
    private void BuildGems(StackPanel panel)
    {
        var recipes=Picker(GemGuide.Recipes);var shapes=Picker(Array.Empty<int>());shapes.ItemTemplate=new Avalonia.Controls.Templates.FuncDataTemplate<int>((n,_)=>Label(n switch{1=>"Radial",2=>"Triangle",4=>"Waning",8=>"Circle",63=>"Droplet",_=>n.ToString()}));var secondary=Picker(Array.Empty<int>());var curses=Picker(Array.Empty<int>());var summary=Label("");
        void Refresh(){if(recipes.SelectedItem is NaturalGemRecipe r){shapes.ItemsSource=r.Shapes;shapes.SelectedIndex=0;secondary.ItemsSource=r.Secondaries;secondary.SelectedIndex=0;curses.ItemsSource=r.Curses;curses.SelectedIndex=0;summary.Text=GemGuide.Describe(r.Primary);}}
        recipes.SelectionChanged+=(_,_)=>Refresh();Refresh();
        Add(panel,Label("Normal gems · combinations obtainable in-game"),recipes,summary,Label("Shape"),shapes,Label("Secondary effect"),secondary,Label("Curse"),curses);
        secondary.ItemTemplate=new Avalonia.Controls.Templates.FuncDataTemplate<int>((n,_)=>Label(GemGuide.Describe(n)));curses.ItemTemplate=secondary.ItemTemplate;
        Add(panel,Button("Generate natural gem",()=>Native(f=>f.RunAsync(a=>a.SpawnGemAsync(Selected<NaturalGemRecipe>(recipes).Build(Selected<int>(shapes),Selected<int>(secondary),Selected<int>(curses)),true)))));
        var advanced=Switch(panel,"advancedGems","Advanced gem editing (custom combinations)");var advancedPanel=Column();advancedPanel.IsVisible=false;advanced.IsCheckedChanged+=(_,_)=>advancedPanel.IsVisible=advanced.IsChecked==true;
        var source=Value(advancedPanel,"gemSource","Generator ID",1000000,0,int.MaxValue);var shape=Picker(new[]{1,2,4,8,63});shape.ItemTemplate=shapes.ItemTemplate;Add(advancedPanel,Label("Shape"),shape);
        var effectSearch=new TextBox{PlaceholderText="Find an effect"};var effects=Picker(GemGuide.Effects.Values.Take(100).Select(x=>x.Label));
        effectSearch.TextChanged+=(_,_)=>effects.ItemsSource=GemGuide.Effects.Values.Where(x=>x.Label.Contains(effectSearch.Text??"",StringComparison.OrdinalIgnoreCase)).Take(200).Select(x=>x.Label).ToArray();
        Add(advancedPanel,effectSearch,effects);var inputs=Enumerable.Range(0,6).Select(i=>Value(advancedPanel,"gemEffect"+i,"Effect "+(i+1),-1,-1,int.MaxValue)).ToArray();
        Add(advancedPanel,Button("Use selected effect in first slot",()=>{var label=Selected<string>(effects);inputs[0].Value=GemGuide.Effects.Values.First(x=>x.Label==label).Id;return Task.CompletedTask;},false),Button("Generate custom gem",()=>Native(f=>f.RunAsync(a=>a.SpawnGemAsync(new((int)source.Value!,Selected<int>(shape),inputs.Select(x=>(int)x.Value!).ToArray()))))));Add(panel,advancedPanel);
        Add(panel,Button("Copy normal recipe to advanced editor",()=>{var spec=Selected<NaturalGemRecipe>(recipes).Build(Selected<int>(shapes),Selected<int>(secondary),Selected<int>(curses));advanced.IsChecked=true;source.Value=spec.Source;shape.SelectedItem=spec.Shape;for(var i=0;i<6;i++)inputs[i].Value=spec.Effects[i];return Task.CompletedTask;},false));
        GemSpec Current()=>advanced.IsChecked==true?new((int)source.Value!,Selected<int>(shape),inputs.Select(x=>(int)x.Value!).ToArray()):Selected<NaturalGemRecipe>(recipes).Build(Selected<int>(shapes),Selected<int>(secondary),Selected<int>(curses));
        var buildName=new TextBox{PlaceholderText="Gem recipe name"};var builds=Picker(library?.GemBuilds()??[]);
        void RefreshBuilds()=>builds.ItemsSource=library?.GemBuilds()??[];refreshLists.Add(RefreshBuilds);
        Add(panel,buildName,Button("Save gem recipe",()=>{Library().Save(new SavedGemBuild(buildName.Text??"",Current()));RefreshBuilds();return Task.CompletedTask;},false),builds,Button("Load saved gem recipe",()=>
        {
            var spec=Selected<SavedGemBuild>(builds).Gem;var recipe=GemGuide.Match(spec);
            advanced.IsChecked=recipe==null;
            if(recipe!=null){recipes.SelectedItem=recipe;Refresh();shapes.SelectedItem=spec.Shape;secondary.SelectedItem=spec.Effects[2]==-1?-1:spec.Effects[1];curses.SelectedItem=spec.Effects[2]==-1?spec.Effects[1]:spec.Effects[2];}
            else{source.Value=spec.Source;shape.SelectedItem=spec.Shape;for(var i=0;i<6;i++)inputs[i].Value=spec.Effects[i];}
            return Task.CompletedTask;
        },false),Button("Delete gem recipe",()=>{Library().Delete("gemBuild",Selected<SavedGemBuild>(builds).Name);RefreshBuilds();return Task.CompletedTask;},false));
        var inventory=Picker(Array.Empty<InventoryGem>());
        Add(panel,Button("Read inventory gems",()=>{inventory.ItemsSource=Active().Features.ReadInventoryGems();inventory.SelectedIndex=0;return Task.CompletedTask;}),inventory,Button("Copy inventory gem to advanced editor",()=>{var spec=Selected<InventoryGem>(inventory).Spec;advanced.IsChecked=true;source.Value=spec.Source;shape.SelectedItem=spec.Shape;for(var i=0;i<6;i++)inputs[i].Value=spec.Effects[i];return Task.CompletedTask;},false));
    }
    private Control BuildTravel()
    {
        var lamps=Column();var lamp=Picker(GameActions.Lamps);var search=new TextBox{PlaceholderText="Search lamp (base game and DLC)"};search.TextChanged+=(_,_)=>lamp.ItemsSource=GameActions.Lamps.Where(x=>x.Name.Contains(search.Text??"",StringComparison.OrdinalIgnoreCase)).ToArray();
        Add(lamps,search,lamp,Button("Warp to lamp",()=>Native(f=>f.RunAsync(a=>a.WarpAsync(Selected<LampChoice>(lamp))))));
        var destinations=Picker(BossDestinations.All);Add(lamps,Label("Bosses and important locations"),destinations,Button("Travel to entrance",()=>Native(f=>f.RunAsync(a=>a.WarpAsync(new(Selected<PracticeDestination>(destinations).Name,Selected<PracticeDestination>(destinations).EntryLamp))))),Button("Move to destination (after map loads)",()=>Native(f=>f.RunAsync(a=>a.RestoreLocationAsync(Selected<PracticeDestination>(destinations).Position)))));
        var locations=Column();var name=new TextBox{PlaceholderText="Location name"};var saved=Picker(library?.Locations()??[]);
        void Refresh()=>saved.ItemsSource=library?.Locations()??[];refreshLists.Add(Refresh);
        Add(locations,name,Button("Save current location",()=>{Library().Save(Active().Session.ReadLocation(name.Text??""));Refresh();return Task.CompletedTask;}),saved,Button("Restore saved location",()=>Native(f=>f.RunAsync(a=>a.RestoreLocationAsync(Selected<SavedLocation>(saved))))),Button("Delete selected location",()=>{Library().Delete("location",Selected<SavedLocation>(saved).Name);Refresh();return Task.CompletedTask;},false));
        var bosses=Column();var boss=Picker(BossRevivalCatalog.All.Select(x=>x.Name));var first=new CheckBox{Content="Revive as first encounter"};retryBoss=boss.SelectedItem as string;boss.SelectionChanged+=(_,_)=>retryBoss=boss.SelectedItem as string;first.IsCheckedChanged+=(_,_)=>retryFirstEncounter=first.IsChecked==true;Add(bosses,boss,first,Label("Revival changes encounter flags in your save. Bosses may be revived during an encounter."),Button("Revive boss",()=>Native(f=>f.RunAsync(_=>{var result=f.Actions.ReviveBoss(Selected<string>(boss),firstEncounter:first.IsChecked==true);log?.Write("BOSS",result);return Task.CompletedTask;}))));
        Add(bosses,Button("Revive and travel to boss",RetryBossAsync));
        var chalice=Column();var altar=Picker(Array.Empty<ChaliceAltar>());var destination=Picker(GameActions.ChaliceLamps);Add(chalice,Button("Read chalice altars",()=>{altar.ItemsSource=Active().Features.Actions.ReadChaliceAltars();altar.SelectedIndex=0;return Task.CompletedTask;}),altar,destination,Button("Warp in selected chalice",()=>Native(f=>f.RunAsync(a=>a.WarpChaliceAsync(Selected<ChaliceAltar>(altar).Bank,Selected<ChaliceLamp>(destination))))));
        return new TabControl{ItemsSource=new[]{new TabItem{Header="Destinations",Content=Page(lamps)},new TabItem{Header="Saved locations",Content=Page(locations)},new TabItem{Header="Boss revival",Content=Page(bosses)},new TabItem{Header="Chalices",Content=Page(chalice)}}};
    }
    private void BuildEnemies(StackPanel panel)
    {
        Add(panel,Label("Target details remain visible until another enemy is targeted."),enemyText);
        var timer=Label("Timer ready");timerText=timer;Add(panel,Button("Start encounter timer",()=>{Active().Features.Timer.Start("Encounter");timer.Text=Active().Features.Timer.Summary;return Task.CompletedTask;},false),Button("Pause / resume timer",()=>{Active().Features.Timer.TogglePause();timer.Text=Active().Features.Timer.Summary;return Task.CompletedTask;},false),Button("Finish timer",()=>{Active().Features.Timer.Finish();timer.Text=Active().Features.Timer.Summary;return Task.CompletedTask;},false),timer,Button("Reset encounter timer",()=>{Active().Features.Timer.Reset();timer.Text=Active().Features.Timer.Summary;return Task.CompletedTask;},false));
    }
    private void BuildSettings(StackPanel panel)
    {
        var presets=Column();var name=new TextBox{PlaceholderText="Preset name"};var saved=Picker(library?.Presets()??[]);void Refresh()=>saved.ItemsSource=library?.Presets()??[];refreshLists.Add(Refresh);
        Add(presets,Label("Saved presets"),name,Button("Save current settings",()=>{Library().Save(Capture(name.Text??""));Refresh();return Task.CompletedTask;}),saved,Button("Apply preset",async()=>{var p=Selected<PracticePreset>(saved);await Active().Features.ApplyPresetAsync(p);ShowPreset(p);SyncCombat();ApplyPresetPreferences(p.Preferences);}),Button("Set preset on startup",()=>{preferences=preferences with{StartupPresetName=Selected<PracticePreset>(saved).Name};SavePreferences();return Task.CompletedTask;},false),Button("Clear startup preset",()=>{preferences=preferences with{StartupPresetName=null};SavePreferences();return Task.CompletedTask;},false));
        Add(presets,Button("Delete preset",()=>{var old=Selected<PracticePreset>(saved).Name;Library().Delete("preset",old);if(old.Equals(preferences.StartupPresetName,StringComparison.OrdinalIgnoreCase)){preferences=preferences with{StartupPresetName=null};SavePreferences();}Refresh();return Task.CompletedTask;},false),Button("Rename preset",()=>{var old=Selected<PracticePreset>(saved).Name;Library().RenamePreset(old,name.Text??"");if(old.Equals(preferences.StartupPresetName,StringComparison.OrdinalIgnoreCase)){preferences=preferences with{StartupPresetName=name.Text};SavePreferences();}Refresh();return Task.CompletedTask;},false),Button("Duplicate preset",()=>{Library().Save(Selected<PracticePreset>(saved) with{Name=name.Text??""});Refresh();return Task.CompletedTask;},false));
        Add(panel,presets);
        var menus=Picker(GameActions.Menus);Add(panel,Label("Open menu anywhere"),menus,Button("Open selected menu",()=>Native(f=>f.RunAsync(a=>a.OpenMenuAsync(Selected<MenuChoice>(menus))))));
        var automatic=Switch(panel,"autoConnect","Automatically connect to the most likely Bloodborne runtime",preferences.AutoConnect);automatic.IsCheckedChanged+=(_,_)=>{if(changing)return;preferences=preferences with{AutoConnect=automatic.IsChecked==true};SavePreferences();};
        var rpc=Switch(panel,"discord","Discord Rich Presence",preferences.DiscordRichPresence);var extra=Switch(panel,"discordDetails","Show area, level, deaths and character name",preferences.DiscordDetails?.Enabled==true);
        rpc.IsCheckedChanged+=(_,_)=>{if(changing)return;preferences=preferences with{DiscordRichPresence=rpc.IsChecked==true};SavePreferences();presence?.Dispose();presence=rpc.IsChecked==true&&!preview?new():null;};extra.IsCheckedChanged+=(_,_)=>{if(changing)return;preferences=preferences with{DiscordDetails=new(Enabled:extra.IsChecked==true)};SavePreferences();};
        refreshPreferences.Add(()=>{automatic.IsChecked=preferences.AutoConnect;rpc.IsChecked=preferences.DiscordRichPresence;extra.IsChecked=preferences.DiscordDetails?.Enabled==true;switches["startupFov"].IsChecked=preferences.StartupFovEnabled;});
        Add(panel,Label("Library transfer"),Button("Export presets, favorites and locations",async()=>{var path=await SaveFile("bb-tools-library.json");if(path!=null)Library().Export(path);},false),Button("Import library",async()=>{var path=await PickFile();if(path!=null){Library().Import(path);foreach(var refresh in refreshLists)refresh();}},false));
        Add(panel,Button("Back up library database",async()=>{var path=await SaveFile("bb-tools-library.db");if(path!=null)Library().Backup(path);},false));
        var saveFolder=new TextBox{PlaceholderText="Absolute path to your save folder"};Add(panel,Label("Save backup and restore · close the game first"),saveFolder,Button("Choose save folder",async()=>saveFolder.Text=await Folder()??saveFolder.Text,false),Button("Back up save folder",async()=>{if(LinuxProcessCatalog.List().Any(x=>LinuxProcessCatalog.Rank(x.Name)>0))throw new IOException("Close the game before backing up.");var destination=await Folder();if(destination!=null)SaveRestore.CreateBackup(saveFolder.Text??"",destination);},false),Button("Restore save backup",async()=>{var path=await PickFile();if(path!=null)SaveRestore.Restore(path,saveFolder.Text??"",()=>!LinuxProcessCatalog.List().Any(x=>LinuxProcessCatalog.Rank(x.Name)>0));},false));
    }
    private void BuildAbout(StackPanel panel)
    {
        Add(panel,Label("About BB Tools · "+AppInfo.Version),Label("Made with 🖤 By James Cicada. BB Tools is developed by one person; contributions and support help keep it growing."),Button("Support on Ko-fi",()=>{OpenUrl("https://ko-fi.com/jcicada");return Task.CompletedTask;},false),Button("GitHub · contribute or report a bug",()=>{OpenUrl(AppInfo.RepositoryUrl);return Task.CompletedTask;},false),Button("Check for updates",async()=>{using var client=new HttpClient();var result=await UpdateChecker.CheckAsync(client,AppInfo.Version,lifetime.Token);preserveStatus=true;Status.Text=result.IsNewerThan(AppInfo.Version)?"Update available: "+result.LatestVersion:"You have the latest release.";},false));
    }
    private void ApplyPresetPreferences(AppSettings? saved)
    {
        if(saved==null)return;
        var next=saved with{Language=preferences.Language,StartupPresetName=preferences.StartupPresetName,ExperimentalExploration=preferences.ExperimentalExploration};next.Validate();
        preferences=next;controls=next.Camera??new();hotkeys=next.Hotkeys??new();SavePreferences();
        presence?.Dispose();presence=!preview&&next.DiscordRichPresence?new():null;
        changing=true;try{foreach(var refresh in refreshPreferences)refresh();}finally{changing=false;}
    }
    private async Task RetryBossAsync()
    {
        var name=retryBoss??throw new IOException("Choose a boss in Travel first.");
        var destination=BossDestinations.All.FirstOrDefault(x=>x.Name==name)??throw new IOException("This boss has no known practice entrance; use Revive boss and travel manually.");
        await Native(f=>f.RunAsync(_=>{f.Actions.ReviveBoss(name,firstEncounter:retryFirstEncounter);return Task.CompletedTask;}));
        await Native(f=>f.RunAsync(a=>a.WarpAsync(new(name,destination.EntryLamp))));
        var deadline=Environment.TickCount64+30000;
        while(Environment.TickCount64<deadline)
        {
            await Task.Delay(500,lifetime.Token);
            bool ready=false;
            try{ready=Active().Session.ReadPlayer().Health>0&&Active().Session.ReadLocation("Travel check").Block==destination.Position.Block;}
            catch(IOException){}
            if(ready){await Native(f=>f.RunAsync(a=>a.RestoreLocationAsync(destination.Position)));return;}
        }
        throw new IOException("Boss revived and lamp travel submitted; the destination did not finish loading. Use Move to destination after the map loads.");
    }
    private void SyncCombat()
    {
        changing=true;
        try
        {
            KeepHealthToggle.IsChecked=connection?.KeepHealth==true;StaminaToggle.IsChecked=connection?.InfiniteStamina==true;
            foreach(var toggle in NativeFlags.Children.OfType<CheckBox>())if(Enum.TryParse<PracticeFlag>(toggle.Tag?.ToString(),out var flag))toggle.IsChecked=connection?.Flags.Contains(flag)==true;
        }
        finally{changing=false;}
    }
    private async Task AutoConnectAsync()
    {
        if(preview||busy||!preferences.AutoConnect||Environment.TickCount64-lastAutoConnect<5000)return;
        lastAutoConnect=Environment.TickCount64;busy=true;
        try
        {
            var candidates=await Task.Run(LinuxProcessCatalog.List);
            foreach(var candidate in candidates.Where(x=>LinuxProcessCatalog.Rank(x.Name)>=80&&x.Identity.Length>0).Take(4))
            {
                try{connection=await Task.Run(()=>new LinuxGameConnection(candidate,lifetime.Token),lifetime.Token);Status.Text="Connected read-only to "+candidate.Label;break;}
                catch(Exception ex)when(Expected(ex)){log?.Write("CONNECT",ex.Message);}
            }
        }
        finally{busy=false;}
    }
    private async Task<string?> Folder(){var files=await StorageProvider.OpenFolderPickerAsync(new(){AllowMultiple=false});return files.Count==1?files[0].TryGetLocalPath():null;}
    private async Task<string?> PickFile(){var files=await StorageProvider.OpenFilePickerAsync(new(){AllowMultiple=false});return files.Count==1?files[0].TryGetLocalPath():null;}
    private async Task<string?> SaveFile(string name){var file=await StorageProvider.SaveFilePickerAsync(new(){SuggestedFileName=name});return file?.TryGetLocalPath();}
    private static void OpenUrl(string url){var start=new ProcessStartInfo("xdg-open"){UseShellExecute=false};start.ArgumentList.Add(url);Process.Start(start)?.Dispose();}
}
