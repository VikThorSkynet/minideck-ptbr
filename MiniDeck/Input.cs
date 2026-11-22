using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Interop;

namespace MiniDeck;

public sealed class RawKeyboard : IDisposable
{
    private readonly HwndSource source;
    private readonly Dictionary<IntPtr,string> names=new();
    private readonly HashSet<(IntPtr,int)> held=new();
    public event Action<int,bool>? Key;
    public RawKeyboard(IntPtr hwnd)
    {
        source=HwndSource.FromHwnd(hwnd)??throw new InvalidOperationException("Janela indisponível.");
        var devices=new[]{new RawDevice { UsagePage=1, Usage=6, Flags=0x100|0x2000, Target=hwnd }};
        if(!RegisterRawInputDevices(devices,1,(uint)Marshal.SizeOf<RawDevice>())) throw new Win32Exception();
        source.AddHook(Hook);
    }
    private IntPtr Hook(IntPtr hwnd,int msg,IntPtr wParam,IntPtr lParam,ref bool handled)
    {
        if(msg==0xFE) { names.Clear(); held.Clear(); return IntPtr.Zero; }
        if(msg!=0xFF) return IntPtr.Zero;
        uint size=0, head=(uint)Marshal.SizeOf<Header>();
        GetRawInputData(lParam,0x10000003,IntPtr.Zero,ref size,head);
        if(size<head+16||size>65536) return IntPtr.Zero;
        var ptr=Marshal.AllocHGlobal((int)size);
        try {
            if(GetRawInputData(lParam,0x10000003,ptr,ref size,head)!=size) return IntPtr.Zero;
            var header=Marshal.PtrToStructure<Header>(ptr);
            if(header.Type!=1 || header.Device==IntPtr.Zero) return IntPtr.Zero;
            if(!names.TryGetValue(header.Device,out var name)) {
                uint count=0; GetRawInputDeviceInfo(header.Device,0x20000007,null,ref count);
                var sb=new StringBuilder((int)count+1); GetRawInputDeviceInfo(header.Device,0x20000007,sb,ref count);
                names[header.Device]=name=sb.ToString();
            }
            if(!IsMini(name)) return IntPtr.Zero;
            var kb=Marshal.PtrToStructure<Keyboard>(IntPtr.Add(ptr,(int)head));
            if(kb.VKey==255) return IntPtr.Zero;
            bool down=(kb.Flags&1)==0;
            var key=(header.Device,(int)kb.VKey);
            if(down) { if(!held.Add(key)) return IntPtr.Zero; } else held.Remove(key);
            Key?.Invoke(kb.VKey,down);
        } finally { Marshal.FreeHGlobal(ptr); }
        // DefWindowProc must still process foreground WM_INPUT cleanup.
        return IntPtr.Zero;
    }
    public static bool IsMini(string name) => name.Contains("VID_1189&PID_8890",StringComparison.OrdinalIgnoreCase);
    public void Dispose()
    {
        source.RemoveHook(Hook);
        RegisterRawInputDevices(new[]{new RawDevice{UsagePage=1,Usage=6,Flags=1,Target=IntPtr.Zero}},1,(uint)Marshal.SizeOf<RawDevice>());
    }
    [StructLayout(LayoutKind.Sequential)] private struct RawDevice { public ushort UsagePage,Usage; public uint Flags; public IntPtr Target; }
    [StructLayout(LayoutKind.Sequential)] private struct Header { public uint Type,Size; public IntPtr Device,WParam; }
    [StructLayout(LayoutKind.Sequential)] private struct Keyboard { public ushort MakeCode,Flags,Reserved,VKey; public uint Message,Extra; }
    [DllImport("user32.dll",SetLastError=true)] private static extern bool RegisterRawInputDevices(RawDevice[] devices,uint count,uint size);
    [DllImport("user32.dll")] private static extern uint GetRawInputData(IntPtr raw,uint cmd,IntPtr data,ref uint size,uint header);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] private static extern uint GetRawInputDeviceInfo(IntPtr device,uint cmd,StringBuilder? data,ref uint size);
}

public sealed class LayerEngine
{
    public DeckConfig Config { get; private set; }
    private readonly List<(int Slot,string Layer)> holds=new();
    public event Action? Changed;
    public LayerEngine(DeckConfig c) { Config=c; }
    public DeckLayer Current => Config.Layers.First(l=>l.Id==(holds.Count>0?holds[^1].Layer:Config.ActiveLayer));
    public void Replace(DeckConfig c) { holds.Clear(); Config=c; Changed?.Invoke(); }
    public void Select(string id) { if(!Config.Layers.Any(l=>l.Id==id)) throw new InvalidOperationException("Camada não encontrada."); Config.ActiveLayer=id; Changed?.Invoke(); }
    public DeckAction? Handle(int slot,bool down)
    {
        if(!down) { if(holds.RemoveAll(h=>h.Slot==slot)>0) Changed?.Invoke(); return null; }
        if(!Current.Actions.TryGetValue(slot,out var a)) return null;
        if(a.Kind=="holdLayer") { if(Config.Layers.Any(l=>l.Id==a.Target)) { holds.Add((slot,a.Target)); Changed?.Invoke(); } return null; }
        if(a.Kind=="nextLayer"||a.Kind=="previousLayer") {
            int i=Config.Layers.IndexOf(Current), count=Config.Layers.Count;
            Select(Config.Layers[(i+(a.Kind=="nextLayer"?1:count-1))%count].Id); return null;
        }
        if(a.Kind=="layer") { Select(a.Target); return null; }
        return a;
    }
    public void ReleaseAll() { if(holds.Count==0) return; holds.Clear(); Changed?.Invoke(); }
}

public static class InputSender
{
    [StructLayout(LayoutKind.Sequential)] private struct Input { public uint Type; public InputUnion Data; }
    [StructLayout(LayoutKind.Explicit)] private struct InputUnion { [FieldOffset(0)] public KeyInput Key; [FieldOffset(0)] public MouseInput Mouse; }
    [StructLayout(LayoutKind.Sequential)] private struct KeyInput { public ushort Vk,Scan; public uint Flags,Time; public UIntPtr Extra; }
    [StructLayout(LayoutKind.Sequential)] private struct MouseInput { public int X,Y; public uint Data,Flags,Time; public UIntPtr Extra; }
    [DllImport("user32.dll",SetLastError=true)] private static extern uint SendInput(uint count,Input[] inputs,int size);
    private static void Send(IEnumerable<Input> sequence) { var inputs=sequence.ToArray(); if(SendInput((uint)inputs.Length,inputs,Marshal.SizeOf<Input>())!=inputs.Length) throw new InvalidOperationException("O Windows bloqueou a ação. Aplicativos executados como administrador podem exigir o mesmo nível de acesso."); }
    public static ushort Vk(string name)
    {
        name=name.Trim().ToUpperInvariant();
        if(name.Length==1&&char.IsAsciiLetterOrDigit(name[0])) return name[0];
        if(name.StartsWith("F")&&int.TryParse(name[1..],out int f)&&f>=1&&f<=24) return (ushort)(0x6F+f);
        if(name.StartsWith("NUM")&&name.Length==4&&char.IsAsciiDigit(name[3]))return (ushort)(0x60+name[3]-'0');
        return name switch { "CTRL" or "CONTROL"=>0x11,"SHIFT"=>0x10,"ALT"=>0x12,"WIN"=>0x5B,"RCTRL"=>0xA3,"RALT"=>0xA5,"RSHIFT"=>0xA1,"RWIN"=>0x5C,"ENTER"=>0x0D,"TAB"=>9,"ESC" or "ESCAPE"=>0x1B,"SPACE" or "ESPAÇO"=>0x20,"BACKSPACE"=>8,"DELETE" or "DEL"=>0x2E,"INSERT"=>0x2D,"HOME"=>0x24,"END"=>0x23,"PAGEUP"=>0x21,"PAGEDOWN"=>0x22,"LEFT"=>0x25,"UP"=>0x26,"RIGHT"=>0x27,"DOWN"=>0x28,_=>Enum.TryParse<System.Windows.Forms.Keys>(name,true,out var key)&&Enum.IsDefined(typeof(System.Windows.Forms.Keys),key)&&(int)key>0&&(int)key<256?(ushort)key:throw new FormatException("Tecla não reconhecida: "+name) };
    }
    private static Input K(ushort vk,bool up=false) => new(){Type=1,Data=new(){Key=new(){Vk=vk,Flags=(up?2u:0u)|(vk is >=0x21 and <=0x2E or 0x5B or 0x5C or 0xA3 or 0xA5?1u:0u)}}};
    public static void Shortcut(string text) { var keys=text.Split('+',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries).Select(Vk).ToArray(); if(keys.Length==0) throw new FormatException("Informe um atalho, como Ctrl+Shift+M."); Send(keys.Select(v=>K(v)).Concat(keys.Reverse().Select(v=>K(v,true)))); }
    public static void Media(string type) { ushort vk=type switch {"play"=>0xB3,"next"=>0xB0,"previous"=>0xB1,"stop"=>0xB2,"mute"=>0xAD,"up"=>0xAF,"down"=>0xAE,_=>throw new FormatException("Ação de mídia inválida.")}; Send(new[]{K(vk),K(vk,true)}); }
    public static void Text(string text)
    {
        // UTF-16 pairs are passed through as consecutive Unicode input events.
        Send(text.SelectMany(c=>new[]{new Input{Type=1,Data=new(){Key=new(){Scan=c,Flags=4}}},new Input{Type=1,Data=new(){Key=new(){Scan=c,Flags=6}}}}));
    }
    public static void Mouse(string type)
    {
        string? modifier=type.StartsWith("ctrl")?"Ctrl":type.StartsWith("shift")?"Shift":type.StartsWith("alt")?"Alt":null;
        if(modifier!=null) {
            bool up=type.EndsWith("Up",StringComparison.Ordinal);
            var mouse=new Input{Type=0,Data=new(){Mouse=new(){Flags=0x800,Data=up?120u:unchecked((uint)-120)}}};
            Send(new[]{K(Vk(modifier)),mouse,K(Vk(modifier),true)});return;
        }
        uint a=type switch {"left"=>2,"right"=>8,"middle"=>0x20,"up" or "down"=>0x800,_=>throw new FormatException("Ação de mouse inválida.")};
        var first=new Input{Type=0,Data=new(){Mouse=new(){Flags=a,Data=type=="down"?unchecked((uint)-120):type=="up"?120u:0}}};
        if(type=="up"||type=="down") Send(new[]{first}); else Send(new[]{first,new Input{Type=0,Data=new(){Mouse=new(){Flags=a*2}}}});
    }
}
