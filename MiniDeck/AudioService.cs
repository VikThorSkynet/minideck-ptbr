using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NAudio.CoreAudioApi;

namespace MiniDeck;

public sealed class AudioService
{
    public List<Choice> Devices(bool capture)
    {
        using var en=new MMDeviceEnumerator();
        var list=new List<Choice>{new("",capture?"Microfone padrão de comunicação":"Saída padrão do Windows")};
        foreach(var d in en.EnumerateAudioEndPoints(capture?DataFlow.Capture:DataFlow.Render,DeviceState.Active)) {
            using(d) list.Add(new(d.ID,d.FriendlyName));
        }
        return list;
    }
    public List<Choice> Applications()
    {
        var names=new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        VisitSessions((s,name)=>{ if(name!="") names.Add(name); });
        return names.Select(n=>new Choice(n,n+".exe")).ToList();
    }
    private static string ProcessName(uint id)
    {
        if(id==0)return "";
        try { using var p=Process.GetProcessById((int)id); return p.ProcessName; } catch { return ""; }
    }
    private static void VisitSessions(Action<AudioSessionControl,string> act)
    {
        using var en=new MMDeviceEnumerator();
        foreach(var d in en.EnumerateAudioEndPoints(DataFlow.Render,DeviceState.Active)) using(d) {
            try {
                var sessions=d.AudioSessionManager.Sessions;
                for(int i=0;i<sessions.Count;i++) using(var s=sessions[i]) act(s,ProcessName(s.GetProcessID));
            } catch(System.Runtime.InteropServices.COMException) { }
        }
    }
    public string Adjust(DeckAction action)
    {
        if(action.Kind.StartsWith("app",StringComparison.Ordinal)) {
            if(string.IsNullOrWhiteSpace(action.Target)) throw new InvalidOperationException("Escolha o aplicativo desta ação.");
            string name=System.IO.Path.GetFileNameWithoutExtension(action.Target); int count=0;
            // Keep one mute target for all sessions; session volumes remain independent.
            bool? newMute=null;
            VisitSessions((s,n)=> {
                if(!n.Equals(name,StringComparison.OrdinalIgnoreCase)) return;
                if(action.Kind=="appMute") { newMute??=!s.SimpleAudioVolume.Mute; s.SimpleAudioVolume.Mute=newMute.Value; }
                else s.SimpleAudioVolume.Volume=action.Kind=="appSet"?ActionRules.Volume(action)/100f:Math.Clamp(s.SimpleAudioVolume.Volume+(action.Kind=="appUp"?1:-1)*action.Step/100f,0,1);
                count++;
            });
            if(count==0) throw new InvalidOperationException("Nenhuma sessão de áudio de "+name+". Abra o aplicativo e reproduza um som.");
            return action.Name+" · "+name;
        }
        bool mic=action.Kind.StartsWith("mic",StringComparison.Ordinal);
        using var en=new MMDeviceEnumerator();
        using var device=string.IsNullOrWhiteSpace(action.Target)?en.GetDefaultAudioEndpoint(mic?DataFlow.Capture:DataFlow.Render,mic?Role.Communications:Role.Multimedia):en.GetDevice(action.Target);
        var volume=device.AudioEndpointVolume;
        if(action.Kind.EndsWith("Mute",StringComparison.Ordinal)) volume.Mute=!volume.Mute;
        else volume.MasterVolumeLevelScalar=action.Kind.EndsWith("Set",StringComparison.Ordinal)?ActionRules.Volume(action)/100f:Math.Clamp(volume.MasterVolumeLevelScalar+(action.Kind.EndsWith("Up",StringComparison.Ordinal)?1:-1)*action.Step/100f,0,1);
        return device.FriendlyName+" · "+(volume.Mute?"mudo":Math.Round(volume.MasterVolumeLevelScalar*100)+"%");
    }
    public object Snapshot()
    {
        var endpoints=new List<object>();
        using var en=new MMDeviceEnumerator();
        foreach(var d in en.EnumerateAudioEndPoints(DataFlow.All,DeviceState.Active)) using(d) endpoints.Add(new { Name=d.FriendlyName, Id=d.ID, Flow=d.DataFlow.ToString(), Volume=d.AudioEndpointVolume.MasterVolumeLevelScalar, Mute=d.AudioEndpointVolume.Mute });
        return new { Endpoints=endpoints, Applications=Applications() };
    }
}
