using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MiniDeck;

public sealed record LayerShortcut(int Function,int Modifiers)
{
    public string Label=>(Modifiers.HasFlagBits(1)?"Ctrl+":"")+(Modifiers.HasFlagBits(2)?"Alt+":"")+(Modifiers.HasFlagBits(4)?"Shift+":"")+"F"+Function;
    public ushort[] Keys=>new[]{Modifiers.HasFlagBits(1)?(ushort)0x11:(ushort)0,Modifiers.HasFlagBits(2)?(ushort)0x12:(ushort)0,Modifiers.HasFlagBits(4)?(ushort)0x10:(ushort)0,(ushort)(0x6F+Function)}.Where(k=>k!=0).ToArray();
}
internal static class ModifierBits {public static bool HasFlagBits(this int value,int flag)=>(value&flag)!=0;}

public static class LayerHotkeys
{
    // Windows has F1–F24. These 96 distinct chords use only F13–F24 and Ctrl/Alt/Shift.
    private static readonly int[] Modifiers={0,1,2,4,3,5,6,7};
    public const int Capacity=96;
    public static void EnsureBanks(DeckConfig config)
    {
        var used=new HashSet<int>();
        foreach(var layer in config.Layers.Where(l=>l.HotkeyBank>=0)) {
            if(layer.HotkeyBank>1000000||!used.Add(layer.HotkeyBank))throw new InvalidDataException("As camadas contêm bancos de atalhos inválidos ou repetidos.");
        }
        if(config.NextHotkeyBank<0||config.NextHotkeyBank>1000000||config.Layers.Any(l=>l.HotkeyBank< -1))throw new InvalidDataException("Banco de atalhos inválido.");
        int next=Math.Max(config.NextHotkeyBank,used.Count==0?0:used.Max()+1);
        foreach(var layer in config.Layers.Where(l=>l.HotkeyBank<0))layer.HotkeyBank=next++;
        config.NextHotkeyBank=next;
    }
    public static LayerShortcut? Get(DeckConfig config,DeckLayer layer,int slot)
    {
        if(slot<1||slot>config.KeyCount)return null; // Knobs retain explicitly configured actions.
        EnsureBanks(config);
        long index=(long)layer.HotkeyBank*config.KeyCount+slot-1;
        return index<Capacity?new(13+(int)(index%12),Modifiers[index/12]):null;
    }
    public static string Describe(DeckConfig config,DeckLayer layer,int slot)
    {
        if(!config.LayerHotkeysEnabled)return config.TriggerName(slot)+" · compartilhado entre camadas";
        if(slot>12)return "Knob: configure uma ação ou um atalho";
        var shortcut=Get(config,layer,slot);
        return shortcut!=null?shortcut.Label+" · atalho para outro programa":"Banco sem atalho automático; configure uma ação";
    }
}

// A held output belongs to the layer at key-down, even if the layer changes before key-up.
public sealed class HeldShortcutOutput
{
    private readonly Action<IReadOnlyList<(ushort Key,bool Up)>> send;
    private readonly Dictionary<int,ushort[]> held=new();
    private readonly Dictionary<ushort,int> references=new();
    public HeldShortcutOutput(Action<IReadOnlyList<(ushort Key,bool Up)>> sender){send=sender;}
    public void Press(int slot,LayerShortcut shortcut)
    {
        if(held.ContainsKey(slot))return;
        var keys=shortcut.Keys;var events=keys.Where(k=>!references.ContainsKey(k)).Select(k=>(k,false)).ToArray();
        held[slot]=keys;foreach(var key in keys)references[key]=references.GetValueOrDefault(key)+1;
        try{send(events);}catch{try{Release(slot);}catch{}throw;}
    }
    public bool Release(int slot)
    {
        if(!held.TryGetValue(slot,out var keys))return false;
        var events=keys.Reverse().Where(k=>references[k]==1).Select(k=>(k,true)).ToArray();
        held.Remove(slot);
        foreach(var key in keys)if(--references[key]==0)references.Remove(key);
        send(events);
        return true;
    }
    public void ReleaseAll(){Exception? error=null;foreach(int slot in held.Keys.ToArray())try{Release(slot);}catch(Exception e){error??=e;}if(error!=null)throw error;}
}

// Pure policy, shared by the native hook and regression tests.
public sealed class ReservedFunctionPolicy
{
    private readonly HashSet<int> held=new();
    public (bool Block,bool Notify) Handle(int vk,bool down,bool injected,bool active,int count)
    {
        if(injected||vk<0x7C||vk>0x87)return (false,false);
        if(!down){bool wasHeld=held.Remove(vk);return (wasHeld,wasHeld);}
        if(held.Contains(vk))return (true,false);
        if(!active||vk>=0x7C+count)return (false,false);
        return (true,held.Add(vk));
    }
}
