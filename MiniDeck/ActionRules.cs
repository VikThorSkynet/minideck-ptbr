using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MiniDeck;
public static class ActionRules
{
    public static IEnumerable<DeckAction> All(IEnumerable<DeckAction> actions)
    {
        foreach(var a in actions){yield return a;if(a.Kind=="sequence")foreach(var child in a.Steps)yield return child;}
    }
    public static void Validate(DeckAction action,int depth=0)
    {
        if(action==null || depth>3 || action.Steps==null || !Catalog.Kinds.Any(c=>c.Id==action.Kind))throw new InvalidDataException("Tipo de ação inválido.");
        if(action.Step<1||action.Step>100)throw new InvalidDataException("A variação de volume precisa estar entre 1 e 100%.");
        if(action.Repeat<1||action.Repeat>100||action.DelayMs<0||action.DelayMs>600000)throw new InvalidDataException("Use de 1 a 100 repetições e espera de até 600.000 ms.");
        if(action.Kind=="shortcut") {
            if(string.IsNullOrWhiteSpace(action.Value))throw new InvalidDataException("Informe ou capture um atalho.");
            foreach(var part in action.Value.Split('+',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries))InputSender.Vk(part);
        }
        if(action.Kind.StartsWith("app",StringComparison.Ordinal)&&string.IsNullOrWhiteSpace(action.Target))throw new InvalidDataException("Escolha ou digite o aplicativo.");
        if(action.Kind is "outputSet" or "micSet" or "appSet" or "soundVolumeSet")Volume(action);
        if(action.Kind=="soundPlay") {
            if(string.IsNullOrWhiteSpace(action.Value)||!Path.IsPathFullyQualified(action.Value)||Uri.TryCreate(action.Value,UriKind.Absolute,out var uri)&&!uri.IsFile)throw new InvalidDataException("Escolha um arquivo de áudio local pelo botão Escolher som.");
            if(action.SoundVolume<0||action.SoundVolume>100||!Catalog.SoundBehaviors.Any(c=>c.Id==action.SoundBehavior))throw new InvalidDataException("Escolha volume de 0 a 100% e o comportamento ao apertar novamente.");
        }
        if(action.Kind is "open" or "program" && string.IsNullOrWhiteSpace(action.Value))throw new InvalidDataException("Informe o programa, arquivo ou endereço.");
        if(action.Kind is "layer" or "holdLayer" && string.IsNullOrWhiteSpace(action.Target))throw new InvalidDataException("Escolha a camada de destino.");
        if(action.Kind=="media"&&!Catalog.Media.Any(c=>c.Id==action.Value))throw new InvalidDataException("Escolha o comando de mídia.");
        if(action.Kind=="mouse"&&!Catalog.Mouse.Any(c=>c.Id==action.Value))throw new InvalidDataException("Escolha o comando de mouse.");
        if(action.Kind=="led"&&(!int.TryParse(action.Value,out var mode)||mode<0||mode>2||!int.TryParse(action.Target,out var layer)||layer<1||layer>3))throw new InvalidDataException("Escolha modo de LED 0, 1 ou 2 e camada de memória 1, 2 ou 3.");
        if(action.Kind=="sequence") {
            if(action.Steps==null||action.Steps.Count<1||action.Steps.Count>100)throw new InvalidDataException("A sequência precisa ter de 1 a 100 passos.");
            foreach(var step in action.Steps) {
                if(step.Kind is "sequence" or "holdLayer")throw new InvalidDataException("Uma sequência não pode conter outra sequência nem uma camada enquanto segura.");
                Validate(step,depth+1);
            }
        }
    }
    public static int Volume(DeckAction action) => int.TryParse(action.Value,out var value)&&value>=0&&value<=100?value:throw new InvalidDataException("Informe o volume de 0 a 100%.");
    public static string Describe(DeckAction a) => a.Kind switch {
        "shortcut"=>a.Value,"program"=>Path.GetFileName(a.Value)+" "+a.Arguments,
        "sequence"=>$"{a.Steps.Count} passos · {a.Repeat} repetição(ões)","delay"=>$"Esperar {a.DelayMs} ms",
        "appUp" or "appDown" or "appMute" or "appSet"=>a.Target,
        "soundPlay"=>$"{Path.GetFileName(a.Value)} · {a.SoundVolume}%"+(a.SoundLoop?" · repetir":""),
        "outputSet" or "micSet"=>$"Volume: {a.Value}%","led"=>$"Modo {a.Value} · memória {a.Target}",
        _=>Catalog.Kinds.FirstOrDefault(c=>c.Id==a.Kind)?.Name??a.Kind
    };
}
