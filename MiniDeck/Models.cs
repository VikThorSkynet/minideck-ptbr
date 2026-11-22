using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace MiniDeck;

public record Choice(string Id, string Name) { public override string ToString() => Name; }
public record ControlSlot(int Id, string Name, string Trigger);
public sealed class DeckAction
{
    public string Name { get; set; } = "Sem ação";
    public string Kind { get; set; } = "none";
    public string Target { get; set; } = "";
    public string Value { get; set; } = "";
    public int Step { get; set; } = 5;
    public string Arguments { get; set; } = "";
    public string WorkingDirectory { get; set; } = "";
    public int DelayMs { get; set; } = 500;
    public int Repeat { get; set; } = 1;
    public int SoundVolume { get; set; } = 80;
    public string SoundBehavior { get; set; } = "restart";
    public bool SoundLoop { get; set; }
    public List<DeckAction> Steps { get; set; } = new();
    public DeckAction Copy() => new() { Name=Name, Kind=Kind, Target=Target, Value=Value, Step=Step, Arguments=Arguments, WorkingDirectory=WorkingDirectory, DelayMs=DelayMs, Repeat=Repeat, SoundVolume=SoundVolume, SoundBehavior=SoundBehavior, SoundLoop=SoundLoop, Steps=Steps.Select(s=>s.Copy()).ToList() };
}
public sealed class DeckLayer
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Nova camada";
    public string Color { get; set; } = "#68DFC3";
    public int HotkeyBank {get;set;} = -1;
    public Dictionary<int, DeckAction> Actions { get; set; } = new();
}
public sealed class CompactSettings
{
    public bool OpenOnStart { get; set; }
    public bool Topmost { get; set; } = true;
    public double? Left { get; set; }
    public double? Top { get; set; }
    public double Width { get; set; } = 420;
    public double Height { get; set; } = 345;
}
public sealed class DeckConfig
{
    public int Version { get; set; } = 1;
    public int KeyCount { get; set; } = 6;
    public int KnobCount { get; set; } = 1;
    public bool BridgeEnabled { get; set; }
    public bool BridgePrepared { get; set; }
    public bool LayerHotkeysEnabled {get;set;} = true;
    public int NextHotkeyBank {get;set;}
    public string ActiveLayer { get; set; } = "";
    public List<DeckLayer> Layers { get; set; } = new();
    public CompactSettings Compact { get; set; } = new();
    public Dictionary<int,int> LedModes { get; set; } = new();
    public int SoundMasterVolume { get; set; } = 100;
    public bool SoundMuted { get; set; }
    public static DeckConfig Create()
    {
        var c = new DeckConfig();
        var home = new DeckLayer { Name="Principal" };
        home.Actions[1]=new() { Name="Microfone", Kind="micMute" };
        home.Actions[2]=new() { Name="Reproduzir / pausar", Kind="media", Value="play" };
        home.Actions[3]=new() { Name="Próxima faixa", Kind="media", Value="next" };
        home.Actions[4]=new() { Name="Volume −", Kind="outputDown" };
        home.Actions[5]=new() { Name="Volume +", Kind="outputUp" };
        home.Actions[6]=new() { Name="Próxima camada", Kind="nextLayer" };
        var audio=new DeckLayer { Name="Áudio" };
        audio.Actions[1]=new() { Name="Microfone −", Kind="micDown" };
        audio.Actions[2]=new() { Name="Microfone +", Kind="micUp" };
        audio.Actions[3]=new() { Name="Mutar microfone", Kind="micMute" };
        audio.Actions[4]=new() { Name="Volume −", Kind="outputDown" };
        audio.Actions[5]=new() { Name="Volume +", Kind="outputUp" };
        audio.Actions[6]=new() { Name="Próxima camada", Kind="nextLayer" };
        var shortcuts=new DeckLayer { Name="Atalhos" };
        shortcuts.Actions[1]=new() { Name="Copiar", Kind="shortcut", Value="Ctrl+C" };
        shortcuts.Actions[2]=new() { Name="Colar", Kind="shortcut", Value="Ctrl+V" };
        shortcuts.Actions[3]=new() { Name="Desfazer", Kind="shortcut", Value="Ctrl+Z" };
        shortcuts.Actions[4]=new() { Name="Refazer", Kind="shortcut", Value="Ctrl+Y" };
        shortcuts.Actions[5]=new() { Name="Selecionar tudo", Kind="shortcut", Value="Ctrl+A" };
        shortcuts.Actions[6]=new() { Name="Próxima camada", Kind="nextLayer" };
        foreach(var layer in new[]{home,audio,shortcuts}) {
            layer.Actions[13]=new() { Name="Volume −", Kind="outputDown" };
            layer.Actions[14]=new() { Name="Mutar saída", Kind="outputMute" };
            layer.Actions[15]=new() { Name="Volume +", Kind="outputUp" };
        }
        c.Layers.AddRange(new[]{home,audio,shortcuts}); c.ActiveLayer=home.Id;
        return c;
    }
    public void Validate()
    {
        if(Version!=1 || KeyCount<1 || KeyCount>12 || KnobCount<0 || KnobCount>2 || Layers.Count<1 || Layers.Count>100)
            throw new InvalidDataException("Perfil inválido: confira a quantidade de teclas, knobs e camadas.");
        if(Layers.Select(l=>l.Id).Distinct().Count()!=Layers.Count || Layers.Any(l=>string.IsNullOrWhiteSpace(l.Id)||string.IsNullOrWhiteSpace(l.Name)||l.Actions==null||!ValidColor(l.Color)))
            throw new InvalidDataException("O perfil contém camadas inválidas ou repetidas.");
        foreach(var a in Layers.SelectMany(l=>l.Actions)) {
            if(a.Key<1||a.Key>18 || a.Value==null)
                throw new InvalidDataException("O perfil contém uma ação inválida.");
            ActionRules.Validate(a.Value);
        }
        foreach(var a in ActionRules.All(Layers.SelectMany(l=>l.Actions.Values)))if(a.Kind is "layer" or "holdLayer" && !Layers.Any(l=>l.Id==a.Target))throw new InvalidDataException("Uma ação aponta para uma camada que não existe mais.");
        LedModes ??= new();if(LedModes.Any(p=>p.Key<1||p.Key>3||p.Value<0||p.Value>2))throw new InvalidDataException("Perfil de LED inválido.");
        if(SoundMasterVolume<0||SoundMasterVolume>100)throw new InvalidDataException("O volume geral dos sons precisa estar entre 0 e 100%.");
        Compact ??= new();
        if(!double.IsFinite(Compact.Width)||!double.IsFinite(Compact.Height)) { Compact.Width=420;Compact.Height=345; }
        Compact.Width=Math.Clamp(Compact.Width,320,900);Compact.Height=Math.Clamp(Compact.Height,315,800);
        if(Compact.Left is double x&&!double.IsFinite(x))Compact.Left=null;
        if(Compact.Top is double y&&!double.IsFinite(y))Compact.Top=null;
        if(!Layers.Any(l=>l.Id==ActiveLayer)) ActiveLayer=Layers[0].Id;
        LayerHotkeys.EnsureBanks(this);
    }
    public static bool ValidColor(string color) => color!=null&&System.Text.RegularExpressions.Regex.IsMatch(color,"^#[0-9a-fA-F]{6}$");
    public IEnumerable<ControlSlot> Slots()
    {
        for(int i=1;i<=KeyCount;i++) yield return new(i,"Tecla "+i,TriggerName(i));
        for(int k=0;k<KnobCount;k++) {
            yield return new(13+k*3,$"Knob {k+1} · esquerda",TriggerName(13+k*3));
            yield return new(14+k*3,$"Knob {k+1} · pressionar",TriggerName(14+k*3));
            yield return new(15+k*3,$"Knob {k+1} · direita",TriggerName(15+k*3));
        }
    }
    // Dense F13-F24 assignment avoids F1-F12 side effects in foreground apps.
    public int TriggerIndex(int id) => id<=12 ? id-1 : KeyCount+id-13;
    public string TriggerName(int id) => TriggerIndex(id)<12 ? "F"+(13+TriggerIndex(id)) : "Excede F24";
    public int TriggerVk(int id) => 0x7C+TriggerIndex(id);
    public byte TriggerUsage(int id) => TriggerIndex(id)<12 ? checked((byte)(0x68+TriggerIndex(id))) : throw new InvalidOperationException("O modo de camadas suporta até 12 entradas, contando 3 por knob. Reduza o layout ou use a gravação direta.");
}
public static class Catalog
{
    public static readonly Choice[] Kinds={
        new("none","Sem ação"), new("nextLayer","Camadas · próxima"),new("previousLayer","Camadas · anterior"),new("layer","Camadas · ir para"),new("holdLayer","Camadas · enquanto segura"),
        new("soundPlay","Sons · tocar arquivo MP3 / WAV"),new("soundStop","Sons · parar todos"),new("soundVolumeUp","Sons · aumentar volume geral"),new("soundVolumeDown","Sons · diminuir volume geral"),new("soundMute","Sons · mutar / desmutar"),new("soundVolumeSet","Sons · definir volume geral"),
        new("outputUp","Saída de áudio · aumentar volume"),new("outputDown","Saída de áudio · diminuir volume"),new("outputMute","Saída de áudio · mutar / desmutar"),
        new("micUp","Microfone · aumentar volume"),new("micDown","Microfone · diminuir volume"),new("micMute","Microfone · mutar / desmutar"),
        new("appUp","Aplicativo · aumentar volume"),new("appDown","Aplicativo · diminuir volume"),new("appMute","Aplicativo · mutar / desmutar"),
        new("outputSet","Saída de áudio · definir volume"),new("micSet","Microfone · definir volume"),new("appSet","Aplicativo · definir volume"),
        new("media","Multimídia"),new("shortcut","Atalho de teclado"),new("text","Digitar texto"),new("open","Abrir programa, arquivo ou site"),new("program","Executar programa com argumentos"),new("mouse","Mouse · clique ou rolagem"),
        new("sequence","Sequência de ações / macro"),new("delay","Esperar entre ações"),new("cancelMacro","Parar sequências em execução"),new("led","LED do teclado · aplicar modo")
    };
    public static readonly Choice[] Media={new("play","Reproduzir / pausar"),new("next","Próxima faixa"),new("previous","Faixa anterior"),new("stop","Parar"),new("mute","Mutar saída"),new("up","Volume +"),new("down","Volume −")};
    public static readonly Choice[] Mouse={new("left","Clique esquerdo"),new("right","Clique direito"),new("middle","Clique central"),new("up","Rolar para cima"),new("down","Rolar para baixo"),new("ctrlUp","Ctrl + rolar para cima"),new("ctrlDown","Ctrl + rolar para baixo"),new("shiftUp","Shift + rolar para cima"),new("shiftDown","Shift + rolar para baixo"),new("altUp","Alt + rolar para cima"),new("altDown","Alt + rolar para baixo")};
    public static readonly JsonSerializerOptions Json=new() { WriteIndented=true, PropertyNameCaseInsensitive=true, Encoder=System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    public static readonly Choice[] SoundBehaviors={new("restart","Reiniciar o som do começo"),new("toggle","Tocar / parar na mesma tecla"),new("overlap","Sobrepor: tocar outra cópia")};
}
public sealed class ProfileStore
{
    public string Folder { get; }
    public string FilePath => Path.Combine(Folder,"perfil.json");
    public string? LoadWarning { get; private set; }
    public ProfileStore(string? folder=null) { Folder=folder??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"MiniDeck"); }
    public DeckConfig Load()
    {
        if(!File.Exists(FilePath)) return DeckConfig.Create();
        try { return Parse(File.ReadAllText(FilePath)); }
        catch(Exception e) { LoadWarning="Não foi possível carregar seu perfil. O arquivo foi preservado. "+e.Message; return DeckConfig.Create(); }
    }
    public static DeckConfig Parse(string json) { var c=JsonSerializer.Deserialize<DeckConfig>(json,Catalog.Json)??throw new InvalidDataException("Perfil vazio."); c.Validate(); return c; }
    public void Save(DeckConfig c)
    {
        c.Validate(); Directory.CreateDirectory(Folder);
        var temp=FilePath+".tmp"; File.WriteAllText(temp,JsonSerializer.Serialize(c,Catalog.Json));
        if(File.Exists(FilePath)) File.Copy(FilePath,FilePath+".bak",true);
        File.Move(temp,FilePath,true);
    }
}
