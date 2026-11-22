using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace MiniDeck;
public static class SelfTests
{
    public static int Run(string path)
    {
        var results=new List<object>();int failed=0,passed=0;
        void Test(string name,Action run){try{run();passed++;results.Add(new{Name=name,Passed=true});}catch(Exception e){failed++;results.Add(new{Name=name,Passed=false,Error=e.ToString()});}}
        void Equal<T>(T actual,T expected){if(!EqualityComparer<T>.Default.Equals(actual,expected))throw new Exception($"Esperado {expected}; recebido {actual}.");}
        void Bytes(byte[] actual,params byte[] expected){if(!actual.SequenceEqual(expected))throw new Exception(Convert.ToHexString(actual)+" != "+Convert.ToHexString(expected));}
        void Throws(Action run){try{run();}catch{return;}throw new Exception("Uma entrada inválida foi aceita.");}
        Test("Ctrl+C, camada 2: quadro USB conforme Download_Click",()=>{
            var p=Protocol.Keyboard(1,2,new[]{((byte)1,(byte)6)}).ToArray();Equal(p.Length,4);
            Bytes(p[0],0xA1,2,0,0,0,0,0,0);Bytes(p[1],1,0x21,1,0,1,0,0,0);Bytes(p[2],1,0x21,1,1,1,6,0,0);Bytes(p[3],0xAA,0xAA,0,0,0,0,0,0);
            Bytes(Protocol.Frame(3,p[2],9),3,1,0x21,1,1,1,6,0,0);
        });
        Test("Versão Report ID 0 não recebe seleção ou bits de camada",()=>{var p=Protocol.Keyboard(2,1,new[]{((byte)0,(byte)4)},0).ToArray();Equal(p.Length,3);Bytes(p[1],2,1,1,1,0,4,0,0);});
        Test("Sequência de 5 passos mantém índice, ordem e modificadores",()=>{var keys=NativeBindings.Sequence("Ctrl+A;Ctrl+C;Alt+Tab;Ctrl+V;Enter");var p=Protocol.Keyboard(3,3,keys).ToArray();Equal(p.Length,8);Bytes(p[6],3,0x31,5,5,0,40,0,0);});
        Test("Sequência inválida e múltiplas teclas no mesmo passo são rejeitadas",()=>{Throws(()=>NativeBindings.Sequence("A;B;C;D;E;F"));Throws(()=>NativeBindings.Chord("A+B"));Throws(()=>NativeBindings.Chord(""));});
        Test("Limites de camada e entrada",()=>{Throws(()=>Protocol.Keyboard(0,1,new[]{((byte)0,(byte)4)}).ToArray());Throws(()=>Protocol.Keyboard(1,4,new[]{((byte)0,(byte)4)}).ToArray());Throws(()=>Protocol.Led(1,3).ToArray());Throws(()=>Protocol.Frame(3,Protocol.Payload(),8));});
        Test("LED usa B0 e commit AA A1, conforme original",()=>{var p=Protocol.Led(3,2).ToArray();Bytes(p[0],0xA1,3,0,0,0,0,0,0);Bytes(p[1],0xB0,0x38,2,0,0,0,0,0);Bytes(p[2],0xAA,0xA1,0,0,0,0,0,0);});
        Test("Volume e mídia nas três versões do original",()=>{Bytes(NativeBindings.Media("up",3),233,0);Bytes(NativeBindings.Media("up",2),64,0);Bytes(NativeBindings.Media("up",0),2,0);Bytes(NativeBindings.Media("mute",0),4,0);Bytes(NativeBindings.Media("next",0),0,1);Throws(()=>NativeBindings.Media("stop",3));});
        Test("Mouse: rolagem negativa e clique central",()=>{Bytes(NativeBindings.Mouse("down"),0,0,0,255,0);Bytes(NativeBindings.Mouse("middle"),4,0,0,0,0);var p=Protocol.Simple(13,1,3,NativeBindings.Mouse("down")).ToArray();Bytes(p[1],13,0x13,0,0,0,255,0,0);});
        Test("F13-F24 e modificadores direitos",()=>{Equal(NativeBindings.Usage("F13"),(byte)104);Equal(NativeBindings.Usage("F24"),(byte)115);Equal(NativeBindings.Chord("RCtrl+RAlt+Delete"),((byte)80,(byte)76));Equal(NativeBindings.Usage("Num0"),(byte)98);});
        Test("6 teclas + 2 knobs são 12 gatilhos sem colisão",()=>{var c=DeckConfig.Create();c.KnobCount=2;var slots=c.Slots().ToArray();Equal(slots.Length,12);Equal(slots.Select(s=>c.TriggerVk(s.Id)).Distinct().Count(),12);Equal(c.TriggerUsage(13),(byte)110);Equal(c.TriggerName(18),"F24");c.KeyCount=12;Throws(()=>c.TriggerUsage(13));});
        Test("Filtro aceita apenas o VID/PID do mini teclado",()=>{Equal(RawKeyboard.IsMini(@"\\?\HID#vid_1189&pid_8890&mi_00"),true);Equal(RawKeyboard.IsMini("HID#VID_258A&PID_0049"),false);Equal(RawKeyboard.IsMini("HID#VID_1189&PID_8888"),false);});
        Test("Camada seguinte, anterior e retorno circular",()=>{var c=DeckConfig.Create();var en=new LayerEngine(c);en.Handle(6,true);Equal(en.Current.Name,"Áudio");en.Handle(6,true);Equal(en.Current.Name,"Atalhos");en.Handle(6,true);Equal(en.Current.Name,"Principal");en.Current.Actions[1]=new(){Kind="previousLayer"};en.Handle(1,true);Equal(en.Current.Name,"Atalhos");});
        Test("Soltar tecla momentânea restaura a camada mesmo com ação diferente no destino",()=>{var c=DeckConfig.Create();var en=new LayerEngine(c);var first=en.Current.Id;en.Current.Actions[1]=new(){Kind="holdLayer",Target=c.Layers[1].Id};en.Handle(1,true);Equal(en.Current.Name,"Áudio");en.Handle(1,false);Equal(en.Current.Id,first);});
        Test("Camadas momentâneas aninhadas, liberação fora de ordem e desconexão",()=>{var c=DeckConfig.Create();var en=new LayerEngine(c);c.Layers[0].Actions[1]=new(){Kind="holdLayer",Target=c.Layers[1].Id};c.Layers[1].Actions[2]=new(){Kind="holdLayer",Target=c.Layers[2].Id};en.Handle(1,true);en.Handle(2,true);en.Handle(1,false);Equal(en.Current.Name,"Atalhos");en.Handle(2,false);Equal(en.Current.Name,"Principal");en.Handle(1,true);en.ReleaseAll();Equal(en.Current.Name,"Principal");});
        Test("Ações de áudio são entregues ao executor sem mudar camada",()=>{var c=DeckConfig.Create();var en=new LayerEngine(c);Equal(en.Handle(1,true)!.Kind,"micMute");Equal(en.Handle(1,false),null);});
        Test("Perfil JSON faz ida e volta e rejeita configuração inválida",()=>{var c=DeckConfig.Create();c.Layers[0].Actions[1].Target="microfone-pt-br";var loaded=ProfileStore.Parse(JsonSerializer.Serialize(c,Catalog.Json));Equal(loaded.Layers[0].Actions[1].Target,"microfone-pt-br");loaded.KeyCount=13;Throws(loaded.Validate);Throws(()=>ProfileStore.Parse("{\"Version\":42}"));});
        Test("Salvar perfil preserva versão anterior",()=>{var folder=Path.Combine(Path.GetTempPath(),"MiniDeck-test-"+Guid.NewGuid().ToString("N"));var store=new ProfileStore(folder);var c=DeckConfig.Create();store.Save(c);c.Layers[0].Name="Alterado";store.Save(c);Equal(store.Load().Layers[0].Name,"Alterado");Equal(ProfileStore.Parse(File.ReadAllText(store.FilePath+".bak")).Layers[0].Name,"Principal");File.Delete(store.FilePath);File.Delete(store.FilePath+".bak");Directory.Delete(folder);});
        Test("Atalhos de software: F24, Ctrl e navegação",()=>{Equal(InputSender.Vk("F24"),(ushort)135);Equal(InputSender.Vk("Ctrl"),(ushort)17);Equal(InputSender.Vk("PageDown"),(ushort)34);Throws(()=>InputSender.Vk("não-existe"));});
        Test("Atalhos enviados levam o scan code físico (Ctrl, F12, F16, PageDown)",()=>{Equal(InputSender.ScanCode(0x11),(ushort)0x1D);Equal(InputSender.ScanCode(0x7B),(ushort)0x58);Equal(InputSender.ScanCode(0x7F),(ushort)0x67);Equal(InputSender.ScanCode(0x22),(ushort)0x51);});
        Test("Captura de atalhos: teclado numérico, pontuação e modificadores direitos",()=>{Equal(InputSender.Vk("Num3"),(ushort)99);Equal(InputSender.Vk("NumPad3"),(ushort)99);Equal(InputSender.Vk("Oemplus"),(ushort)187);Equal(InputSender.Vk("RCtrl"),(ushort)163);Equal(InputSender.Vk("RAlt"),(ushort)165);});
        Test("Volume absoluto permite zero e cem, rejeita limites inválidos",()=>{Equal(ActionRules.Volume(new(){Kind="outputSet",Value="0"}),0);Equal(ActionRules.Volume(new(){Kind="appSet",Value="100"}),100);Throws(()=>ActionRules.Volume(new(){Value="101"}));Throws(()=>ActionRules.Validate(new(){Kind="micSet",Value="-1"}));});
        Test("Sequência executa passos na ordem e respeita repetições",()=>{
            using var runner=new SequenceRunner();var order=new List<string>();int progress=0;
            var macro=new DeckAction{Kind="sequence",Repeat=2,Steps=new(){new(){Kind="text",Value="A"},new(){Kind="delay",DelayMs=1},new(){Kind="text",Value="B"}}};
            runner.Run(macro,(a,t)=>{order.Add(a.Value);return System.Threading.Tasks.Task.CompletedTask;},(done,total)=>progress=done).GetAwaiter().GetResult();
            Equal(string.Join("",order),"ABAB");Equal(progress,6);Equal(runner.Running,false);
        });
        Test("Cancelar sequência interrompe a espera e impede o próximo passo",()=>{
            using var runner=new SequenceRunner();bool executed=false;
            var macro=new DeckAction{Kind="sequence",Steps=new(){new(){Kind="delay",DelayMs=60000},new(){Kind="text",Value="Não executar"}}};
            Throws(()=>runner.Run(macro,(a,t)=>{executed=true;return System.Threading.Tasks.Task.CompletedTask;},(done,total)=>{if(done==1)runner.Cancel();}).GetAwaiter().GetResult());
            Equal(executed,false);Equal(runner.Running,false);
        });
        Test("Sequências inválidas, aninhadas ou com repetição excessiva são rejeitadas",()=>{Throws(()=>ActionRules.Validate(new(){Kind="sequence"}));Throws(()=>ActionRules.Validate(new(){Kind="sequence",Repeat=101,Steps=new(){new(){Kind="delay"}}}));Throws(()=>ActionRules.Validate(new(){Kind="sequence",Steps=new(){new(){Kind="sequence",Steps=new(){new(){Kind="delay"}}}}}));});
        Test("Duplicar uma ação copia os passos sem alterar o original",()=>{var a=new DeckAction{Kind="sequence",Steps=new(){new(){Name="Original",Kind="delay"}}};var b=a.Copy();b.Steps[0].Name="Alterado";Equal(a.Steps[0].Name,"Original");});
        Test("Perfil novo preserva argumentos, macros e painel compacto",()=>{
            var c=DeckConfig.Create();c.Compact=new(){Left=-1200,Top=120,Width=360,Height=300,Topmost=false,OpenOnStart=true};c.Layers[0].Color="#112233";
            c.Layers[0].Actions[3]=new(){Kind="program",Value="example.exe",Arguments="--teste 123",WorkingDirectory="C:\\"};c.Layers[0].Actions[4]=new(){Kind="sequence",Repeat=3,Steps=new(){new(){Kind="appSet",Target="chrome",Value="0"},new(){Kind="delay",DelayMs=123}}};
            var read=ProfileStore.Parse(JsonSerializer.Serialize(c,Catalog.Json));Equal(read.Layers[0].Actions[3].Arguments,"--teste 123");Equal(read.Layers[0].Actions[4].Steps[1].DelayMs,123);Equal(read.Compact.Left,(double?)-1200);Equal(read.Compact.Topmost,false);Equal(read.Layers[0].Color,"#112233");
        });
        Test("Perfis antigos continuam legíveis sem perder ações",()=>{
            var c=DeckConfig.Create();var json=System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(c,Catalog.Json))!;json.AsObject().Remove("Compact");foreach(var l in json["Layers"]!.AsArray())l!.AsObject().Remove("Color");var read=ProfileStore.Parse(json.ToJsonString());Equal(read.Layers[0].Actions[2].Kind,"media");Equal(read.Layers[0].Actions[2].Value,"play");Equal(read.Compact.Width,420d);
        });
        Test("Painel aceita segundo monitor à esquerda e recupera posição fora da tela",()=>{
            var desktop=new System.Windows.Rect(-1920,0,3840,1080);var point=CompactPlacement.Place(new(){Left=-1700,Top=100,Width=420,Height=345},desktop);Equal(point.X,-1700d);
            point=CompactPlacement.Place(new(){Left=9000,Top=9000,Width=420,Height=345},desktop);Equal(desktop.Contains(point),true);
        });
        Test("LED aceita exatamente os três modos recuperados",()=>{for(int mode=0;mode<3;mode++){var action=new DeckAction{Kind="led",Target="1",Value=mode.ToString()};ActionRules.Validate(action);Equal(Protocol.Led(1,mode).ToArray()[1][2],(byte)mode);}Throws(()=>ActionRules.Validate(new(){Kind="led",Target="1",Value="3"}));});
        Test("Últimos modos de LED enviados são preservados no perfil",()=>{var c=DeckConfig.Create();c.LedModes[1]=2;c.LedModes[3]=0;var loaded=ProfileStore.Parse(JsonSerializer.Serialize(c,Catalog.Json));Equal(loaded.LedModes[1],2);Equal(loaded.LedModes[3],0);});
        Test("Referência a camada inexistente é detectada também dentro de macros",()=>{var c=DeckConfig.Create();c.Layers[0].Actions[1]=new(){Kind="sequence",Steps=new(){new(){Kind="layer",Target="inexistente"}}};Throws(c.Validate);});
        Test("Destaque continua legível com cores claras, escuras ou vermelhas",()=>{Equal(Ui.InkFor("#FFFFFF"),"#000000");Equal(Ui.InkFor("#000000"),"#FFFFFF");Equal(Ui.InkFor("#FF0000"),"#000000");});
        SoundboardTests.Register(Test);
        HotkeyTests.Register(Test);
        File.WriteAllText(path,JsonSerializer.Serialize(new { Passed=passed, Failed=failed, Tests=results, Note="Sem gravação USB, sem entrada simulada e sem alteração de áudio." },Catalog.Json));return failed==0?0:1;
    }
}
