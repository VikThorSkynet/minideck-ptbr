using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace MiniDeck;
public partial class CompactWindow : Window
{
    private readonly Func<DeckConfig> getConfig;
    private readonly Func<DeckLayer> getLayer;
    private readonly Action<string> select;
    private readonly Action configure,save;
    private readonly Func<DateTime> utcNow;
    private readonly Dictionary<int,Button> buttons=new();
    private readonly HashSet<int> held=new();
    private readonly Dictionary<int,DateTime> flashes=new();
    private readonly DispatcherTimer pulseTimer=new(){Interval=TimeSpan.FromMilliseconds(80)};
    private readonly DispatcherTimer saveTimer=new(){Interval=TimeSpan.FromMilliseconds(500)};
    private bool loading,initialized;
    public bool AllowClose {get;set;}
    public event Action? StopSequenceRequested;
    public event Action? StopSoundsRequested;
    public void SetSoundState(int count,int volume,bool muted){SoundStopButton.Visibility=count>0?Visibility.Visible:Visibility.Collapsed;SoundStopButton.ToolTip=$"Parar {count} som(ns) · "+(muted?"mudo":volume+"%");}
    private void SoundStopClick(object sender,RoutedEventArgs e)=>StopSoundsRequested?.Invoke();
    public void SetSequenceRunning(bool running)=>StopButton.Visibility=running?Visibility.Visible:Visibility.Collapsed;
    private void StopClick(object sender,RoutedEventArgs e)=>StopSequenceRequested?.Invoke();
    public string DisplayedLayer=>LayerText.Text;
    public string LastActivity=>LastText.Text;
    public int? LastPressedSlot {get;private set;}
    public string LastPressedLabel=>LastKeyText.Text;
    public CompactWindow(Func<DeckConfig> config,Func<DeckLayer> layer,Action<string> selectLayer,Action openConfiguration,Action saveSettings,Func<DateTime>? clock=null)
    {
        getConfig=config;getLayer=layer;select=selectLayer;configure=openConfiguration;save=saveSettings;utcNow=clock??(()=>DateTime.UtcNow);
        InitializeComponent();var settings=config().Compact;Width=settings.Width;Height=settings.Height;Topmost=settings.Topmost;PinBox.IsChecked=Topmost;
        var work=new Rect(SystemParameters.VirtualScreenLeft,SystemParameters.VirtualScreenTop,SystemParameters.VirtualScreenWidth,SystemParameters.VirtualScreenHeight);
        var position=CompactPlacement.Place(settings,work);Left=position.X;Top=position.Y;
        Refresh();initialized=true;
        pulseTimer.Tick+=(_,_)=>PaintButtons();pulseTimer.Start();
        saveTimer.Tick+=(_,_)=>{saveTimer.Stop();Remember();save();};
        LocationChanged+=(_,_)=>QueueSave();SizeChanged+=(_,_)=>QueueSave();
        Closing+=(_,e)=>{Remember();saveTimer.Stop();save();if(!AllowClose){e.Cancel=true;Hide();}else{pulseTimer.Stop();}};
    }
    public void Refresh()
    {
        loading=true;var c=getConfig();var layer=getLayer();
        MinHeight=315+Math.Max(0,(int)Math.Ceiling(c.KeyCount/3d)-2)*50+Math.Max(0,c.KnobCount-1)*50;
        LayerText.Text=layer.Name;LayerText.ToolTip=layer.Name;var accent=Ui.Brush(layer.Color);LayerText.Foreground=Ui.Luminance(layer.Color)>0.22?accent:Ui.Brush("#EAF0F7");OuterEdge.BorderBrush=accent;
        LayerPicker.ItemsSource=c.Layers.ToList();LayerPicker.SelectedValue=layer.Id;
        CompactKeys.Children.Clear();CompactKnobs.Children.Clear();buttons.Clear();CompactKeys.Columns=c.KeyCount<3?c.KeyCount:3;
        foreach(var slot in c.Slots().Where(s=>s.Id<=12))CompactKeys.Children.Add(MakeButton(slot,layer,false));
        for(int k=0;k<c.KnobCount;k++) {
            var grid=new UniformGrid{Columns=3};
            foreach(var slot in c.Slots().Where(s=>s.Id>=13+k*3&&s.Id<=15+k*3))grid.Children.Add(MakeButton(slot,layer,true));
            CompactKnobs.Children.Add(grid);
        }
        loading=false;PaintButtons();
    }
    private Button MakeButton(ControlSlot slot,DeckLayer layer,bool knob)
    {
        var a=layer.Actions.GetValueOrDefault(slot.Id)??new DeckAction();
        var content=new StackPanel();
        string caption=knob?((slot.Id-13)%3) switch{0=>"↶ Esquerda",1=>"Clique",_=>"↷ Direita"}:"Tecla "+slot.Id;
        content.Children.Add(new TextBlock{Text=caption,Tag=caption,FontSize=9,Foreground=Ui.Brush("#9CB0C5"),HorizontalAlignment=HorizontalAlignment.Center});
        content.Children.Add(new TextBlock{Text=a.Name,FontSize=12,FontWeight=FontWeights.SemiBold,TextAlignment=TextAlignment.Center,TextWrapping=TextWrapping.Wrap,TextTrimming=TextTrimming.CharacterEllipsis,MaxHeight=34,Margin=new Thickness(0,3,0,0)});
        var b=new Button{Content=content,ToolTip=slot.Name+" · "+a.Name+"\n"+ActionRules.Describe(a),Name="CompactKey"+slot.Id};
        AutomationProperties.SetName(b,slot.Name+": "+a.Name);AutomationProperties.SetAutomationId(b,b.Name);
        b.Click+=(_,_)=>SetActivity(slot.Name+" · "+a.Name+" · "+ActionRules.Describe(a));buttons[slot.Id]=b;return b;
    }
    public void SetRuntimeStatus(string connection,bool enabled,bool prepared)
    {
        bool connected=connection.Contains("conectado",StringComparison.OrdinalIgnoreCase)&&!connection.Contains("desconectado",StringComparison.OrdinalIgnoreCase);
        ModeText.Text=!connected?"○ Sem conexão":enabled?"● Ações ativas":prepared?"Ⅱ Ações pausadas":"○ Preparar teclado";
        ModeText.Foreground=Ui.Brush(connected&&enabled?"#68DFC3":"#E7BA75");
    }
    public void SetActivity(string message){LastText.Text=message;LastText.ToolTip=message;}
    public void Pulse(int slot,bool down)
    {
        if(!buttons.ContainsKey(slot))return;
        if(down){held.Add(slot);LastPressedSlot=slot;}
        else if(!held.Remove(slot))return;
        flashes[slot]=utcNow().AddSeconds(2);PaintButtons();
    }
    public bool IsHighlighted(int slot)=>held.Contains(slot)||flashes.TryGetValue(slot,out var until)&&until>utcNow();
    public void ReleaseAll(){held.Clear();flashes.Clear();PaintButtons();}
    private void PaintButtons()
    {
        // The input signal stays bright even when the layer uses a dark custom color.
        const string signal="#FFD166",pressedInk="#111A27";
        var last=getConfig().Slots().FirstOrDefault(s=>s.Id==LastPressedSlot);
        if(last==null)LastPressedSlot=null;
        LastKeyText.Text=last==null?"":"Última: "+last.Name;LastKeyText.ToolTip=LastKeyText.Text;
        LastKeyText.Visibility=last==null?Visibility.Collapsed:Visibility.Visible;
        foreach(var item in buttons) {
            bool active=IsHighlighted(item.Key),latest=item.Key==LastPressedSlot;item.Value.Background=Ui.Brush(active?signal:"#1D2A3D");
            item.Value.Foreground=Ui.Brush(active?pressedInk:"#EAF0F7");
            if(item.Value.Content is StackPanel panel)foreach(var child in panel.Children.OfType<TextBlock>()) {
                bool caption=child.Tag is string;
                if(caption)child.Text=(latest?"● ":"")+child.Tag;
                child.Foreground=Ui.Brush(active?pressedInk:caption?latest?signal:"#9CB0C5":"#EAF0F7");
            }
            item.Value.BorderBrush=Ui.Brush(active||latest?signal:"#34455E");
        }
    }
    private void HeaderDrag(object sender,MouseButtonEventArgs e)
    {
        if(e.LeftButton!=MouseButtonState.Pressed)return;
        if(e.OriginalSource is DependencyObject source) {
            for(var p=source;p!=null;p=p is Visual?VisualTreeHelper.GetParent(p):LogicalTreeHelper.GetParent(p))if(p is ButtonBase)return;
        }
        try{DragMove();}catch(InvalidOperationException){}
    }
    private void ConfigureClick(object sender,RoutedEventArgs e)=>configure();
    private void HideClick(object sender,RoutedEventArgs e){Remember();Hide();save();}
    private void PinChanged(object sender,RoutedEventArgs e){if(!initialized)return;Topmost=PinBox.IsChecked==true;Remember();save();}
    private void LayerSelected(object sender,SelectionChangedEventArgs e){if(!loading&&LayerPicker.SelectedValue is string id)select(id);}
    private void QueueSave(){if(!initialized||WindowState!=WindowState.Normal)return;saveTimer.Stop();saveTimer.Start();}
    public void Remember()
    {
        if(WindowState!=WindowState.Normal)return;var s=getConfig().Compact;s.Left=Left;s.Top=Top;s.Width=Width;s.Height=Height;s.Topmost=Topmost;
    }
}
public static class CompactPlacement
{
    public static Point Place(CompactSettings s,Rect desktop)
    {
        double x=s.Left??desktop.Right-s.Width-28,y=s.Top??desktop.Top+40;
        if(!double.IsFinite(x)||!double.IsFinite(y)||!desktop.IntersectsWith(new Rect(x,y,s.Width,s.Height)))return new Point(desktop.Left+28,desktop.Top+40);
        return new Point(Math.Clamp(x,desktop.Left,Math.Max(desktop.Left,desktop.Right-s.Width)),Math.Clamp(y,desktop.Top,Math.Max(desktop.Top,desktop.Bottom-s.Height)));
    }
}
