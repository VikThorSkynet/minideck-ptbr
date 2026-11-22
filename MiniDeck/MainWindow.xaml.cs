using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;

namespace MiniDeck;

public partial class MainWindow : Window
{
    private readonly ProfileStore store=new();
    private DeckConfig config;
    private readonly AudioService audio=new();
    private readonly LayerEngine engine;
    private RawKeyboard? input;
    private ReservedFunctionHook? reservedKeys;
    private readonly HeldShortcutOutput externalKeys=new(InputSender.KeyTransitions);
    private bool miniConnected;
    private bool ready,busy,exitRequested;
    private CompactWindow? compact;
    private readonly SequenceRunner sequences=new();
    private readonly SoundboardService sounds;
    private bool updatingSoundControls;
    private int selectedSlot=1;
    private readonly Dictionary<int,Button> slotButtons=new();
    private readonly DispatcherTimer connectionTimer=new(){Interval=TimeSpan.FromSeconds(3)};
    private System.Windows.Forms.NotifyIcon? tray;
    private static Brush Color(string hex)=>(Brush)new BrushConverter().ConvertFromString(hex)!;
    public MainWindow()
    {
        config=store.Load(); engine=new(config);sounds=new(()=>config);
        InitializeComponent();Ui.DarkTitleBar(this);
        sounds.Changed+=()=>Dispatcher.BeginInvoke(new Action(()=>{if(!exitRequested)RefreshSoundControls();}));
        sounds.PlaybackError+=message=>Dispatcher.BeginInvoke(new Action(()=>{if(!exitRequested)SetStatus(message,true);}));
        HardwareKind.ItemsSource=new[]{new Choice("keyboard","Atalho / sequência"),new Choice("media","Multimídia"),new Choice("mouse","Mouse")}; HardwareKind.SelectedIndex=0;
        LedLayer.ItemsSource=HardwareLayer.ItemsSource=new[]{new Choice("1","Camada 1"),new Choice("2","Camada 2"),new Choice("3","Camada 3")}; LedLayer.SelectedIndex=HardwareLayer.SelectedIndex=0;
        LedMode.ItemsSource=new[]{new Choice("0","Modo 0"),new Choice("1","Modo 1"),new Choice("2","Modo 2")}; LedMode.SelectedIndex=0;
        LedLayer.SelectionChanged+=(_,_)=>RefreshLedSelection();RefreshLedSelection();
        KeyCountBox.ItemsSource=Enumerable.Range(1,12).ToArray(); KeyCountBox.SelectedItem=config.KeyCount;
        KnobCountBox.ItemsSource=new[]{0,1,2}; KnobCountBox.SelectedItem=config.KnobCount;
        engine.Changed+=()=>{ RenderDeck(); RefreshBridge(); if(ready)LoadEditor(); compact?.Refresh(); };
        LayerHotkeysBox.IsChecked=config.LayerHotkeysEnabled;
        BuildHelp(); ShowPage("deck"); ready=true; RenderDeck(); LoadEditor(); RefreshHardwareSlots(); RefreshBridge();RefreshSoundControls();
        SourceInitialized+=(_,_)=> {
            try {
                input=new RawKeyboard(new WindowInteropHelper(this).Handle);input.Key+=(vk,down)=>{if(!config.LayerHotkeysEnabled)OnPhysicalKey(vk,down);};
                reservedKeys=new ReservedFunctionHook();reservedKeys.Key+=(vk,down)=>Dispatcher.BeginInvoke(new Action(()=>OnPhysicalKey(vk,down)));
                UpdateInputRouting();
            }catch(Exception e){config.BridgeEnabled=false;RefreshBridge();SetStatus("Ações pausadas: não foi possível receber/isolar as teclas. "+e.Message,true);}
        };
        Loaded+=(_,_)=>{ RefreshConnection(); if(store.LoadWarning!=null) { config.BridgeEnabled=false; SetStatus(store.LoadWarning,true); } };
        connectionTimer.Tick+=(_,_)=>RefreshConnection(); connectionTimer.Start();
        InitializeTray();
        Closing+=(_,e)=> {
            if(busy) { e.Cancel=true; SetStatus("Aguarde o envio ao teclado terminar."); return; }
            SaveLocal();
            if(!exitRequested && config.BridgeEnabled) { e.Cancel=true; Hide(); tray?.ShowBalloonTip(2500,"MiniDeck continua ativo","Use o ícone da bandeja para abrir ou encerrar.",System.Windows.Forms.ToolTipIcon.Info); }
            else {reservedKeys?.Configure(false,0);externalKeys.ReleaseAll();reservedKeys?.Dispose();sequences.Dispose();sounds.Dispose();input?.Dispose(); connectionTimer.Stop(); tray?.Dispose();if(compact!=null){compact.AllowClose=true;compact.Close();}}
        };
    }
    private void InitializeTray()
    {
        tray=new System.Windows.Forms.NotifyIcon { Text="MiniDeck · "+engine.Current.Name, Icon=System.Drawing.SystemIcons.Application,Visible=true };
        var menu=new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("Abrir MiniDeck",null,(_,_)=>Dispatcher.Invoke(ShowWindow));
        menu.Items.Add("Mostrar painel compacto",null,(_,_)=>Dispatcher.Invoke(ShowCompact));
        menu.Items.Add("Parar todos os sons",null,(_,_)=>Dispatcher.Invoke(StopSounds));
        menu.Items.Add("Pausar / retomar ações",null,(_,_)=>Dispatcher.Invoke(()=>{ if(!config.BridgePrepared) { ShowWindow(); return; } config.BridgeEnabled=!config.BridgeEnabled; engine.ReleaseAll(); SaveLocal(); RefreshBridge(); }));
        menu.Items.Add("Encerrar MiniDeck",null,(_,_)=>Dispatcher.Invoke(()=>{ exitRequested=true; Close(); }));
        tray.ContextMenuStrip=menu; tray.DoubleClick+=(_,_)=>Dispatcher.Invoke(ShowWindow);
    }
    public void ShowWindow() {config.Compact.OpenOnStart=false;compact?.Hide();Show();WindowState=WindowState.Normal;Activate();SaveLocal();}
    public void ShowCompact()
    {
        if(busy)return;
        new WindowInteropHelper(this).EnsureHandle();
        compact??=new CompactWindow(()=>config,()=>engine.Current,id=>{engine.Select(id);SaveLocal();},ShowWindow,()=>{SaveLocal();});
        compact.StopSequenceRequested-=sequences.Cancel;compact.StopSequenceRequested+=sequences.Cancel;
        compact.StopSoundsRequested-=StopSounds;compact.StopSoundsRequested+=StopSounds;
        compact.SetSoundState(sounds.ActiveCount,config.SoundMasterVolume,config.SoundMuted);
        config.Compact.OpenOnStart=true;RefreshConnection();compact.Refresh();compact.SetRuntimeStatus(ConnectionText.Text,config.BridgeEnabled,config.BridgePrepared);compact.Show();Hide();SaveLocal();
    }
    public bool StartInCompact=>config.Compact.OpenOnStart;
    public void ShutdownForUpdate(){exitRequested=true;Close();}
    private void CompactClick(object sender,RoutedEventArgs e)=>ShowCompact();
    private void LayerColorClick(object sender,RoutedEventArgs e)
    {
        using var dialog=new System.Windows.Forms.ColorDialog{FullOpen=true,Color=System.Drawing.ColorTranslator.FromHtml(engine.Current.Color)};
        if(dialog.ShowDialog()!=System.Windows.Forms.DialogResult.OK)return;
        engine.Current.Color=$"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";RenderDeck();compact?.Refresh();SaveLocal();SetStatus("Cor do painel atualizada para a camada "+engine.Current.Name+".");
    }
    private void SetStatus(string message,bool error=false) { StatusText.Text=message; StatusText.Foreground=Color(error?"#FFB6A6":"#A5BACD");compact?.SetActivity(message); }
    private bool SaveLocal()
    {
        try { store.Save(config); return true; } catch(Exception e) { SetStatus("Não foi possível salvar o perfil: "+e.Message,true); return false; }
    }
    private void RefreshConnection()
    {
        try {
            var count=KeyboardDevice.Find().Count;
            miniConnected=count==1;
            ConnectionText.Text=count==1?"●  Mini teclado conectado":count>1?"●  Mais de um mini teclado":"○  Teclado desconectado";
            ConnectionText.Foreground=Color(count==1?"#68DFC3":"#E7BA75");
            if(count!=1) {engine.ReleaseAll();compact?.ReleaseAll();}
            compact?.SetRuntimeStatus(ConnectionText.Text,config.BridgeEnabled,config.BridgePrepared);
        }catch{miniConnected=false;ConnectionText.Text="○  Conexão indisponível";}
        UpdateInputRouting();
    }
    private void RefreshBridge()
    {
        ActiveText.Text="Em uso: "+engine.Current.Name;
        BridgeTitle.Text=config.BridgeEnabled?"Camadas ativas · "+engine.Current.Name:config.BridgePrepared?"Ações pausadas":"Prepare o teclado uma única vez";
        BridgeText.Text=config.BridgeEnabled?(config.LayerHotkeysEnabled?"Atalhos exclusivos: teclas sem ação enviam a combinação da camada atual.":"Modo compartilhado: os F13–F24 originais também chegam aos outros programas."):"O MiniDeck precisa ficar aberto para executar camadas e controles de áudio.";
        UpdateInputRouting();
        BridgeButton.Content=config.BridgeEnabled?"Pausar ações":config.BridgePrepared?"Retomar ações":"Ativar camadas";
        compact?.SetRuntimeStatus(ConnectionText.Text,config.BridgeEnabled,config.BridgePrepared);
        if(tray!=null) tray.Text=("MiniDeck · "+engine.Current.Name+(config.BridgeEnabled?" · ativo":" · pausado"))[..Math.Min(63,("MiniDeck · "+engine.Current.Name+(config.BridgeEnabled?" · ativo":" · pausado")).Length)];
    }
    private void RenderDeck()
    {
        LayerHotkeys.EnsureBanks(config);
        LayerPanel.Children.Clear();
        foreach(var layer in config.Layers) {
            bool current=layer==engine.Current;
            var label=new StackPanel{Orientation=Orientation.Horizontal};
            label.Children.Add(new Border{Width=10,Height=10,CornerRadius=new CornerRadius(5),Background=Color(layer.Color),Margin=new Thickness(0,0,8,0),VerticalAlignment=VerticalAlignment.Center});
            label.Children.Add(new TextBlock{Text=layer.Name,FontWeight=current?FontWeights.SemiBold:FontWeights.Normal,VerticalAlignment=VerticalAlignment.Center});
            if(current)label.Children.Add(new TextBlock{Text="  · em uso",FontSize=11,Foreground=Color("#68DFC3"),VerticalAlignment=VerticalAlignment.Center});
            var b=new Button { Content=label, Background=Color(current?"#294A48":"#1D293B"),BorderBrush=Color(current?"#68DFC3":"#33445B"), Padding=new Thickness(13,9,13,9),ToolTip=current?"Camada “"+layer.Name+"”: é a que está em uso agora e a que você está editando.":"Camada “"+layer.Name+"”: clique para passar a usá-la e editar suas teclas." };
            b.Click+=(_,_)=>{ engine.Select(layer.Id); LoadEditor(); SaveLocal(); }; LayerPanel.Children.Add(b);
        }
        KeyGrid.Children.Clear(); KnobPanel.Children.Clear(); slotButtons.Clear();
        KeyGrid.Columns=config.KeyCount<=3?config.KeyCount:3;
        foreach(var slot in config.Slots()) {
            if(slot.Id>12) continue;
            var b=MakeSlot(slot,false); KeyGrid.Children.Add(b);
        }
        for(int k=0;k<config.KnobCount;k++) {
            var panel=new UniformGrid {Columns=3};
            foreach(var slot in config.Slots().Where(s=>s.Id>=13+k*3&&s.Id<=15+k*3)) panel.Children.Add(MakeSlot(slot,true));
            KnobPanel.Children.Add(new TextBlock{Text="KNOB "+(k+1)+"  ·  gire para os lados ou aperte",Foreground=Color("#93A3B8"),FontSize=10,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,8,0,8)}); KnobPanel.Children.Add(panel);
        }
        LayoutCaption.Text=config.KeyCount+" teclas · "+config.KnobCount+" knob(s)";compact?.Refresh();
        ActiveText.Text="Em uso: "+engine.Current.Name;
    }
    private Button MakeSlot(ControlSlot slot,bool knob)
    {
        var a=engine.Current.Actions.GetValueOrDefault(slot.Id)??new DeckAction();
        var panel=new StackPanel();
        panel.Children.Add(new TextBlock {Text=knob?slot.Id%3==1?"↶  ESQUERDA":slot.Id%3==2?"●  PRESSIONAR":"↷  DIREITA":"TECLA "+slot.Id,FontSize=10,Foreground=Color("#91A4BE"),Margin=new Thickness(0,0,0,12)});
        panel.Children.Add(new TextBlock {Text=a.Name,FontSize=knob?12:14,FontWeight=FontWeights.SemiBold,TextWrapping=TextWrapping.Wrap,MaxHeight=58});
        panel.Children.Add(new TextBlock {Text=a.Kind=="none"?LayerHotkeys.Describe(config,engine.Current,slot.Id):ActionShort(a),FontSize=10,Foreground=Color("#8EB3BA"),Margin=new Thickness(0,9,0,0),TextTrimming=TextTrimming.CharacterEllipsis});
        string tip=slot.Name+(slot.Id==selectedSlot?" (selecionada para editar)":" · clique para configurar")+"\nAção: "+a.Name+(a.Kind=="none"?"\n"+LayerHotkeys.Describe(config,engine.Current,slot.Id):"\n"+ActionShort(a));
        var b=new Button {Content=panel, MinHeight=knob?88:100,Padding=new Thickness(12),Background=Color(slot.Id==selectedSlot?"#243D46":"#1B293C"),BorderBrush=Color(slot.Id==selectedSlot?"#68DFC3":"#35465D"),ToolTip=tip,HorizontalContentAlignment=HorizontalAlignment.Stretch};
        b.Click+=(_,_)=>{selectedSlot=slot.Id;RenderDeck();LoadEditor();}; slotButtons[slot.Id]=b;return b;
    }
    private static string ActionShort(DeckAction a)=>ActionRules.Describe(a);
    private void ShowPage(string page)
    {
        DeckView.Visibility=page=="deck"?Visibility.Visible:Visibility.Collapsed; LedView.Visibility=page=="led"?Visibility.Visible:Visibility.Collapsed; HardwareView.Visibility=page=="hardware"?Visibility.Visible:Visibility.Collapsed; HelpView.Visibility=page=="help"?Visibility.Visible:Visibility.Collapsed;
        PageTitle.Text=page switch {"led"=>"Iluminação do teclado","hardware"=>"Gravar no teclado","help"=>"Entenda o seu MiniDeck",_=>"Seu teclado, do seu jeito"};
        PageSubtitle.Text=page switch {"led"=>"Envie um dos modos de LED encontrados no programa original e observe o efeito no teclado.","hardware"=>"Comandos salvos na memória do próprio teclado: funcionam mesmo com o MiniDeck fechado.","help"=>"Primeiros passos, glossário, recursos e solução de problemas.",_=>"Escolha uma camada, clique em uma tecla e defina sua ação."};
        foreach(var (button,id) in new[]{(NavDeck,"deck"),(NavLed,"led"),(NavHardware,"hardware"),(NavHelp,"help")}) {
            bool current=id==page;button.Background=Color(current?"#1A3A3A":"Transparent");button.BorderBrush=Color(current?"#2F6A5F":"Transparent");
        }
    }
    private void DeckClick(object sender,RoutedEventArgs e)=>ShowPage("deck");
    private void LedClick(object sender,RoutedEventArgs e){RefreshLedSelection();ShowPage("led");}
    private void HardwareClick(object sender,RoutedEventArgs e) {RefreshHardwareSlots();ShowPage("hardware");}
    private void HelpClick(object sender,RoutedEventArgs e)=>ShowPage("help");
    private void MinimizeClick(object sender,RoutedEventArgs e) {if(SaveLocal())Hide();}
    private string? AskName(string title,string initial)
    {
        var dialog=new Window {Title=title,Width=440,SizeToContent=SizeToContent.Height,ResizeMode=ResizeMode.NoResize,WindowStartupLocation=WindowStartupLocation.CenterOwner,Owner=this,Background=Color("#151F2E"),Foreground=Color("#EAF0F7"),FontFamily=FontFamily,FontSize=14};
        dialog.Resources.MergedDictionaries.Add(Resources);Ui.DarkTitleBar(dialog);
        var panel=new StackPanel{Margin=new Thickness(22)};var box=new TextBox{Text=initial,MaxLength=40,ToolTip="Até 40 caracteres. Aparece nos botões de camada e no painel compacto."};
        panel.Children.Add(new TextBlock{Text="Nome da camada",FontWeight=FontWeights.SemiBold});panel.Children.Add(box);
        panel.Children.Add(new TextBlock{Text="Use um nome que diga para que serve a camada, por exemplo “OBS”, “Música” ou “Trabalho”. Enter também salva.",Foreground=Color("#93A3B8"),FontSize=12,Margin=new Thickness(0,4,0,14),TextWrapping=TextWrapping.Wrap});
        var save=new Button{Content="Salvar",IsDefault=true,Padding=new Thickness(20,9,20,9),HorizontalAlignment=HorizontalAlignment.Right};save.SetResourceReference(StyleProperty,"Primary");save.Click+=(_,_)=>{if(!string.IsNullOrWhiteSpace(box.Text))dialog.DialogResult=true;};panel.Children.Add(save);dialog.Content=panel;dialog.Loaded+=(_,_)=>{box.Focus();box.SelectAll();};return dialog.ShowDialog()==true?box.Text.Trim():null;
    }
    private void AddLayerClick(object sender,RoutedEventArgs e) {if(config.Layers.Count>=100){SetStatus("O perfil já possui 100 camadas.",true);return;}var name=AskName("Nova camada","Nova camada");if(name==null)return;var l=new DeckLayer{Name=name};l.Actions[config.KeyCount]=new(){Name="Próxima camada",Kind="nextLayer"};config.Layers.Add(l);engine.Select(l.Id);LoadEditor();SaveLocal();}
    private void RenameLayerClick(object sender,RoutedEventArgs e) {var name=AskName("Renomear camada",engine.Current.Name);if(name==null)return;engine.Current.Name=name;RenderDeck();LoadEditor();RefreshBridge();SaveLocal();}
    private void DuplicateLayerClick(object sender,RoutedEventArgs e) {if(config.Layers.Count>=100){SetStatus("O perfil já possui 100 camadas.",true);return;}var old=engine.Current;var l=new DeckLayer{Name=old.Name+" (cópia)",Color=old.Color,Actions=old.Actions.ToDictionary(x=>x.Key,x=>x.Value.Copy())};config.Layers.Add(l);engine.Select(l.Id);LoadEditor();SaveLocal();}
    private void DeleteLayerClick(object sender,RoutedEventArgs e)
    {
        if(config.Layers.Count==1){SetStatus("Mantenha pelo menos uma camada.",true);return;}
        var layer=engine.Current;
        if(MessageBox.Show(this,"Excluir a camada “"+layer.Name+"” e suas ações do perfil?","Excluir camada",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
        config.Layers.Remove(layer);config.ActiveLayer=config.Layers[0].Id;engine.ReleaseAll();
        foreach(var a in ActionRules.All(config.Layers.SelectMany(l=>l.Actions.Values)).Where(a=>a.Target==layer.Id&&a.Kind is "layer" or "holdLayer"))a.Target=config.ActiveLayer;
        engine.Replace(config);LoadEditor();SaveLocal();
    }
    private void LayoutChanged(object sender,SelectionChangedEventArgs e)
    {
        if(!ready)return;
        config.KeyCount=(int)KeyCountBox.SelectedItem; config.KnobCount=(int)KnobCountBox.SelectedItem; config.BridgeEnabled=false;config.BridgePrepared=false;engine.ReleaseAll();selectedSlot=1;RenderDeck();LoadEditor();RefreshBridge();RefreshHardwareSlots();SaveLocal();SetStatus("Layout alterado. Ative as camadas para preparar os novos comandos.");
    }
    private void ExportClick(object sender,RoutedEventArgs e)
    {
        var d=new SaveFileDialog{Filter="Perfil MiniDeck (*.json)|*.json",FileName="Meu MiniDeck.json"};if(d.ShowDialog(this)!=true)return;
        try {File.WriteAllText(d.FileName,JsonSerializer.Serialize(config,Catalog.Json));SetStatus("Perfil exportado.");}catch(Exception ex){SetStatus(ex.Message,true);}
    }
    private void ImportClick(object sender,RoutedEventArgs e)
    {
        var d=new OpenFileDialog{Filter="Perfil MiniDeck (*.json)|*.json"};if(d.ShowDialog(this)!=true)return;
        try {
            var imported=ProfileStore.Parse(File.ReadAllText(d.FileName));imported.BridgeEnabled=false;imported.BridgePrepared=false;
            if(MessageBox.Show(this,"Substituir o perfil aberto pelo perfil importado? O perfil atual ficará em perfil.json.bak.","Importar perfil",MessageBoxButton.YesNo)!=MessageBoxResult.Yes)return;
            sounds.StopAll();config=imported;ready=false;KeyCountBox.SelectedItem=config.KeyCount;KnobCountBox.SelectedItem=config.KnobCount;ready=true;engine.Replace(config);selectedSlot=1;RenderDeck();LoadEditor();RefreshHardwareSlots();RefreshSoundControls();SaveLocal();SetStatus("Perfil importado. Revise as ações e ative as camadas.");
        }catch(Exception ex){SetStatus("Não foi possível importar: "+ex.Message,true);}
    }
    private async Task<bool> WithDevice(Func<KeyboardDevice,Task> task)
    {
        if(busy){SetStatus("Aguarde o envio atual ao teclado.");return false;}
        busy=true;IsEnabled=false;engine.ReleaseAll();compact?.ReleaseAll();UpdateInputRouting();
        try {
            var devices=KeyboardDevice.Find();
            if(devices.Count!=1)throw new InvalidOperationException(devices.Count==0?"Conecte o mini teclado por USB.":"Há mais de um mini teclado. Conecte apenas o que deseja configurar.");
            using var device=new KeyboardDevice(devices[0]); await task(device);return true;
        }catch(Exception ex){SetStatus("Envio interrompido: "+ex.Message+" Se houve gravação parcial, prepare novamente o teclado.",true);return false;}
        finally {busy=false;IsEnabled=true;RefreshBridge();}
    }
    private async void BridgeClick(object sender,RoutedEventArgs e)
    {
        if(config.BridgeEnabled) {config.BridgeEnabled=false;engine.ReleaseAll();SaveLocal();RefreshBridge();SetStatus("Ações pausadas. Os comandos na memória do teclado foram mantidos.");return;}
        if(config.LayerHotkeysEnabled&&reservedKeys?.Running!=true){SetStatus("Reabra o MiniDeck para iniciar o isolamento de atalhos, ou desmarque Atalhos exclusivos para usar o modo compartilhado.",true);return;}
        if(config.BridgePrepared){config.BridgeEnabled=true;SaveLocal();RefreshBridge();SetStatus("Ações retomadas.");return;}
        var slots=config.Slots().ToList();
        if(slots.Count>12){SetStatus("O modo de camadas aceita até 12 entradas: cada knob usa 3. Para 6 teclas, você pode usar até 2 knobs. A gravação direta aceita o layout completo.",true);return;}
        string text="O MiniDeck enviará estes comandos às camadas disponíveis na memória do teclado:\n\n"+string.Join("\n",slots.Select(s=>s.Name+" → "+s.Trigger))+"\n\nOs comandos anteriores dessas entradas serão substituídos. O protocolo original não permite lê-los para backup. Os perfis do MiniDeck ficam salvos neste computador.\n\nApós o envio, mantenha o MiniDeck aberto. Deseja preparar o teclado?";
        if(MessageBox.Show(this,text,"Preparar teclado para camadas",MessageBoxButton.YesNo,MessageBoxImage.Information)!=MessageBoxResult.Yes)return;
        config.BridgeEnabled=false;config.BridgePrepared=false;SaveLocal();
        await WithDevice(async device=>{
            await Task.Run(()=>device.ProgramBridge(config,(done,total)=>Dispatcher.Invoke(()=>SetStatus($"Enviando comandos ao teclado: {done}/{total}…"))));
            config.BridgePrepared=true;config.BridgeEnabled=true;SaveLocal();SetStatus("Comandos enviados. Aperte as teclas e confira se acendem no painel. A tecla "+config.KeyCount+" muda de camada no perfil inicial.");
        });
    }
    private void RefreshHardwareSlots() { HardwareSlot.ItemsSource=config.Slots().Select(s=>new Choice(s.Id.ToString(),s.Name)).ToList();HardwareSlot.SelectedIndex=0; }
    private void HardwareKindChanged(object sender,SelectionChangedEventArgs e)
    {
        if(HardwareTextPanel==null)return;string kind=(HardwareKind.SelectedItem as Choice)?.Id??"keyboard";HardwareTextPanel.Visibility=kind=="keyboard"?Visibility.Visible:Visibility.Collapsed;HardwareChoicePanel.Visibility=kind=="keyboard"?Visibility.Collapsed:Visibility.Visible;HardwareChoice.ItemsSource=kind=="media"?Catalog.Media.Where(c=>c.Id!="stop").ToArray():Catalog.Mouse;HardwareChoice.SelectedIndex=0;
    }
    private async void ApplyHardwareClick(object sender,RoutedEventArgs e)
    {
        int slot=int.Parse(((Choice)HardwareSlot.SelectedItem).Id),layer=int.Parse(((Choice)HardwareLayer.SelectedItem).Id);string kind=((Choice)HardwareKind.SelectedItem).Id;
        (byte Mod,byte Usage)[]? sequence=null;
        try{if(kind=="keyboard")sequence=NativeBindings.Sequence(HardwareValue.Text);}catch(Exception ex){SetStatus(ex.Message,true);return;}
        string value=kind=="keyboard"?HardwareValue.Text:((Choice)HardwareChoice.SelectedItem).Name;
        if(MessageBox.Show(this,$"Substituir {((Choice)HardwareSlot.SelectedItem).Name}, camada {layer}, por:\n\n{value}\n\nAs camadas do MiniDeck serão pausadas. O comando anterior não pode ser recuperado pelo programa.","Gravar entrada",MessageBoxButton.YesNo,MessageBoxImage.Information)!=MessageBoxResult.Yes)return;
        string choice=(HardwareChoice.SelectedItem as Choice)?.Id??"";
        config.BridgeEnabled=false;config.BridgePrepared=false;engine.ReleaseAll();SaveLocal();
        await WithDevice(async device=>{
            await Task.Run(()=>{
                device.Connect();if(device.ReportId==0&&layer!=1)throw new InvalidOperationException("Esta versão só aceita gravação na camada 1.");
                var packets=kind=="keyboard"?Protocol.Keyboard(slot,layer,sequence!,device.ReportId):Protocol.Simple(slot,layer,(byte)(kind=="media"?2:3),kind=="media"?NativeBindings.Media(choice,device.ReportId):NativeBindings.Mouse(choice),device.ReportId);
                device.Send(packets);
            });SetStatus("Comando enviado à memória do teclado. Confira pressionando a entrada física.");
        });
    }
    private async void ApplyLedClick(object sender,RoutedEventArgs e)
    {
        int layer=int.Parse(((Choice)LedLayer.SelectedItem).Id),mode=int.Parse(((Choice)LedMode.SelectedItem).Id);
        if(MessageBox.Show(this,$"Enviar o modo de LED {mode} para a camada {layer}? O efeito depende da versão do seu teclado.","Iluminação",MessageBoxButton.YesNo)!=MessageBoxResult.Yes)return;
        await WithDevice(async device=>{await Task.Run(()=>{device.Connect();if(device.ReportId==0&&layer!=1)throw new InvalidOperationException("Esta versão só aceita a camada 1.");device.Send(Protocol.Led(layer,mode,device.ReportId));});config.LedModes[layer]=mode;SaveLocal();RefreshLedSelection();SetStatus("Modo de LED enviado. Confira o efeito no teclado.");});
    }
    private void RefreshLedSelection()
    {
        if(LedLayer.SelectedItem is not Choice choice)return;int layer=int.Parse(choice.Id);
        bool known=config.LedModes.TryGetValue(layer,out int mode);
        if(known)LedMode.SelectedItem=((Choice[])LedMode.ItemsSource).First(c=>c.Id==mode.ToString());
        LastLedMode.Text=known?$"Último modo enviado pelo MiniDeck à memória {layer}: {mode}.":"Ainda não há envio de LED registrado para esta memória.";
    }
    private void BuildHelp()
    {
        Border Card(string icon,string title,UIElement body,string accent="#68DFC3")
        {
            var head=new DockPanel{Margin=new Thickness(0,0,0,10)};
            head.Children.Add(new Border{Width=34,Height=34,CornerRadius=new CornerRadius(9),Background=Color("#1A3A3A"),Margin=new Thickness(0,0,12,0),Child=new TextBlock{Text=icon,FontSize=16,Foreground=Color(accent),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center}});
            head.Children.Add(new TextBlock{Text=title,FontSize=18,FontWeight=FontWeights.SemiBold,VerticalAlignment=VerticalAlignment.Center,TextWrapping=TextWrapping.Wrap});
            var stack=new StackPanel();stack.Children.Add(head);stack.Children.Add(body);
            var card=new Border{Child=stack,Margin=new Thickness(0,0,0,14)};card.SetResourceReference(StyleProperty,"Card");return card;
        }
        TextBlock Body(string text)=>new(){Text=text,Foreground=Color("#AABACE"),FontSize=13.5,LineHeight=22,TextWrapping=TextWrapping.Wrap};
        void Section(string icon,string title,string text,string accent="#68DFC3")=>HelpContent.Children.Add(Card(icon,title,Body(text),accent));
        void Group(string title)=>HelpContent.Children.Add(new TextBlock{Text=title.ToUpperInvariant(),Foreground=Color("#68DFC3"),FontSize=11,FontWeight=FontWeights.Bold,Margin=new Thickness(2,10,0,10)});

        Group("Começando");
        var steps=new StackPanel();
        foreach(var (n,text) in new[]{("1","Ajuste a quantidade de teclas e knobs em Seu teclado → Ajustar layout e gerenciar perfil."),("2","Escolha uma camada, clique numa tecla, escolha a ação no painel à direita e clique em Salvar ação."),("3","Clique em Ativar camadas e revise os comandos que serão enviados ao teclado. Depois aperte uma tecla física: o cartão dela acende na tela.")}) {
            var row=new DockPanel{Margin=new Thickness(0,0,0,10)};
            row.Children.Add(new Border{Width=24,Height=24,CornerRadius=new CornerRadius(12),Background=Color("#68DFC3"),Margin=new Thickness(0,0,12,0),VerticalAlignment=VerticalAlignment.Top,Child=new TextBlock{Text=n,FontWeight=FontWeights.Bold,Foreground=Color("#102720"),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center}});
            row.Children.Add(Body(text));steps.Children.Add(row);
        }
        steps.Children.Add(Body("Pronto. Use Salvar e minimizar para deixar o MiniDeck trabalhando em segundo plano, ou Modo compacto para ver as funções numa segunda tela."));
        HelpContent.Children.Add(Card("🚀","Comece em três passos",steps));

        var glossary=new Grid();glossary.ColumnDefinitions.Add(new(){Width=new GridLength(170)});glossary.ColumnDefinitions.Add(new());
        var terms=new[]{
            ("Camada","Um conjunto completo de funções para as mesmas teclas. Trocar de camada muda o que cada tecla faz, como mudar de página."),
            ("Tecla / entrada","Cada botão físico do mini teclado. Cada knob conta como 3 entradas: girar à esquerda, apertar e girar à direita."),
            ("Ação","O que acontece quando você aperta a entrada: atalho, texto, som, volume, programa, mudança de camada, macro…"),
            ("Sem ação","Com Atalhos exclusivos por camada ligado, a tecla envia uma combinação única (ex.: F17 ou Ctrl+F17) para você usar em outro programa, como OBS ou Discord."),
            ("Ativar / pausar","Ativar camadas prepara o teclado (só na 1ª vez) e liga as ações. Pausar desliga as ações sem apagar nada."),
            ("Gravar no teclado","Salva um comando na memória do próprio aparelho. Funciona mesmo com o MiniDeck fechado, mas tem menos opções."),
            ("Painel compacto","Janela pequena que mostra a camada em uso e o que cada tecla faz. Ideal para uma segunda tela."),
            ("Perfil","Todas as suas camadas e ações. Fica salvo neste computador e pode ser exportado como backup."),
        };
        for(int i=0;i<terms.Length;i++) {
            glossary.RowDefinitions.Add(new(){Height=GridLength.Auto});
            var term=new TextBlock{Text=terms[i].Item1,FontWeight=FontWeights.SemiBold,Foreground=Color("#EAF0F7"),Margin=new Thickness(0,0,12,10),TextWrapping=TextWrapping.Wrap};
            var meaning=Body(terms[i].Item2);meaning.Margin=new Thickness(0,0,0,10);
            Grid.SetRow(term,i);Grid.SetRow(meaning,i);Grid.SetColumn(meaning,1);glossary.Children.Add(term);glossary.Children.Add(meaning);
        }
        HelpContent.Children.Add(Card("📖","Glossário: as palavras do MiniDeck",glossary));
        Section("▦","Seis teclas, muitas funções","No perfil inicial, a tecla 6 alterna entre Principal, Áudio e Atalhos. Cada camada tem suas próprias ações. Você pode criar novas camadas, escolher uma tecla de retorno ou usar Camadas · enquanto segura para acessar outra camada temporariamente. O limite do MiniDeck é de 100 camadas.");
        Section("⚙","O que cada entrada pode fazer","Teclas, giros e cliques dos knobs podem executar atalhos, digitar texto, abrir programas ou sites, controlar mídia, clicar ou rolar o mouse, mudar de camada e ajustar o áudio. O clique do knob e as teclas também podem manter uma camada ativa enquanto ficam pressionados.");
        Section("▶","Testar uma ação","Testar ação em 3 segundos executa a configuração do formulário, mesmo antes de salvar. Use esses 3 segundos para colocar o aplicativo de destino em primeiro plano. Para o uso normal pelo teclado, clique em Salvar ação. O clique nos cartões do painel compacto mostra a descrição e não executa a função.");

        Group("Usando com outros programas");
        Section("🎬","Configurar direto no OBS ou em outro programa","Deixe Atalhos exclusivos por camada marcado. Selecione Sem ação na tecla e salve; cadastre no programa de destino a combinação mostrada acima do editor ou no painel compacto. Com seis teclas: primeira camada F13–F18, segunda F19–F24, terceira Ctrl+F13–F18, quarta Ctrl+F19–F24. Depois vêm combinações com Alt/Shift. O Windows não tem F25. São 96 combinações automáticas (16 bancos de seis teclas); camadas adicionais continuam aceitando as ações do MiniDeck. Os knobs usam as ações configuradas, sem atalho automático para Sem ação. Enquanto o modo está ativo e um mini teclado está conectado, os F13–F24 físicos usados pelo layout ficam reservados em todos os teclados. O sinal original não passa para o destino; uma ação configurada, como trocar camada, não envia um F junto. Ao pausar ou desmarcar o modo, o comportamento compartilhado anterior volta. Depois de atualizar, recadastre os atalhos nas outras aplicações: por exemplo, a tecla 5 na terceira camada passa a Ctrl+F17. Os bancos permanecem ao renomear ou excluir outras camadas.");
        Section("⌨","Sequências, captura de atalhos e programas","Use Capturar atalho para registrar uma combinação. Uma Sequência de ações / macro reúne até 100 passos com pausas, repetição, texto, áudio, programas e mudanças de camada. Use Parar sequências para interromper a execução. Executar programa com argumentos separa executável, argumentos e pasta de trabalho. Definir volume coloca uma entrada, saída ou aplicativo em um percentual exato, inclusive 0%.");

        Group("Sons e áudio");
        Section("♪","Colocar sons em uma camada","1. Clique em + Sons para criar uma camada pronta, ou use qualquer camada existente.\n2. Escolha uma tecla e a ação Sons · tocar arquivo MP3 / WAV.\n3. Clique em Escolher som e selecione seu arquivo. Escolha a saída de áudio, o volume e o que acontece quando apertar de novo.\n4. Clique em Salvar ação. Aperte a tecla física para tocar. Testar ação em 3 segundos também permite ouvir antes de salvar.\nNa camada criada por + Sons, a última tecla troca de camada. O knob diminui o volume dos sons, muta ao clicar e aumenta ao girar para a direita. O controle Sons na lateral ajusta o volume geral. Parar sons encerra todos, inclusive os que repetem; também fica disponível no painel compacto enquanto há som tocando.","#FFD166");
        Section("🎙","Fazer os sons aparecerem na gravação","No OBS, capture o áudio do MiniDeck ou a saída de áudio em que ele está tocando. Se já usa Voicemeeter ou um cabo virtual, escolha essa saída na configuração do som e capture-a no OBS. Escolher um arquivo não o copia: mantenha o MP3 ou WAV na mesma pasta. Cada som tem volume próprio, combinado com o volume geral dos sons. O MiniDeck não altera o volume do Windows para fazer esse ajuste. Trocar de camada não interrompe o som; use Parar sons quando quiser parar.");
        Section("🔊","Áudio por aplicativo e microfone","Escolha o dispositivo de entrada ou saída pelo nome. Para ajustar um aplicativo, selecione seu processo na lista ou digite o nome sem .exe. A lista mostra aplicativos que têm uma sessão de áudio: reproduza algum som se não aparecer. O ajuste alcança as sessões desse processo nas saídas ativas. O microfone usa o volume de entrada do Windows, não filtros ou ganho interno do OBS ou do Voicemeeter.");

        Group("Tela e iluminação");
        Section("▣","Painel compacto para a segunda tela","Clique em Modo compacto para abrir o painel pequeno. Arraste pelo cabeçalho MINIDECK até a outra tela. O nome da camada, as seis teclas e as três ações do knob acompanham o teclado físico. A tecla apertada fica amarela enquanto estiver segurada e por 2 segundos depois de soltar. Uma bolinha, a borda amarela e o texto Última indicam a última entrada usada até você apertar outra, inclusive após trocar de camada. Esse destaque aparece na tela. Você pode redimensionar pelo canto inferior direito. Fixar mantém o painel por cima de outras janelas; Configurar volta ao editor. O tamanho e a posição ficam salvos.");
        Section("🎨","Cor do painel e LED físico","Cor do painel muda apenas a cor que identifica a camada na janela compacta. O LED físico é configurado em Iluminação ou pela ação LED do teclado · aplicar modo. São os 3 modos recuperados do programa original; o protocolo não informa suas cores, brilho ou velocidade. O último modo enviado fica registrado no perfil, sem anunciar leitura de volta da memória.");
        Section("☀","LED: somente o que foi encontrado","O programa original tem modos 0, 1 e 2, sem nomes de efeitos. Não contém controle de cor RGB, brilho, velocidade ou luz individual. É necessário observar o teclado para confirmar se esta unidade tem LED e o que cada modo faz. A troca de camada do MiniDeck não muda automaticamente o LED.");

        Group("Por dentro do MiniDeck");
        Section("⇣","O que fica no teclado e o que fica no Windows","A gravação direta salva atalhos de até 5 passos, mídia ou mouse na memória do teclado. O protocolo oferece até 3 camadas nativas, mas o programa original só as seleciona para configuração; não foi encontrada uma ação física nativa para alterná-las. As camadas livres, áudio por aplicativo e controle do microfone são executados pelo MiniDeck. Minimize para manter as ações funcionando. Com as camadas ativas, fechar a janela envia o MiniDeck para a bandeja; para encerrar, use o menu do ícone.");
        Section("🔌","Como as teclas chegam ao MiniDeck","A preparação usa F13–F24 como gatilhos internos, até 12 entradas contando três por knob. No modo exclusivo, esses F físicos usados pelo layout ficam reservados em todos os teclados enquanto as camadas estão ativas; só o atalho externo da camada ou a ação escolhida é enviado. Desmarcar o modo exclusivo volta ao Raw Input filtrado pelo USB 1189:8890, deixando os gatilhos originais passar. Não é instalado driver ou serviço. Se houver dois dispositivos desse modelo, conecte somente o que deseja usar.");
        Section("Aa","Nomes aceitos na gravação direta","A–Z, 0–9, F1–F24; Enter, Esc, Backspace, Tab, Space; CapsLock, PrintScreen, ScrollLock, Pause; Insert, Delete, Home, End, PageUp, PageDown, Left, Right, Up, Down.\nModificadores: Ctrl, Shift, Alt, Win, RCtrl, RShift, RAlt, RWin.\nNum0–Num9, NumLock, NumDivide, NumMultiply, NumSubtract, NumAdd, NumEnter, NumDecimal, Menu.\nPontuação por posição USB: Minus, Equal, LBracket, RBracket, Backslash, NonUSHash, Semicolon, Apostrophe, Grave, Comma, Period, Slash, NonUSBackslash. O caractere resultante depende do layout PT-BR configurado no Windows. Use ; para separar até 5 passos.");

        Group("Problemas e segurança");
        Section("🛠","Se uma tecla não funcionar","Confira o cabo USB, o layout escolhido e se Camadas ativas aparece no topo. Feche o programa original antes de gravar. Se outro programa tiver reconfigurado o teclado, ajuste o layout e prepare novamente. Atalhos enviados a programas executados como administrador podem ser bloqueados pelo Windows. Não é necessário usar administrador para o funcionamento normal.\nAlguns aplicativos só reconhecem o atalho quando a janela deles está na frente (em primeiro plano). Se um atalho funciona pelo teclado normal só com o programa aberto na frente, clique nele antes de apertar a tecla do MiniDeck.","#FF9C8A");
        Section("💾","Perfis, backup e reversão","Os perfis ficam em "+store.Folder+". O arquivo perfil.json.bak guarda a versão local anterior. Exportar e importar trabalham com o perfil do MiniDeck; não leem a configuração antiga do dispositivo. O executável original continua intacto na pasta Release e pode reprogramar o teclado a qualquer momento. As alterações de hardware precisam ser regravadas para serem revertidas.");
    }
}
