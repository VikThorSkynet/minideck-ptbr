using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using HidSharp;

namespace MiniDeck;

// Payload bytes below are reproduced from FormMain.Download_Click, Send_SwLayer,
// Send_WriteFlash_Cmd and Send_WriteFlashLED_Cmd in the supplied executable.
public static class Protocol
{
    public static byte[] Payload(params byte[] b) { var p=new byte[8]; Array.Copy(b,p,b.Length); return p; }
    public static IEnumerable<byte[]> Keyboard(int slot,int layer,IReadOnlyList<(byte Mod,byte Usage)> keys,byte reportId=3)
    {
        if(slot<1||slot>18 || layer<1||layer>3 || keys.Count<1||keys.Count>5) throw new ArgumentOutOfRangeException("Tecla, camada ou sequência inválida (máximo: 5 teclas).");
        if(reportId!=0) yield return Payload(0xA1,(byte)layer);
        byte type=(byte)((reportId==0?0:layer<<4)|1);
        yield return Payload((byte)slot,type,(byte)keys.Count,0,keys[0].Mod,0);
        for(int i=0;i<keys.Count;i++) yield return Payload((byte)slot,type,(byte)keys.Count,(byte)(i+1),keys[i].Mod,keys[i].Usage);
        yield return Payload(0xAA,0xAA);
    }
    public static IEnumerable<byte[]> Simple(int slot,int layer,byte type,byte[] data,byte reportId=3)
    {
        if(slot<1||slot>18||layer<1||layer>3||!(type==2||type==3)) throw new ArgumentOutOfRangeException();
        if(reportId!=0) yield return Payload(0xA1,(byte)layer);
        var p=Payload((byte)slot,(byte)((reportId==0?0:layer<<4)|type)); Array.Copy(data,0,p,2,Math.Min(data.Length,5)); yield return p;
        yield return Payload(0xAA,0xAA);
    }
    public static IEnumerable<byte[]> Led(int layer,int mode,byte reportId=3)
    {
        if(layer<1||layer>3||mode<0||mode>2) throw new ArgumentOutOfRangeException();
        if(reportId!=0) yield return Payload(0xA1,(byte)layer);
        yield return Payload(0xB0,(byte)((reportId==0?0:layer<<4)|8),(byte)mode);
        yield return Payload(0xAA,0xA1);
    }
    public static byte[] Frame(byte reportId,byte[] payload,int length)
    {
        if(length<9||payload.Length!=8) throw new InvalidDataException("Tamanho de relatório HID incompatível.");
        var frame=new byte[length]; frame[0]=reportId; Array.Copy(payload,0,frame,1,8); return frame;
    }
}
public sealed class KeyboardDevice : IDisposable
{
    private HidStream? stream;
    public HidDevice Device { get; }
    public byte ReportId { get; private set; } = 3;
    public KeyboardDevice(HidDevice device) { Device=device; }
    public static List<HidDevice> Find() => DeviceList.Local.GetHidDevices(0x1189,0x8890)
        .Where(d=>d.DevicePath.Contains("mi_01",StringComparison.OrdinalIgnoreCase)&&d.GetMaxOutputReportLength()>=9).ToList();
    public static object[] Inspect() => DeviceList.Local.GetHidDevices(0x1189,0x8890).Select(d=>new {
        Path=d.DevicePath, Input=d.GetMaxInputReportLength(), Output=d.GetMaxOutputReportLength(), Feature=d.GetMaxFeatureReportLength(),Descriptor=Descriptor(d)
    }).Cast<object>().ToArray();
    private static string Descriptor(HidDevice d) {try{return Convert.ToHexString(d.GetRawReportDescriptor());}catch(Exception e){return "Indisponível: "+e.Message;}}
    public void Connect()
    {
        if(stream!=null) return;
        stream=Device.Open(); stream.WriteTimeout=600;
        // Original checks 3, 0, then 2 with the all-zero, non-persistent probe.
        foreach(byte id in new byte[]{3,0,2}) {
            try { Write(id,Protocol.Payload()); ReportId=id; return; }
            catch(IOException) { }
            catch(TimeoutException) { }
        }
        Dispose(); throw new IOException("O teclado não aceitou nenhum dos relatórios conhecidos (3, 0, 2).");
    }
    private void Write(byte id,byte[] payload)
    {
        if(stream==null) throw new InvalidOperationException("Conecte o teclado antes de gravar.");
        stream.Write(Protocol.Frame(id,payload,Device.GetMaxOutputReportLength()));
    }
    public void Send(IEnumerable<byte[]> packets)
    {
        Connect(); foreach(var p in packets) { Write(ReportId,p); Thread.Sleep(8); }
    }
    public void ProgramBridge(DeckConfig config,Action<int,int>? progress=null)
    {
        Connect(); var slots=config.Slots().ToList(); int done=0;
        for(int layer=1;layer<=(ReportId==0?1:3);layer++) foreach(var slot in slots) {
            Send(Protocol.Keyboard(slot.Id,layer,new[]{((byte)0,config.TriggerUsage(slot.Id))},ReportId));
            progress?.Invoke(++done,slots.Count*(ReportId==0?1:3));
        }
    }
    public void Dispose() { stream?.Dispose(); stream=null; }
}
