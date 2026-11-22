using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace MiniDeck;
public partial class MainWindow
{
    private void LoadEditor()
    {
        var slot=config.Slots().FirstOrDefault(s=>s.Id==selectedSlot)??config.Slots().First();selectedSlot=slot.Id;
        EditorTitle.Text=slot.Name;EditorLayer.Text=engine.Current.Name+" · "+LayerHotkeys.Describe(config,engine.Current,slot.Id);
        Editor.Load(config,engine.Current.Actions.GetValueOrDefault(selectedSlot)??new DeckAction());
    }
    private DeckAction ReadEditor()
    {
        var action=Editor.Read();
        if(action.Kind=="holdLayer"&&selectedSlot>12&&(selectedSlot-14)%3!=0)throw new FormatException("Enquanto segura precisa de uma tecla ou do clique do knob. Giros são pulsos curtos.");
        return action;
    }
    private void SaveActionClick(object sender,RoutedEventArgs e)
    {
        try{var action=ReadEditor();engine.Current.Actions[selectedSlot]=action;if(SaveLocal()){RenderDeck();compact?.Refresh();SetStatus(action.Name+" salva em "+engine.Current.Name+".");}}
        catch(Exception ex){SetStatus(ex.Message,true);}
    }
    private async void TestActionClick(object sender,RoutedEventArgs e)
    {
        try {
            var action=ReadEditor();if(action.Kind=="holdLayer")throw new InvalidOperationException("Teste a camada enquanto segura usando a tecla física.");
            var shortcut=action.Kind=="none"&&config.LayerHotkeysEnabled?LayerHotkeys.Get(config,engine.Current,selectedSlot):null;
            if(action.Kind=="none"&&config.LayerHotkeysEnabled&&shortcut==null)throw new InvalidOperationException("Escolha uma ação para o knob ou para uma camada sem atalho automático disponível.");
            SetStatus("Teste em 3 segundos. Coloque o aplicativo de destino em primeiro plano…");await Task.Delay(3000);
            if(shortcut!=null){const int testSlot=1000;externalKeys.Press(testSlot,shortcut);try{await Task.Delay(150);}finally{externalKeys.Release(testSlot);}SetStatus("Atalho enviado: "+shortcut.Label);}
            else await ExecuteAction(action,CancellationToken.None);
        }catch(OperationCanceledException){SetStatus("Sequência interrompida.");}catch(Exception ex){SetStatus(ex.Message,true);}
    }
    private async void OnPhysicalKey(int vk,bool down)
    {
        var slot=config.Slots().FirstOrDefault(s=>config.TriggerVk(s.Id)==vk);
        if(slot==null)return;
        compact?.Pulse(slot.Id,down);
        if(!config.BridgeEnabled||busy){if(down)SetStatus(slot.Name+" recebida · ações pausadas.");return;}
        if(down&&slotButtons.TryGetValue(slot.Id,out var button)) {
            button.Background=Color("#3A7566");var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(250)};
            timer.Tick+=(_,_)=>{timer.Stop();if(slotButtons.TryGetValue(slot.Id,out var current))current.Background=Color(slot.Id==selectedSlot?"#243D46":"#1B293C");};timer.Start();
        }
        try {
            if(!down)externalKeys.Release(slot.Id);
            if(down&&config.LayerHotkeysEnabled&&(engine.Current.Actions.GetValueOrDefault(slot.Id)?.Kind??"none")=="none") {
                var shortcut=LayerHotkeys.Get(config,engine.Current,slot.Id);
                if(shortcut==null){SetStatus(slot.Id>12?"Knob sem ação: escolha uma função ou Atalho de teclado.":"Esta camada não tem atalho F automático disponível. Configure uma ação ou um atalho manual.");return;}
                externalKeys.Press(slot.Id,shortcut);SetStatus(slot.Name+" → "+shortcut.Label+" · "+engine.Current.Name);return;
            }
            var action=engine.Handle(slot.Id,down);
            if(action!=null){await ExecuteAction(action,CancellationToken.None);SetStatus(slot.Name+" · "+StatusText.Text);}
            else if(down){SetStatus(slot.Name+" · camada "+engine.Current.Name);SaveLocal();}
        }catch(OperationCanceledException){SetStatus("Sequência interrompida.");}catch(Exception ex){SetStatus(ex.Message,true);}
    }
    private async Task ExecuteAction(DeckAction action,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();string message=action.Name;
        if(action.Kind=="soundPlay")message=sounds.Play(action);
        else if(action.Kind.StartsWith("sound",StringComparison.Ordinal)){message=sounds.Adjust(action);SaveLocal();RefreshSoundControls();}
        else if(action.Kind.StartsWith("mic")||action.Kind.StartsWith("output")||action.Kind.StartsWith("app"))message=audio.Adjust(action);
        else switch(action.Kind) {
            case "shortcut":InputSender.Shortcut(action.Value);break;
            case "media":InputSender.Media(action.Value);break;
            case "text":InputSender.Text(action.Value);break;
            case "mouse":InputSender.Mouse(action.Value);break;
            case "open":Process.Start(new ProcessStartInfo(action.Value){UseShellExecute=true});break;
            case "program":
                var start=new ProcessStartInfo(action.Value,action.Arguments){UseShellExecute=true};
                if(!string.IsNullOrWhiteSpace(action.WorkingDirectory))start.WorkingDirectory=action.WorkingDirectory;
                Process.Start(start);break;
            case "delay":await Task.Delay(action.DelayMs,token);break;
            case "cancelMacro":sequences.Cancel();message="Interrupção da sequência solicitada.";break;
            case "nextLayer":case "previousLayer":
                int i=config.Layers.IndexOf(engine.Current),count=config.Layers.Count;engine.Select(config.Layers[(i+(action.Kind=="nextLayer"?1:count-1))%count].Id);SaveLocal();message="Camada "+engine.Current.Name;break;
            case "layer":engine.Select(action.Target);SaveLocal();message="Camada "+engine.Current.Name;break;
            case "led":
                int layer=int.Parse(action.Target),mode=int.Parse(action.Value);
                bool applied=await WithDevice(async device=>{await Task.Run(()=>{device.Connect();if(device.ReportId==0&&layer!=1)throw new InvalidOperationException("Esta versão só aceita a camada 1.");device.Send(Protocol.Led(layer,mode,device.ReportId));});});
                if(!applied)throw new IOException("Não foi possível aplicar o modo de LED. Confira a conexão.");config.LedModes[layer]=mode;SaveLocal();RefreshLedSelection();message="LED: modo "+mode+" enviado.";break;
            case "sequence":
                try {
                    StopSequenceButton.Visibility=Visibility.Visible;compact?.SetSequenceRunning(true);
                    await sequences.Run(action,ExecuteAction,(done,total)=>SetStatus(action.Name+$" · passo {done}/{total}"));
                    message=action.Name+" concluída.";
                }finally{StopSequenceButton.Visibility=Visibility.Collapsed;compact?.SetSequenceRunning(false);}break;
            case "none":message="Esta entrada ainda não tem ação.";break;
            default:throw new InvalidOperationException("Esta ação não pode ser testada sem pressionar uma tecla física.");
        }
        SetStatus(message);
    }
    private void StopSequenceClick(object sender,RoutedEventArgs e)=>sequences.Cancel();
    private void UpdateInputRouting()
    {
        bool active=config.LayerHotkeysEnabled&&config.BridgeEnabled&&config.BridgePrepared&&miniConnected&&!busy;
        if(!active)externalKeys.ReleaseAll();
        reservedKeys?.Configure(active,Math.Min(12,config.KeyCount+config.KnobCount*3));
    }
    private void LayerHotkeysChanged(object sender,RoutedEventArgs e)
    {
        if(!ready)return;externalKeys.ReleaseAll();engine.ReleaseAll();config.LayerHotkeysEnabled=LayerHotkeysBox.IsChecked==true;
        UpdateInputRouting();RenderDeck();LoadEditor();RefreshBridge();SaveLocal();
        SetStatus(config.LayerHotkeysEnabled?"Atalhos exclusivos ativados. Cadastre a combinação mostrada em cada camada no programa de destino.":"Modo compartilhado ativado: os F13–F24 originais voltam a passar.");
    }
    private void RefreshSoundControls()
    {
        updatingSoundControls=true;
        SoundMasterSlider.Value=config.SoundMasterVolume;
        SoundMasterLabel.Text="Sons · "+(config.SoundMuted?"mudo":config.SoundMasterVolume+"%")+(sounds.ActiveCount>0?" · "+sounds.ActiveCount+" tocando":"");
        StopSoundsButton.IsEnabled=sounds.ActiveCount>0;
        compact?.SetSoundState(sounds.ActiveCount,config.SoundMasterVolume,config.SoundMuted);
        updatingSoundControls=false;
    }
    private void SoundMasterChanged(object sender,RoutedPropertyChangedEventArgs<double> e)
    {
        if(!ready||updatingSoundControls)return;sounds.SetVolume((int)Math.Round(e.NewValue));RefreshSoundControls();SaveLocal();
    }
    private void StopSounds(){sounds.StopAll();RefreshSoundControls();SetStatus("Todos os sons foram parados.");}
    private void StopSoundsClick(object sender,RoutedEventArgs e)=>StopSounds();
    private void AddSoundsLayerClick(object sender,RoutedEventArgs e)
    {
        if(config.Layers.Count>=100){SetStatus("O limite é de 100 camadas.",true);return;}
        var layer=SoundboardTemplates.Create(config);config.Layers.Add(layer);selectedSlot=1;engine.Select(layer.Id);LoadEditor();SaveLocal();
        SetStatus("Camada de sons criada. Escolha uma tecla, selecione Sons · tocar arquivo MP3 / WAV e clique em Escolher som. O knob já controla o volume dos sons.");
    }
}
