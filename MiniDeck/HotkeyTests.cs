using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace MiniDeck;
public static class HotkeyTests
{
    public static int NativeSmoke(string path)
    {
        try {
            var hook=new ReservedFunctionHook();bool started=hook.Running;hook.Configure(true,0);bool enabled=hook.Active;hook.Configure(false,0);bool disabled=!hook.Active;hook.Dispose();
            Check(started&&enabled&&disabled&&!hook.Running);
            System.IO.File.WriteAllText(path,JsonSerializer.Serialize(new{Passed=1,Failed=0,Note="Hook nativo iniciou, alternou o estado e encerrou a thread. Nenhuma tecla reservada no teste (count=0); sem entrada simulada e sem escrita USB."},Catalog.Json));return 0;
        }catch(Exception e){System.IO.File.WriteAllText(path,JsonSerializer.Serialize(new{Passed=0,Failed=1,Error=e.ToString()},Catalog.Json));return 1;}
    }
    private static void Check(bool value){if(!value)throw new Exception("Falha no isolamento dos atalhos por camada.");}
    public static void Register(Action<string,Action> test)
    {
        test("Atalhos: primeira camada F13–F18, segunda F19–F24, terceira Ctrl+F13–F18",()=>{
            var c=DeckConfig.Create();
            Check(string.Join(",",Enumerable.Range(1,6).Select(s=>LayerHotkeys.Get(c,c.Layers[0],s)!.Label))=="F13,F14,F15,F16,F17,F18");
            Check(string.Join(",",Enumerable.Range(1,6).Select(s=>LayerHotkeys.Get(c,c.Layers[1],s)!.Label))=="F19,F20,F21,F22,F23,F24");
            Check(LayerHotkeys.Get(c,c.Layers[2],5)!.Label=="Ctrl+F17"&&LayerHotkeys.Get(c,c.Layers[2],6)!.Label=="Ctrl+F18");
        });
        test("Atalhos: 96 combinações exclusivas sem F25 nem reutilização ao exceder o limite",()=>{
            var c=DeckConfig.Create();for(int i=c.Layers.Count;i<100;i++)c.Layers.Add(new());
            var all=c.Layers.SelectMany(l=>Enumerable.Range(1,6).Select(s=>LayerHotkeys.Get(c,l,s))).Where(h=>h!=null).ToArray();
            Check(all.Length==96&&all.Select(h=>h!.Label).Distinct().Count()==96&&all.All(h=>h!.Function>=13&&h.Function<=24));
            Check(LayerHotkeys.Get(c,c.Layers[16],1)==null&&LayerHotkeys.Get(c,c.Layers[0],13)==null);
        });
        test("Atalhos: excluir ou reordenar camadas não troca seus bancos",()=>{
            var c=DeckConfig.Create();LayerHotkeys.EnsureBanks(c);var audio=c.Layers[1];string before=LayerHotkeys.Get(c,audio,5)!.Label;
            c.Layers.RemoveAt(0);c.Layers.Reverse();Check(LayerHotkeys.Get(c,audio,5)!.Label==before);
            c.Layers.Add(new());LayerHotkeys.EnsureBanks(c);Check(c.Layers[^1].HotkeyBank==3);
            c.ActiveLayer=c.Layers[0].Id;var loaded=ProfileStore.Parse(JsonSerializer.Serialize(c,Catalog.Json));Check(LayerHotkeys.Get(loaded,loaded.Layers.Single(l=>l.Id==audio.Id),5)!.Label==before);
        });
        test("Atalhos: perfil antigo recebe bancos sem perder nomes, ações ou camadas",()=>{
            var c=DeckConfig.Create();var json=System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(c,Catalog.Json))!;
            json.AsObject().Remove("NextHotkeyBank");json.AsObject().Remove("LayerHotkeysEnabled");foreach(var l in json["Layers"]!.AsArray())l!.AsObject().Remove("HotkeyBank");
            var loaded=ProfileStore.Parse(json.ToJsonString());Check(loaded.LayerHotkeysEnabled&&loaded.Layers[0].Actions[1].Kind=="micMute"&&loaded.Layers[1].Name=="Áudio"&&loaded.Layers[2].HotkeyBank==2);
        });
        test("Atalhos: bancos duplicados são rejeitados antes de criar colisões",()=>{
            var c=DeckConfig.Create();c.Layers[0].HotkeyBank=c.Layers[1].HotkeyBank=0;bool rejected=false;try{c.Validate();}catch(System.IO.InvalidDataException){rejected=true;}Check(rejected);
        });
        test("Atalhos: tecla fica segurada e solta a combinação original após mudar de camada",()=>{
            var events=new List<(ushort Key,bool Up)>();var c=DeckConfig.Create();var output=new HeldShortcutOutput(e=>events.AddRange(e));
            output.Press(5,LayerHotkeys.Get(c,c.Layers[2],5)!);c.ActiveLayer=c.Layers[0].Id;output.Release(5);
            Check(events.SequenceEqual(new[]{((ushort)0x11,false),((ushort)0x80,false),((ushort)0x80,true),((ushort)0x11,true)}));
        });
        test("Atalhos: duas teclas compartilham Ctrl sem soltar o modificador da outra",()=>{
            var events=new List<(ushort Key,bool Up)>();var output=new HeldShortcutOutput(e=>events.AddRange(e));
            output.Press(1,new(13,1));output.Press(2,new(14,1));output.Release(1);Check(!events.Contains(((ushort)0x11,true)));
            output.ReleaseAll();Check(events.Count(e=>e.Key==0x11&&!e.Up)==1&&events.Count(e=>e.Key==0x11&&e.Up)==1);
        });
        test("Atalhos: repetição da mesma pressão não duplica envio e liberação é idempotente",()=>{
            var events=new List<(ushort Key,bool Up)>();var output=new HeldShortcutOutput(e=>events.AddRange(e));output.Press(5,new(17,0));output.Press(5,new(17,0));output.ReleaseAll();output.ReleaseAll();Check(events.Count==2&&!output.Release(5));
        });
        test("Reserva: F17 físico é bloqueado; F17 injetado e teclas normais passam",()=>{
            var policy=new ReservedFunctionPolicy();Check(policy.Handle(0x80,true,false,true,9)==(true,true));
            Check(policy.Handle(0x80,true,true,true,9)==(false,false));Check(policy.Handle(0x41,true,false,true,9)==(false,false));Check(policy.Handle(0x7B,true,false,true,9)==(false,false));
        });
        test("Atalhos: falha de envio tenta liberar teclas e modificadores já enviados",()=>{
            var events=new List<(ushort Key,bool Up)>();int calls=0;var output=new HeldShortcutOutput(keys=>{events.AddRange(keys);if(++calls==1)throw new InvalidOperationException("envio parcial");});
            try{output.Press(1,new(17,1));}catch(InvalidOperationException){}
            Check(calls==2&&events.Contains(((ushort)0x11,true))&&events.Contains(((ushort)0x80,true))&&!output.Release(1));
        });
        test("Reserva: pausa bloqueia repetições pendentes e libera a tecla já interceptada",()=>{
            var p=new ReservedFunctionPolicy();p.Handle(0x80,true,false,true,9);Check(p.Handle(0x80,true,false,false,9)==(true,false));Check(p.Handle(0x80,false,false,false,9)==(true,true));Check(p.Handle(0x80,true,false,false,9)==(false,false));
        });
        test("Reserva: tecla solta sem pressão interceptada passa sem deixar evento preso no destino",()=>{
            var p=new ReservedFunctionPolicy();Check(p.Handle(0x80,false,false,true,9)==(false,false));Check(p.Handle(0x87,true,false,true,9)==(false,false));
        });
    }
}
