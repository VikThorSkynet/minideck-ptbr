using System;
using System.Collections.Generic;
using System.Linq;

namespace MiniDeck;
public static class NativeBindings
{
    public static (byte Mod,byte Usage) Chord(string text)
    {
        byte mod=0,usage=0; int keys=0;
        foreach(var item in text.Split('+',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries)) {
            string s=item.ToUpperInvariant();
            byte bit=s switch {"CTRL" or "LCTRL"=>1,"SHIFT" or "LSHIFT"=>2,"ALT" or "LALT"=>4,"WIN" or "LWIN"=>8,"RCTRL"=>16,"RSHIFT"=>32,"RALT"=>64,"RWIN"=>128,_=>0};
            if(bit!=0) mod|=bit; else { usage=Usage(s); keys++; }
        }
        if(keys>1 || (keys==0&&mod==0)) throw new FormatException("Cada passo aceita uma tecla com modificadores. Separe passos usando ;");
        return (mod,usage);
    }
    public static byte Usage(string s)
    {
        s=s.Trim().ToUpperInvariant();
        if(s.Length==1&&s[0]>='A'&&s[0]<='Z') return (byte)(4+s[0]-'A');
        if(s.Length==1&&s[0]>='1'&&s[0]<='9') return (byte)(30+s[0]-'1');
        if(s=="0") return 39;
        if(s.StartsWith("F")&&int.TryParse(s[1..],out int f)&&f>=1&&f<=24) return (byte)(f<=12?57+f:91+f);
        if(s.StartsWith("NUM")&&int.TryParse(s[3..],out int num)&&num>=0&&num<=9) return (byte)(num==0?98:88+num);
        return s switch {
            "ENTER"=>40,"ESC" or "ESCAPE"=>41,"BACKSPACE"=>42,"TAB"=>43,"SPACE" or "ESPAÇO"=>44,
            "MINUS"=>45,"EQUAL"=>46,"LBRACKET"=>47,"RBRACKET"=>48,"BACKSLASH"=>49,"NONUSHASH"=>50,"SEMICOLON"=>51,"APOSTROPHE"=>52,"GRAVE"=>53,"COMMA"=>54,"PERIOD"=>55,"SLASH"=>56,
            "CAPSLOCK"=>57,"PRINTSCREEN"=>70,"SCROLLLOCK"=>71,"PAUSE"=>72,"INSERT"=>73,"HOME"=>74,"PAGEUP"=>75,"DELETE" or "DEL"=>76,"END"=>77,"PAGEDOWN"=>78,"RIGHT"=>79,"LEFT"=>80,"DOWN"=>81,"UP"=>82,
            "NUMLOCK"=>83,"NUMDIVIDE"=>84,"NUMMULTIPLY"=>85,"NUMSUBTRACT"=>86,"NUMADD"=>87,"NUMENTER"=>88,"NUMDECIMAL"=>99,"NONUSBACKSLASH"=>100,"MENU"=>101,
            _=>throw new FormatException("Tecla não reconhecida: "+s+". Veja os nomes aceitos em Como funciona.")};
    }
    public static (byte Mod,byte Usage)[] Sequence(string text)
    {
        var parts=text.Split(';',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries);
        if(parts.Length<1||parts.Length>5) throw new FormatException("A sequência precisa ter de 1 a 5 passos.");
        return parts.Select(Chord).ToArray();
    }
    public static byte[] Media(string value,byte reportId) => (reportId,value) switch {
        (0,"play")=>new byte[]{64,0},(0,"previous")=>new byte[]{128,0},(0,"next")=>new byte[]{0,1},(0,"mute")=>new byte[]{4,0},(0,"up")=>new byte[]{2,0},(0,"down")=>new byte[]{1,0},
        (2,"play")=>new byte[]{0,4},(2,"previous")=>new byte[]{0,11},(2,"next")=>new byte[]{0,10},(2,"mute")=>new byte[]{0,1},(2,"up")=>new byte[]{64,0},(2,"down")=>new byte[]{128,0},
        (3,"play")=>new byte[]{205,0},(3,"previous")=>new byte[]{182,0},(3,"next")=>new byte[]{181,0},(3,"mute")=>new byte[]{226,0},(3,"up")=>new byte[]{233,0},(3,"down")=>new byte[]{234,0},
        _=>throw new FormatException("Este comando não aparece no protocolo original para esta versão.")};
    public static byte[] Mouse(string value) => value switch {"left"=>new byte[]{1,0,0,0,0},"right"=>new byte[]{2,0,0,0,0},"middle"=>new byte[]{4,0,0,0,0},"up"=>new byte[]{0,0,0,1,0},"down"=>new byte[]{0,0,0,255,0},"ctrlUp"=>new byte[]{0,0,0,1,1},"ctrlDown"=>new byte[]{0,0,0,255,1},"shiftUp"=>new byte[]{0,0,0,1,2},"shiftDown"=>new byte[]{0,0,0,255,2},"altUp"=>new byte[]{0,0,0,1,4},"altDown"=>new byte[]{0,0,0,255,4},_=>throw new FormatException("Comando inválido.")};
}
