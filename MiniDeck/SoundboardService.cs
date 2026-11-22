using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace MiniDeck;

public interface ISoundVoice : IDisposable
{
    float Volume {get;set;}
    event Action<Exception?>? Finished;
    void Play();
}

public static class SoundboardTemplates
{
    public static DeckLayer Create(DeckConfig config)
    {
        string name="Sons";int suffix=2;while(config.Layers.Any(l=>l.Name.Equals(name,StringComparison.OrdinalIgnoreCase)))name="Sons "+suffix++;
        var layer=new DeckLayer{Name=name,Color="#FFD166"};
        for(int i=1;i<config.KeyCount;i++)layer.Actions[i]=new(){Name="Escolher som "+i};
        layer.Actions[config.KeyCount]=new(){Name="Próxima camada",Kind="nextLayer"};
        for(int k=0;k<config.KnobCount;k++) {
            layer.Actions[13+k*3]=new(){Name="Sons −",Kind="soundVolumeDown"};
            layer.Actions[14+k*3]=new(){Name="Sons: mudo",Kind="soundMute"};
            layer.Actions[15+k*3]=new(){Name="Sons +",Kind="soundVolumeUp"};
        }
        return layer;
    }
}

// Volumes apply to decoded samples, preserving Windows endpoint and other app volumes.
public sealed class SoundboardService : IDisposable
{
    private sealed record Voice(string Key,int ClipVolume,ISoundVoice Output);
    private readonly Func<DeckConfig> config;
    private readonly Func<DeckAction,ISoundVoice> create;
    private readonly object gate=new();
    private readonly List<Voice> voices=new();
    private bool disposed;
    public event Action? Changed;
    public event Action<string>? PlaybackError;
    public int ActiveCount {get{lock(gate)return voices.Count;}}
    public SoundboardService(Func<DeckConfig> profile,Func<DeckAction,ISoundVoice>? factory=null){config=profile;create=factory??(a=>new WasapiSoundVoice(a));}
    private float Gain(int clip)=>config().SoundMuted?0:clip/100f*config().SoundMasterVolume/100f;
    public string Play(DeckAction action)
    {
        ActionRules.Validate(action);
        string key=Path.GetFullPath(action.Value)+"|"+action.Target;
        List<Voice> matching;
        lock(gate){ObjectDisposedException.ThrowIf(disposed,this);matching=voices.Where(v=>v.Key.Equals(key,StringComparison.OrdinalIgnoreCase)).ToList();}
        if(action.SoundBehavior=="toggle"&&matching.Count>0){Remove(matching);return "Som parado: "+Path.GetFileName(action.Value);}
        // Prepare successfully before interrupting an existing sound.
        var output=create(action);var voice=new Voice(key,action.SoundVolume,output);
        try {
            lock(gate) {
                ObjectDisposedException.ThrowIf(disposed,this);
                if(voices.Count-(action.SoundBehavior=="overlap"?0:matching.Count)>=16)throw new InvalidOperationException("Já há 16 sons tocando. Pare um som antes de iniciar outro.");
                output.Volume=Gain(voice.ClipVolume);output.Finished+=error=>Finished(voice,error);
            }
            if(action.SoundBehavior!="overlap")Remove(matching);
            lock(gate)voices.Add(voice);
            output.Play();Changed?.Invoke();
            return "Tocando: "+Path.GetFileName(action.Value)+(action.SoundLoop?" · em repetição":"");
        } catch {lock(gate)voices.Remove(voice);output.Dispose();Changed?.Invoke();throw;}
    }
    private void Finished(Voice voice,Exception? error)
    {
        bool removed;lock(gate)removed=voices.Remove(voice);
        if(!removed)return;
        voice.Output.Dispose();Changed?.Invoke();
        if(error!=null)PlaybackError?.Invoke("O som foi interrompido: "+error.Message);
    }
    private void Remove(IEnumerable<Voice> items)
    {
        foreach(var voice in items){bool removed;lock(gate)removed=voices.Remove(voice);if(removed)voice.Output.Dispose();}
        Changed?.Invoke();
    }
    public void StopAll(){List<Voice> all;lock(gate){all=voices.ToList();}Remove(all);}
    public void SetVolume(int percent)
    {
        if(percent<0||percent>100)throw new ArgumentOutOfRangeException(nameof(percent));
        config().SoundMasterVolume=percent;ApplyVolume();
    }
    private void ApplyVolume(){lock(gate)foreach(var voice in voices)voice.Output.Volume=Gain(voice.ClipVolume);Changed?.Invoke();}
    public string Adjust(DeckAction action)
    {
        if(action.Kind=="soundStop"){StopAll();return "Todos os sons foram parados.";}
        if(action.Kind=="soundMute"){config().SoundMuted=!config().SoundMuted;ApplyVolume();}
        else SetVolume(action.Kind=="soundVolumeSet"?ActionRules.Volume(action):Math.Clamp(config().SoundMasterVolume+(action.Kind=="soundVolumeUp"?1:-1)*action.Step,0,100));
        return "Sons · "+(config().SoundMuted?"mudo":config().SoundMasterVolume+"%");
    }
    public void Dispose(){lock(gate)disposed=true;StopAll();}
}

public sealed class LoopingWaveStream : WaveStream
{
    private readonly WaveStream source;
    public bool Loop {get;}
    public LoopingWaveStream(WaveStream input,bool loop){source=input;Loop=loop;}
    public override WaveFormat WaveFormat=>source.WaveFormat;
    public override long Length=>source.Length;
    public override long Position {get=>source.Position;set=>source.Position=value;}
    public override int Read(byte[] buffer,int offset,int count)
    {
        int total=0;bool justRewound=false;
        while(total<count) {
            int read=source.Read(buffer,offset+total,count-total);
            if(read==0){if(!Loop||justRewound)break;source.Position=0;justRewound=true;continue;}
            justRewound=false;total+=read;
        }
        return total;
    }
    protected override void Dispose(bool disposing){if(disposing)source.Dispose();base.Dispose(disposing);}
}

public sealed class WasapiSoundVoice : ISoundVoice
{
    private readonly MMDevice device;
    private readonly LoopingWaveStream stream;
    private readonly SampleChannel samples;
    private readonly WasapiOut player;
    private int disposed;
    public event Action<Exception?>? Finished;
    public float Volume {get=>samples.Volume;set=>samples.Volume=value;}
    public WasapiSoundVoice(DeckAction action)
    {
        if(!File.Exists(action.Value))throw new FileNotFoundException("O arquivo de som foi movido ou não existe. Escolha o arquivo novamente.",action.Value);
        stream=new LoopingWaveStream(new MediaFoundationReader(action.Value),action.SoundLoop);
        try {
            using var en=new MMDeviceEnumerator();
            device=string.IsNullOrWhiteSpace(action.Target)?en.GetDefaultAudioEndpoint(DataFlow.Render,Role.Multimedia):en.GetDevice(action.Target);
            try {
                samples=new SampleChannel(stream);
                player=new WasapiOut(device,AudioClientShareMode.Shared,true,100);
                try{player.Init(new SampleToWaveProvider(samples));}
                catch{player.Dispose();throw;}
                // Cleanup must not join the playback thread from inside its own event.
                player.PlaybackStopped+=(_,e)=>ThreadPool.QueueUserWorkItem(_=>Finished?.Invoke(e.Exception));
            }catch{device.Dispose();throw;}
        }catch{stream.Dispose();throw;}
    }
    public void Play()=>player.Play();
    public void Dispose()
    {
        if(Interlocked.Exchange(ref disposed,1)!=0)return;
        try{player.Dispose();}finally{stream.Dispose();device.Dispose();}
    }
}
