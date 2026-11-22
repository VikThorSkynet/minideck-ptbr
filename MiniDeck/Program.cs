using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Windows;

namespace MiniDeck;
internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if(args.Length>0&&args[0]=="--self-test") return SelfTests.Run(args.Length>1?args[1]:Path.Combine(AppContext.BaseDirectory,"testes.json"));
        if(args.Length>0&&args[0]=="--ui-smoke")return UiSmoke.Run(args.Length>1?args[1]:Path.Combine(AppContext.BaseDirectory,"verificacao-visual"));
        if(args.Length>0&&args[0]=="--sound-smoke")return SoundboardTests.RunPlayback(args.Length>1?args[1]:Path.Combine(AppContext.BaseDirectory,"verificacao-sons"));
        if(args.Length>0&&args[0]=="--hook-smoke")return HotkeyTests.NativeSmoke(args.Length>1?args[1]:Path.Combine(AppContext.BaseDirectory,"verificacao-atalhos.json"));
        if(args.Length>0&&args[0]=="--diagnostico") {
            string path=args.Length>1?args[1]:Path.Combine(AppContext.BaseDirectory,"diagnostico.json");
            try { File.WriteAllText(path,JsonSerializer.Serialize(new { Device=KeyboardDevice.Inspect(), Audio=new AudioService().Snapshot(), Note="Somente leitura: nenhum comando foi gravado no teclado e nenhum volume foi alterado." },Catalog.Json));return 0; }
            catch(Exception e){File.WriteAllText(path,e.ToString());return 1;}
        }
        using var mutex=new Mutex(true,"Local\\MiniDeck-1189-8890",out bool first);
        bool requestCompact=Array.Exists(args,a=>a=="--compact"),requestSettings=Array.Exists(args,a=>a=="--settings"),requestQuit=Array.Exists(args,a=>a=="--quit");
        if(!first){try{using var signal=EventWaitHandle.OpenExisting(requestQuit?"Local\\MiniDeck-Quit":requestSettings?"Local\\MiniDeck-ShowSettings":"Local\\MiniDeck-ShowCompact");signal.Set();}catch(WaitHandleCannotBeOpenedException){MessageBox.Show("O MiniDeck está iniciando. Aguarde e abra pelo ícone da bandeja.","MiniDeck");}return 0;}
        if(requestQuit)return 0;
        var app=new Application();
        app.DispatcherUnhandledException+=(_,e)=> {
            var folder=new ProfileStore().Folder; Directory.CreateDirectory(folder); File.AppendAllText(Path.Combine(folder,"erros.log"),DateTimeOffset.Now+" "+e.Exception+Environment.NewLine);
            MessageBox.Show("Não foi possível concluir a operação.\n\n"+e.Exception.Message,"MiniDeck");e.Handled=true;
        };
        try {
            var window=new MainWindow();app.MainWindow=window;
            using var showCompact=new EventWaitHandle(false,EventResetMode.AutoReset,"Local\\MiniDeck-ShowCompact");
            using var showSettings=new EventWaitHandle(false,EventResetMode.AutoReset,"Local\\MiniDeck-ShowSettings");
            using var quit=new EventWaitHandle(false,EventResetMode.AutoReset,"Local\\MiniDeck-Quit");
            var compactWait=ThreadPool.RegisterWaitForSingleObject(showCompact,(_,_)=>app.Dispatcher.BeginInvoke(window.ShowCompact),null,Timeout.Infinite,false);
            var settingsWait=ThreadPool.RegisterWaitForSingleObject(showSettings,(_,_)=>app.Dispatcher.BeginInvoke(window.ShowWindow),null,Timeout.Infinite,false);
            var quitWait=ThreadPool.RegisterWaitForSingleObject(quit,(_,_)=>app.Dispatcher.BeginInvoke(window.ShutdownForUpdate),null,Timeout.Infinite,false);
            try {
                if(requestCompact||!requestSettings&&window.StartInCompact)window.ShowCompact();else window.Show();
                return app.Run();
            }finally{compactWait.Unregister(null);settingsWait.Unregister(null);quitWait.Unregister(null);}
        }
        catch(Exception e){MessageBox.Show(e.ToString(),"Erro ao iniciar MiniDeck");return 1;}
    }
}
