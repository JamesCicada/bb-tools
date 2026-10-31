namespace BBTools;

public static class KeyCodes
{
    public static readonly string[] FunctionKeys = Enumerable.Range(1,12).Select(x=>"F"+x).ToArray();
    private static readonly Dictionary<string,int> named = new(StringComparer.OrdinalIgnoreCase)
    { ["Left"]=0x25,["Up"]=0x26,["Right"]=0x27,["Down"]=0x28,["PageUp"]=0x21,["PageDown"]=0x22,["Home"]=0x24,["End"]=0x23,
      ["Insert"]=0x2D,["Delete"]=0x2E,["Escape"]=0x1B,["Space"]=0x20,["Tab"]=9,["Return"]=13 };
    public static readonly string[] Choices = Enumerable.Range(0,26).Select(x=>((char)('A'+x)).ToString())
        .Concat(Enumerable.Range(0,10).Select(x=>"D"+x)).Concat(FunctionKeys).Concat(named.Keys).ToArray();
    public static int VirtualKey(string key)
    {
        if(key.Length==1 && key[0] is >= 'A' and <= 'Z')return key[0];
        if(key.Length==2 && key[0]=='D' && key[1] is >= '0' and <= '9')return key[1];
        if(FunctionKeys.Contains(key))return 0x70+int.Parse(key[1..])-1;
        return named.TryGetValue(key,out var value)?value:throw new ArgumentException("Unsupported key binding.");
    }
}

public sealed record HotkeyPreferences(bool Enabled=false,string Heal="F6",string God="F7",string Save="F8",string Restore="F9",string Retry="F10",string Modifiers="Ctrl+Alt")
{
    public static string[] ModifierChoices => ["None","Ctrl","Alt","Ctrl+Alt"];
    public string[] Keys => [Heal,God,Save,Restore,Retry];
    public string Shortcut(string key) => Modifiers=="None"?key:$"{Modifiers}+{key}";
    public void Validate()
    {
        if(!ModifierChoices.Contains(Modifiers))throw new ArgumentException("Choose None, Ctrl, Alt or Ctrl+Alt for hotkey modifiers.");
        if(Keys.Any(x=>!KeyCodes.FunctionKeys.Contains(x,StringComparer.OrdinalIgnoreCase))||Keys.Distinct(StringComparer.OrdinalIgnoreCase).Count()!=5)
            throw new ArgumentException("Assign a different F1–F12 key to each hotkey action.");
    }
}
