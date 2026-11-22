using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace MiniDeck;
public static class SoundboardTests
{
    private sealed class FakeVoice : ISoundVoice
    {
        public float Volume {get;set;}
        public int Plays,Disposals;
        public event Action<Exception?>? Finished;
        public void Play()=>Plays++;
        public void Dispose()=>Disposals++;
        public void End(Exception? error=null)=>Finished?.Invoke(error);
    }
    private static void Check(bool value){if(!value)throw new Exception("Comportamento de som inesperado.");}
    private static void Reject(Action run){try{run();}catch{return;}throw new Exception("Configuração inválida foi aceita.");}
    private static DeckAction Clip(string name="teste.wav",string behavior="restart")=>new(){Name="Som de teste",Kind="soundPlay",Value=Path.Combine(Path.GetTempPath(),name),SoundVolume=50,SoundBehavior=behavior};
    public static void Register(Action<string,Action> test)
    {
        test("Sons: perfil e cópia preservam arquivo, saída, volume, repetição e comportamento",()=>{
            var c=DeckConfig.Create();var a=Clip();a.Target="saida-virtual";a.SoundLoop=true;a.SoundBehavior="toggle";c.Layers[0].Actions[1]=a;c.SoundMasterVolume=37;c.SoundMuted=true;
            var read=ProfileStore.Parse(JsonSerializer.Serialize(c,Catalog.Json));var b=read.Layers[0].Actions[1].Copy();
            Check(b.Value==a.Value&&b.Target==a.Target&&b.SoundVolume==50&&b.SoundBehavior=="toggle"&&b.SoundLoop&&read.SoundMasterVolume==37&&read.SoundMuted);
        });
        test("Sons: perfil antigo mantém volumes padrão e ações anteriores",()=>{
            var json=System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(DeckConfig.Create(),Catalog.Json))!;json.AsObject().Remove("SoundMasterVolume");json.AsObject().Remove("SoundMuted");
            var c=ProfileStore.Parse(json.ToJsonString());Check(c.SoundMasterVolume==100&&!c.SoundMuted&&c.Layers[0].Actions[2].Kind=="media");
        });
        test("Sons: arquivo vazio, endereço web e volume inválido são rejeitados",()=>{
            foreach(var path in new[]{"","teste.mp3","https://example.com/teste.mp3"}){var a=Clip();a.Value=path;Reject(()=>ActionRules.Validate(a));}
            var bad=Clip();bad.SoundVolume=101;Reject(()=>ActionRules.Validate(bad));bad.SoundVolume=50;bad.SoundBehavior="incorreto";Reject(()=>ActionRules.Validate(bad));
            var c=DeckConfig.Create();c.SoundMasterVolume=-1;Reject(c.Validate);
        });
        test("Sons: volume individual é combinado com o geral e atualiza sons ativos",()=>{
            var c=DeckConfig.Create();c.SoundMasterVolume=40;var outputs=new List<FakeVoice>();using var s=new SoundboardService(()=>c,a=>{var v=new FakeVoice();outputs.Add(v);return v;});
            s.Play(Clip());Check(Math.Abs(outputs[0].Volume-0.2f)<0.001);s.Adjust(new(){Kind="soundVolumeSet",Value="60"});Check(Math.Abs(outputs[0].Volume-0.3f)<0.001);
            s.Adjust(new(){Kind="soundMute"});Check(outputs[0].Volume==0&&c.SoundMasterVolume==60);s.Adjust(new(){Kind="soundMute"});Check(Math.Abs(outputs[0].Volume-0.3f)<0.001);
            s.Adjust(new(){Kind="soundVolumeDown",Step=100});Check(outputs[0].Volume==0&&c.SoundMasterVolume==0);s.Adjust(new(){Kind="soundVolumeUp",Step=100});Check(c.SoundMasterVolume==100);
        });
        test("Sons: reiniciar substitui apenas o mesmo arquivo na mesma saída",()=>{
            var c=DeckConfig.Create();var outputs=new List<FakeVoice>();using var s=new SoundboardService(()=>c,a=>{var v=new FakeVoice();outputs.Add(v);return v;});
            s.Play(Clip());s.Play(Clip("outro.wav"));s.Play(Clip());Check(s.ActiveCount==2&&outputs[0].Disposals==1&&outputs[1].Disposals==0&&outputs[2].Plays==1);
            var differentOutput=Clip();differentOutput.Target="outro-dispositivo";s.Play(differentOutput);Check(s.ActiveCount==3&&outputs[2].Disposals==0);
        });
        test("Sons: tocar/parar alterna e sobrepor mantém cópias independentes",()=>{
            var c=DeckConfig.Create();var outputs=new List<FakeVoice>();using var s=new SoundboardService(()=>c,a=>{var v=new FakeVoice();outputs.Add(v);return v;});
            var a=Clip(behavior:"toggle");s.Play(a);s.Play(a);Check(s.ActiveCount==0&&outputs.Count==1&&outputs[0].Disposals==1);
            a.SoundBehavior="overlap";s.Play(a);s.Play(a);Check(s.ActiveCount==2);s.Adjust(new(){Kind="soundStop"});Check(s.ActiveCount==0&&outputs.All(v=>v.Disposals==1));
        });
        test("Sons: término e falha liberam recursos uma única vez",()=>{
            var c=DeckConfig.Create();FakeVoice? output=null;using var s=new SoundboardService(()=>c,a=>output=new());string? error=null;s.PlaybackError+=e=>error=e;
            s.Play(Clip());output!.End(new IOException("saída removida"));output.End();Check(s.ActiveCount==0&&output.Disposals==1&&error!.Contains("saída removida"));
        });
        test("Sons: falha ao abrir substituto preserva o som em andamento",()=>{
            var c=DeckConfig.Create();var output=new FakeVoice();int creates=0;using var s=new SoundboardService(()=>c,a=>++creates==1?output:throw new IOException("arquivo removido"));
            s.Play(Clip());Reject(()=>s.Play(Clip()));Check(s.ActiveCount==1&&output.Disposals==0);
        });
        test("Sons: limite de simultaneidade e parar tudo não deixam saídas abertas",()=>{
            var c=DeckConfig.Create();var outputs=new List<FakeVoice>();using var s=new SoundboardService(()=>c,a=>{var v=new FakeVoice();outputs.Add(v);return v;});
            for(int i=0;i<16;i++)s.Play(Clip(behavior:"overlap"));Reject(()=>s.Play(Clip(behavior:"overlap")));Check(s.ActiveCount==16&&outputs[16].Disposals==1);s.StopAll();Check(outputs.All(v=>v.Disposals==1));
        });
        test("Sons: repetição retorna ao começo e arquivo vazio não trava",()=>{
            using var input=new RawSourceWaveStream(new MemoryStream(new byte[]{1,2,3,4}),new WaveFormat(8000,16,1));using var loop=new LoopingWaveStream(input,true);var bytes=new byte[12];Check(loop.Read(bytes,0,12)==12&&bytes.SequenceEqual(new byte[]{1,2,3,4,1,2,3,4,1,2,3,4}));
            using var empty=new LoopingWaveStream(new RawSourceWaveStream(new MemoryStream(),new WaveFormat(8000,16,1)),true);Check(empty.Read(bytes,0,12)==0);
        });
        test("Sons: camada pronta reserva troca de camada e volume dos knobs sem alterar as anteriores",()=>{
            var c=DeckConfig.Create();string before=JsonSerializer.Serialize(c,Catalog.Json);var layer=SoundboardTemplates.Create(c);Check(JsonSerializer.Serialize(c,Catalog.Json)==before&&layer.Actions[6].Kind=="nextLayer"&&layer.Actions[13].Kind=="soundVolumeDown"&&layer.Actions[14].Kind=="soundMute"&&layer.Actions[15].Kind=="soundVolumeUp");
            c.Layers.Add(layer);c.Validate();Check(SoundboardTemplates.Create(c).Name=="Sons 2");
        });
    }

    // Exercise the actual Windows decoder and audio renderer at zero sample volume.
    public static int RunPlayback(string folder)
    {
        Directory.CreateDirectory(folder);var checks=new List<string>();
        void Pass(bool value,string text){Check(value);checks.Add(text);}
        try {
            string wav=Path.GetFullPath(Path.Combine(folder,"som-teste.wav")),mp3=Path.GetFullPath(Path.Combine(folder,"som-teste.mp3"));
            using(var writer=new WaveFileWriter(wav,new WaveFormat(44100,16,2)))for(int i=0;i<22050;i++){float sample=(float)(Math.Sin(2*Math.PI*440*i/44100)*0.1);writer.WriteSample(sample);writer.WriteSample(sample);}
            using(var input=new WaveFileReader(wav))MediaFoundationEncoder.EncodeToMp3(input,mp3,128000);
            foreach(var file in new[]{wav,mp3}) {
                using var reader=new MediaFoundationReader(file);var samples=new SampleChannel(reader){Volume=0.25f};var buffer=new float[44100];int n=samples.Read(buffer,0,buffer.Length);
                float peak=buffer.Take(n).Select(Math.Abs).DefaultIfEmpty(0).Max();Pass(n>0&&peak>0.01&&peak<0.04,"Decodifica "+Path.GetExtension(file)+" e aplica volume aos dados do áudio");
            }
            using var en=new MMDeviceEnumerator();using var endpoint=en.GetDefaultAudioEndpoint(DataFlow.Render,Role.Multimedia);float before=endpoint.AudioEndpointVolume.MasterVolumeLevelScalar;bool muted=endpoint.AudioEndpointVolume.Mute;
            var c=DeckConfig.Create();c.SoundMasterVolume=0;using var sounds=new SoundboardService(()=>c);string? error=null;sounds.PlaybackError+=e=>error=e;
            foreach(var file in new[]{wav,mp3}) {
                sounds.Play(new(){Kind="soundPlay",Value=file,Target=endpoint.ID,SoundVolume=100});
                Pass(SpinWait.SpinUntil(()=>sounds.ActiveCount==0,TimeSpan.FromSeconds(5))&&error==null,"Saída WASAPI reproduz "+Path.GetExtension(file)+" em silêncio e libera o arquivo no término");
            }
            sounds.Play(new(){Kind="soundPlay",Value=mp3,Target=endpoint.ID,SoundLoop=true,SoundVolume=100});
            Thread.Sleep(850);Pass(sounds.ActiveCount==1&&error==null,"MP3 em repetição continua após o fim do arquivo");sounds.StopAll();Pass(sounds.ActiveCount==0,"Parar tudo encerra a repetição na saída real");
            Pass(endpoint.AudioEndpointVolume.MasterVolumeLevelScalar==before&&endpoint.AudioEndpointVolume.Mute==muted,"Volume e mudo do dispositivo Windows permanecem iguais");
            File.WriteAllText(Path.Combine(folder,"resultado.json"),JsonSerializer.Serialize(new{Passed=checks.Count,Failed=0,Checks=checks,Output=endpoint.FriendlyName,Note="Sinal de teste próprio; saída real com volume de amostras em 0%. Sem alteração do perfil, do USB ou do volume do Windows."},Catalog.Json));return 0;
        }catch(Exception e){File.WriteAllText(Path.Combine(folder,"resultado.json"),JsonSerializer.Serialize(new{Passed=checks.Count,Failed=1,Checks=checks,Error=e.ToString()},Catalog.Json));return 1;}
    }
}
