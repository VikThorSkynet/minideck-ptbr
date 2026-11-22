using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;

namespace MiniDeck;
public sealed class ActionEditor : UserControl
{
    private DeckConfig config=DeckConfig.Create();
    private readonly AudioService audio=new();
    private readonly StackPanel root=new(),targetPanel=new(),valuePanel=new(),choicePanel=new(),stepPanel=new(),programPanel=new(),macroPanel=new(),delayPanel=new(),soundPanel=new();
    private readonly Slider soundVolume=new(){Name="SoundClipVolume",Minimum=0,Maximum=100,TickFrequency=1,IsSnapToTickEnabled=true,Margin=new Thickness(0,8,0,14)};
    private readonly TextBlock soundVolumeLabel=new();
    private readonly ComboBox soundBehavior=new(){Name="SoundBehavior",ItemsSource=Catalog.SoundBehaviors};
    private readonly CheckBox soundLoop=new(){Name="SoundLoop",Content="Repetir até eu mandar parar",Foreground=Ui.Brush("#EAF0F7"),Margin=new Thickness(0,0,0,12)};
    private readonly TextBox name=new(){Name="ActionName",MaxLength=60},value=new(){Name="ActionValue",TextWrapping=TextWrapping.Wrap,MinHeight=38},step=new(){Name="ActionStep",Text="5"},arguments=new(){Name="ActionArguments"},folder=new(){Name="ActionWorkingDirectory"},delay=new(){Text="500"},repeat=new(){Text="1"};
    private readonly ComboBox kind=new(){Name="ActionKind"},target=new(){Name="ActionTarget"},choice=new(){Name="ActionValueChoice"};
    private readonly TextBlock targetLabel=new(),valueLabel=new(),hint=new(){Foreground=Ui.Brush("#AABACE"),FontSize=12,Margin=new Thickness(0,5,0,12),TextWrapping=TextWrapping.Wrap},macroCount=new();
    private readonly Button capture=new(){Content="Capturar atalho",Name="CaptureShortcut"},browse=new(){Content="Escolher arquivo / programa"},refresh=new(){Content="Atualizar lista de áudio"};
    private List<DeckAction> steps=new();
    private bool loading,capturing;
    public ActionEditor()
    {
        Content=root;
        Add(root,"Nome no painel",name);Add(root,"O que esta entrada faz?",kind);
        targetPanel.Children.Add(targetLabel);targetPanel.Children.Add(target);targetPanel.Children.Add(refresh);root.Children.Add(targetPanel);
        valuePanel.Children.Add(valueLabel);valuePanel.Children.Add(value);valuePanel.Children.Add(capture);valuePanel.Children.Add(browse);root.Children.Add(valuePanel);
        Add(choicePanel,"Comando",choice);root.Children.Add(choicePanel);
        Add(stepPanel,"Variação a cada toque (%)",step);root.Children.Add(stepPanel);
        Add(programPanel,"Argumentos (opcional)",arguments);Add(programPanel,"Pasta de trabalho (opcional)",folder);root.Children.Add(programPanel);
        Add(delayPanel,"Espera em milissegundos (1.000 = 1 segundo)",delay);root.Children.Add(delayPanel);
        soundPanel.Children.Add(soundVolumeLabel);soundPanel.Children.Add(soundVolume);Add(soundPanel,"Ao apertar novamente",soundBehavior);soundPanel.Children.Add(soundLoop);root.Children.Add(soundPanel);
        soundVolume.ValueChanged+=(_,_)=>soundVolumeLabel.Text="Volume deste som: "+Math.Round(soundVolume.Value)+"%";
        macroPanel.Children.Add(macroCount);var edit=new Button{Content="Montar sequência de ações",Name="EditSequence"};macroPanel.Children.Add(edit);Add(macroPanel,"Quantas vezes repetir?",repeat);root.Children.Add(macroPanel);
        root.Children.Add(hint);
        kind.SelectionChanged+=(_,_)=>{if(!loading){steps=new();Configure(new DeckAction{Kind=Kind,Value=Kind.EndsWith("Set")?"50":Kind=="led"?"0":""});}};
        refresh.Click+=(_,_)=>{try{BuildTargets((target.SelectedItem as Choice)?.Id??target.Text);}catch(Exception e){hint.Text=e.Message;}};
        capture.Click+=(_,_)=>{capturing=true;capture.Content="Aperte o atalho… (Esc cancela)";capture.Focus();};
        PreviewKeyDown+=CaptureKey;
        browse.Click+=(_,_)=>{
            var dialog=new OpenFileDialog{Filter=Kind=="soundPlay"?"Áudio (MP3, WAV, M4A, WMA)|*.mp3;*.wav;*.m4a;*.wma;*.aac;*.aiff;*.aif|Todos os arquivos|*.*": "Todos os arquivos|*.*",CheckFileExists=true};
            if(dialog.ShowDialog(Window.GetWindow(this))==true){value.Text=dialog.FileName;if(Kind=="soundPlay"&&(name.Text=="Sem ação"||name.Text.StartsWith("Escolher som",StringComparison.Ordinal)||string.IsNullOrWhiteSpace(name.Text)||name.Text==Catalog.Kinds.First(c=>c.Id==Kind).Name))name.Text=System.IO.Path.GetFileNameWithoutExtension(dialog.FileName);}
        };
        edit.Click+=(_,_)=>{var owner=Window.GetWindow(this);var dialog=new MacroEditorWindow(config,steps){Owner=owner};if(owner!=null)dialog.Resources.MergedDictionaries.Add(owner.Resources);if(dialog.ShowDialog()==true){steps=dialog.Result;UpdateMacroCount();}};
    }
    private static void Add(Panel panel,string label,Control control){panel.Children.Add(new TextBlock{Text=label,TextWrapping=TextWrapping.Wrap});panel.Children.Add(control);}
    private string Kind=>(kind.SelectedItem as Choice)?.Id??"none";
    public void Load(DeckConfig cfg,DeckAction action,bool insideSequence=false)
    {
        config=cfg;loading=true;capturing=false;capture.Content="Capturar atalho";
        kind.ItemsSource=Catalog.Kinds.Where(k=>!insideSequence||k.Id is not("sequence" or "holdLayer")).ToArray();
        name.Text=action.Name;kind.SelectedItem=((IEnumerable<Choice>)kind.ItemsSource).FirstOrDefault(k=>k.Id==action.Kind)??((IEnumerable<Choice>)kind.ItemsSource).First();
        steps=action.Steps.Select(s=>s.Copy()).ToList();Configure(action);loading=false;
    }
    private void Configure(DeckAction action)
    {
        string k=Kind;bool endpoint=k.StartsWith("mic")||k.StartsWith("output"),app=k.StartsWith("app"),layer=k is "layer" or "holdLayer",led=k=="led",sound=k=="soundPlay";
        targetPanel.Visibility=endpoint||app||layer||led||sound?Visibility.Visible:Visibility.Collapsed;
        refresh.Visibility=endpoint||app||sound?Visibility.Visible:Visibility.Collapsed;
        valuePanel.Visibility=k is "shortcut" or "text" or "open" or "program" or "soundPlay"||k.EndsWith("Set")?Visibility.Visible:Visibility.Collapsed;
        choicePanel.Visibility=k is "media" or "mouse" or "led"?Visibility.Visible:Visibility.Collapsed;
        stepPanel.Visibility=(endpoint||app)&&!k.EndsWith("Mute")&&!k.EndsWith("Set")||k is "soundVolumeUp" or "soundVolumeDown"?Visibility.Visible:Visibility.Collapsed;
        soundPanel.Visibility=sound?Visibility.Visible:Visibility.Collapsed;soundVolume.Value=action.SoundVolume;soundVolumeLabel.Text="Volume deste som: "+action.SoundVolume+"%";soundLoop.IsChecked=action.SoundLoop;soundBehavior.SelectedItem=Catalog.SoundBehaviors.FirstOrDefault(c=>c.Id==action.SoundBehavior)??Catalog.SoundBehaviors[0];
        programPanel.Visibility=k=="program"?Visibility.Visible:Visibility.Collapsed;macroPanel.Visibility=k=="sequence"?Visibility.Visible:Visibility.Collapsed;delayPanel.Visibility=k=="delay"?Visibility.Visible:Visibility.Collapsed;
        capture.Visibility=k=="shortcut"?Visibility.Visible:Visibility.Collapsed;browse.Visibility=k is "open" or "program" or "soundPlay"?Visibility.Visible:Visibility.Collapsed;browse.Content=sound?"Escolher som…":"Escolher arquivo / programa";
        value.AcceptsReturn=k=="text";value.MinHeight=k=="text"?85:38;
        valueLabel.Text=sound?"Arquivo do som (MP3 / WAV)":k=="text"?"Texto para digitar":k.EndsWith("Set")?"Volume desejado (0 a 100%)":k=="shortcut"?"Atalho (ex.: Ctrl+Shift+M)":"Caminho do programa, arquivo ou site";
        value.Text=action.Value;step.Text=action.Step.ToString();arguments.Text=action.Arguments;folder.Text=action.WorkingDirectory;delay.Text=action.DelayMs.ToString();repeat.Text=action.Repeat.ToString();
        targetLabel.Text=layer?"Camada de destino":led?"Camada de memória do teclado":app?"Aplicativo (nome do processo)":k.StartsWith("mic")?"Qual microfone?":"Qual saída de áudio?";
        BuildTargets(action.Target);
        var choices=k=="media"?Catalog.Media:k=="mouse"?Catalog.Mouse:led?new[]{new Choice("0","Modo 0"),new Choice("1","Modo 1"),new Choice("2","Modo 2")}:Array.Empty<Choice>();
        choice.ItemsSource=choices;choice.SelectedItem=choices.FirstOrDefault(c=>c.Id==action.Value)??choices.FirstOrDefault();
        hint.Text=k switch {
            "soundPlay"=>"O som toca pelo MiniDeck, com volume próprio. No OBS, capture a saída escolhida ou o áudio do MiniDeck. Para Voicemeeter ou cabo virtual, selecione essa saída aqui. O arquivo permanece na pasta original.",
            "soundStop"=>"Para imediatamente todos os sons do MiniDeck, inclusive os que estão repetindo.",
            "soundVolumeUp" or "soundVolumeDown" or "soundVolumeSet" or "soundMute"=>"Controla somente os sons do MiniDeck, inclusive os que já estão tocando. Você pode colocar esse controle no knob.",
            "sequence"=>"Combine atalhos, texto, áudio, programas, mudanças de camada e pausas. A ordem dos passos é a ordem de execução.",
            "program"=>"Informe o executável e seus argumentos separadamente. Para scripts, escolha o interpretador e passe o arquivo nos argumentos.",
            "holdLayer"=>"A camada muda enquanto a tecla fica pressionada, se o firmware mantiver esse evento. Use uma tecla ou o clique do knob.",
            "led"=>"Envia um dos 3 modos encontrados no programa original e grava o ajuste no teclado. Cor RGB, brilho e velocidade não são expostos por este protocolo.",
            "shortcut"=>"Use Capturar atalho ou digite a combinação. O atalho é enviado ao aplicativo em primeiro plano.",
            "appUp" or "appDown" or "appMute" or "appSet"=>"Reproduza um som para o aplicativo aparecer. Você também pode digitar chrome ou Discord, por exemplo.",
            "delay"=>"Este passo espera sem travar o painel nem impedir a troca de camadas.",
            "cancelMacro"=>"Interrompe uma sequência, inclusive durante uma pausa entre passos.",
            _=>"Clique em Salvar ação para colocar esta configuração em uso."
        };UpdateMacroCount();
    }
    private void BuildTargets(string selected)
    {
        string k=Kind;List<Choice> list=new();
        try {
            if(k.StartsWith("mic")||k.StartsWith("output")||k=="soundPlay")list=audio.Devices(k.StartsWith("mic"));
            else if(k.StartsWith("app"))list=audio.Applications();
            else if(k is "layer" or "holdLayer")list=config.Layers.Select(l=>new Choice(l.Id,l.Name)).ToList();
            else if(k=="led")list=new(){new("1","Camada 1"),new("2","Camada 2"),new("3","Camada 3")};
            if(!string.IsNullOrEmpty(selected)&&!list.Any(c=>c.Id==selected)&&!k.StartsWith("app"))list.Add(new(selected,"Dispositivo / destino salvo (indisponível)"));
            target.IsEditable=k.StartsWith("app");target.ItemsSource=list;target.SelectedItem=list.FirstOrDefault(c=>c.Id==selected)??(!k.StartsWith("app")?list.FirstOrDefault():null);
            if(target.IsEditable&&target.SelectedItem==null)target.Text=selected;
        }catch(Exception e){hint.Text="Não foi possível listar os dispositivos: "+e.Message;}
    }
    private void UpdateMacroCount()=>macroCount.Text=$"{steps.Count} passo(s) na sequência";
    private void CaptureKey(object sender,KeyEventArgs e)
    {
        if(!capturing)return;e.Handled=true;var key=e.Key==Key.System?e.SystemKey:e.Key;
        if(key==Key.Escape){capturing=false;capture.Content="Capturar atalho";return;}
        if(key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)return;
        var parts=new List<string>();var mods=Keyboard.Modifiers;
        if(mods.HasFlag(ModifierKeys.Control))parts.Add("Ctrl");if(mods.HasFlag(ModifierKeys.Alt))parts.Add("Alt");if(mods.HasFlag(ModifierKeys.Shift))parts.Add("Shift");if(mods.HasFlag(ModifierKeys.Windows))parts.Add("Win");
        parts.Add(((System.Windows.Forms.Keys)KeyInterop.VirtualKeyFromKey(key)).ToString());value.Text=string.Join("+",parts);capturing=false;capture.Content="Capturar atalho";
    }
    public DeckAction Read()
    {
        var a=new DeckAction{Name=name.Text.Trim(),Kind=Kind,Target=(target.SelectedItem as Choice)?.Id??target.Text.Trim(),Value=value.Text,Steps=steps.Select(s=>s.Copy()).ToList(),Arguments=arguments.Text,WorkingDirectory=folder.Text};
        if(a.Name=="")a.Name=Catalog.Kinds.First(c=>c.Id==a.Kind).Name;
        if(choicePanel.Visibility==Visibility.Visible)a.Value=(choice.SelectedItem as Choice)?.Id??"";
        if(stepPanel.Visibility==Visibility.Visible)a.Step=int.TryParse(step.Text,out var n)?n:0;
        if(a.Kind=="delay")a.DelayMs=int.TryParse(delay.Text,out var ms)?ms:-1;
        if(a.Kind=="sequence")a.Repeat=int.TryParse(repeat.Text,out var repeats)?repeats:0;
        if(a.Kind=="soundPlay"){a.SoundVolume=(int)Math.Round(soundVolume.Value);a.SoundLoop=soundLoop.IsChecked==true;a.SoundBehavior=(soundBehavior.SelectedItem as Choice)?.Id??"restart";}
        ActionRules.Validate(a);return a;
    }
}
public static class Ui
{
    public static System.Windows.Media.Brush Brush(string hex)=>(System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString(hex)!;
    public static double Luminance(string hex)
    {
        var c=(System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
        static double Linear(byte value){double s=value/255d;return s<=0.04045?s/12.92:Math.Pow((s+0.055)/1.055,2.4);}
        return 0.2126*Linear(c.R)+0.7152*Linear(c.G)+0.0722*Linear(c.B);
    }
    public static string InkFor(string background)=>Luminance(background)>0.179?"#000000":"#FFFFFF";
}
