using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MiniDeck;
public static class UiSmoke
{
    public static int Run(string folder)
    {
        Directory.CreateDirectory(folder);var checks=new List<string>();CompactWindow? compact=null;
        try {
            var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
            var config=DeckConfig.Create();config.BridgeEnabled=config.BridgePrepared=true;
            var engine=new LayerEngine(config);
            var now=DateTime.UtcNow;
            compact=new CompactWindow(()=>config,()=>engine.Current,id=>engine.Select(id),()=>{},()=>{},()=>now);
            engine.Changed+=compact.Refresh;
            compact.Left=40;compact.Top=40;compact.Topmost=false;compact.Show();compact.UpdateLayout();
            void Check(bool okay,string label){if(!okay)throw new Exception(label);checks.Add(label);}
            Check(compact.DisplayedLayer=="Principal","Nome da camada inicial no painel");
            compact.Pulse(2,true);Check(compact.IsHighlighted(2),"Tecla 2 acende ao pressionar");
            compact.Pulse(2,false);Check(compact.IsHighlighted(2),"Pulso curto permanece visível após soltar");
            Snapshot(compact,Path.Combine(folder,"compacto-principal.png"));
            now=now.AddMilliseconds(1500);compact.Refresh();
            Check(compact.IsHighlighted(2),"Toque rápido ainda acende após olhar para a outra tela por 1,5 segundo");
            now=now.AddMilliseconds(600);compact.Refresh();compact.UpdateLayout();
            Check(!compact.IsHighlighted(2)&&compact.LastPressedSlot==2&&compact.LastPressedLabel=="Última: Tecla 2"&&Texts(Buttons(compact).Single(b=>b.Name=="CompactKey2")).Any(t=>t.Text=="● Tecla 2"),"Após o pulso, a última tecla continua identificada sem parecer segurada");
            Snapshot(compact,Path.Combine(folder,"compacto-ultima-tecla.png"));
            compact.Pulse(6,true);engine.Handle(6,true);
            compact.UpdateLayout();
            Check(compact.DisplayedLayer=="Áudio"&&compact.IsHighlighted(6)&&compact.LastPressedSlot==6&&compact.LastPressedLabel=="Última: Tecla 6","Troca de camada mantém o destaque e a identificação da tecla que a acionou");
            Check(Texts(compact).Any(t=>t.Text=="Microfone +"),"Funções exibidas mudam junto com a camada");
            now=now.AddSeconds(3);Check(compact.IsHighlighted(6),"Tecla segurada continua acesa além da duração do pulso");
            compact.Pulse(6,false);engine.Handle(6,false);
            compact.Pulse(2,false);Check(compact.LastPressedSlot==6&&!compact.IsHighlighted(2),"Soltar uma tecla anterior não apaga a identificação da última tecla");
            config.Layers[1].Color="#101010";compact.Refresh();
            Check(((SolidColorBrush)Buttons(compact).Single(b=>b.Name=="CompactKey6").Background).Color==System.Windows.Media.Color.FromRgb(255,209,102),"Destaque continua claro em uma camada de cor escura");
            config.Layers[1].Actions[1]=new(){Kind="holdLayer",Target=config.Layers[2].Id};
            engine.Handle(1,true);Check(compact.DisplayedLayer=="Atalhos","Painel acompanha camada momentânea");
            engine.Handle(1,false);Check(compact.DisplayedLayer=="Áudio","Painel retorna ao soltar a tecla momentânea");
            compact.ReleaseAll();Check(!compact.IsHighlighted(2)&&!compact.IsHighlighted(6),"Desconexão limpa teclas presas");
            Check(compact.LastPressedSlot==6&&compact.LastPressedLabel=="Última: Tecla 6","Identificação histórica permanece separada do estado de tecla pressionada");
            compact.SetRuntimeStatus("● Mini teclado conectado",false,true);compact.UpdateLayout();Check(Texts(compact).Any(t=>t.Text=="Ⅱ Ações pausadas"),"Estado pausado é visível");
            compact.SetRuntimeStatus("○ Teclado desconectado",true,true);compact.UpdateLayout();Check(Texts(compact).Any(t=>t.Text=="○ Sem conexão"),"Desconexão não aparece como ações ativas");
            compact.Width=320;compact.Height=265;compact.UpdateLayout();Snapshot(compact,Path.Combine(folder,"compacto-pequeno.png"));
            Check(compact.ActualWidth>=320&&compact.ActualHeight>=315,"Tamanho pequeno respeita limites da janela");
            Check(Buttons(compact).Where(b=>b.Name.StartsWith("CompactKey")).All(b=>b.Content is FrameworkElement content&&content.DesiredSize.Height+b.Padding.Top+b.Padding.Bottom<=b.ActualHeight+1),"Nomes das ações cabem nos cartões no tamanho mínimo");
            bool stopSounds=false;compact.StopSoundsRequested+=()=>stopSounds=true;compact.SetSoundState(2,65,false);compact.UpdateLayout();
            var soundStop=Buttons(compact).Single(b=>b.Name=="SoundStopButton");soundStop.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(soundStop.IsVisible&&stopSounds&&soundStop.ToolTip.ToString()!.Contains("65%"),"Painel compacto mostra e aciona Parar sons com volume atual");
            Check(Buttons(compact).Where(b=>b.Name.StartsWith("CompactKey")).All(b=>b.Content is FrameworkElement content&&content.DesiredSize.Height+b.Padding.Top+b.Padding.Bottom<=b.ActualHeight+1),"Ações cabem no tamanho mínimo enquanto há sons tocando");
            Snapshot(compact,Path.Combine(folder,"compacto-sons.png"));compact.SetSoundState(0,65,false);Check(!soundStop.IsVisible,"Parar sons desaparece quando não há áudio tocando");
            compact.SetRuntimeStatus("● Mini teclado conectado",true,true);compact.Width=520;compact.Height=400;compact.UpdateLayout();Snapshot(compact,Path.Combine(folder,"compacto-ampliado.png"));
            var editor=new ActionEditor();var editorWindow=new Window{Content=new ScrollViewer{Content=editor},Width=460,Height=650,Left=40,Top=40,Background=Ui.Brush("#151F2E"),Foreground=Ui.Brush("#EAF0F7"),ShowActivated=false};
            editor.Load(config,new(){Name="Comando com parâmetros",Kind="program",Value="example.exe",Arguments="--arquivo teste",WorkingDirectory="C:\\",Step=5});
            Check(editor.Read().Arguments=="--arquivo teste","Editor preserva argumentos do programa");
            editor.Load(config,new(){Name="Volume exato",Kind="appSet",Target="chrome",Value="0"});
            Check(editor.Read().Value=="0","Editor aceita volume zero sem transformá-lo em volume relativo");
            editor.Load(config,new(){Name="Macro",Kind="sequence",Repeat=2,Steps=new(){new(){Name="Pausa",Kind="delay",DelayMs=40}}});
            Check(editor.Read().Steps.Count==1&&editor.Read().Repeat==2,"Editor preserva passos e repetição da macro");
            editor.Load(config,new(){Name="Atalho",Kind="shortcut",Value="Ctrl+Shift+M"});editorWindow.Show();editorWindow.UpdateLayout();Snapshot(editorWindow,Path.Combine(folder,"editor-atalho.png"));
            string clipPath=Path.Combine(Path.GetTempPath(),"Meu som.mp3");
            editor.Load(config,new(){Name="Meu som",Kind="soundPlay",Value=clipPath,Target="saida-salva-indisponivel",SoundVolume=35,SoundLoop=true,SoundBehavior="toggle"});editorWindow.UpdateLayout();
            var sound=editor.Read();Check(sound.Value==clipPath&&sound.Target=="saida-salva-indisponivel"&&sound.SoundVolume==35&&sound.SoundLoop&&sound.SoundBehavior=="toggle","Editor preserva arquivo, saída, volume individual, repetição e tocar/parar");
            Elements<Slider>(editor).Single(s=>s.Name=="SoundClipVolume").Value=0;Check(editor.Read().SoundVolume==0,"Volume individual pode ser ajustado para zero pelo controle visual");
            Elements<Slider>(editor).Single(s=>s.Name=="SoundClipVolume").Value=35;Snapshot(editorWindow,Path.Combine(folder,"editor-som.png"));editorWindow.Close();
            Exception? macroError=null;var dialog=new MacroEditorWindow(config,new[]{new DeckAction{Name="Pausa inicial",Kind="delay",DelayMs=40}});
            dialog.Loaded+=(_,_)=>dialog.Dispatcher.BeginInvoke(new Action(()=>{
                try {
                    dialog.UpdateLayout();var actionEditor=Elements<ActionEditor>(dialog).Single();
                    actionEditor.Load(config,new(){Name="Primeiro texto",Kind="text",Value="Teste"},true);
                    Buttons(dialog).Single(b=>Equals(b.Content,"Salvar passo")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Buttons(dialog).Single(b=>Equals(b.Content,"+ Adicionar passo")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    actionEditor.Load(config,new(){Name="Esperar",Kind="delay",DelayMs=123},true);
                    Buttons(dialog).Single(b=>Equals(b.Content,"Concluir sequência")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                }catch(Exception ex){macroError=ex;dialog.Close();}
            }));
            dialog.ShowDialog();if(macroError!=null)throw macroError;
            Check(dialog.Result.Count==2&&dialog.Result[0].Kind=="text"&&dialog.Result[0].Value=="Teste"&&dialog.Result[1].DelayMs==123,"Montagem visual de macro salva dois passos na ordem correta");
            File.WriteAllText(Path.Combine(folder,"resultado.json"),JsonSerializer.Serialize(new{Passed=checks.Count,Failed=0,Checks=checks,Note="Eventos simulados internamente em um perfil descartável; sem escrita USB e sem alteração de áudio."},Catalog.Json));
            compact.AllowClose=true;compact.Close();app.Shutdown();return 0;
        }catch(Exception e){if(compact!=null){compact.AllowClose=true;compact.Close();}File.WriteAllText(Path.Combine(folder,"resultado.json"),JsonSerializer.Serialize(new{Passed=checks.Count,Failed=1,Checks=checks,Error=e.ToString()},Catalog.Json));return 1;}
    }
    private static IEnumerable<TextBlock> Texts(DependencyObject node)
    {
        if(node is TextBlock t)yield return t;
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(node);i++)foreach(var text in Texts(VisualTreeHelper.GetChild(node,i)))yield return text;
    }
    private static IEnumerable<Button> Buttons(DependencyObject node)
    {
        if(node is Button b)yield return b;
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(node);i++)foreach(var button in Buttons(VisualTreeHelper.GetChild(node,i)))yield return button;
    }
    private static IEnumerable<T> Elements<T>(DependencyObject node) where T:DependencyObject
    {
        if(node is T value)yield return value;
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(node);i++)foreach(var item in Elements<T>(VisualTreeHelper.GetChild(node,i)))yield return item;
    }
    private static void Snapshot(Window window,string path)
    {
        window.UpdateLayout();var dpi=VisualTreeHelper.GetDpi(window);
        var bitmap=new RenderTargetBitmap((int)Math.Ceiling(window.ActualWidth*dpi.DpiScaleX),(int)Math.Ceiling(window.ActualHeight*dpi.DpiScaleY),dpi.PixelsPerInchX,dpi.PixelsPerInchY,PixelFormats.Pbgra32);bitmap.Render(window);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var output=File.Create(path);encoder.Save(output);
    }
}
