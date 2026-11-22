using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;

namespace MiniDeck;

// This hook reserves physical F13–F24 globally. It cannot identify the source keyboard.
// Raw Input remains the device-filtered path when exclusive routing is disabled.
public sealed class ReservedFunctionHook : IDisposable
{
    private readonly Thread thread;
    private readonly ManualResetEventSlim ready=new();
    private readonly ReservedFunctionPolicy policy=new();
    private readonly HookProc callback;
    private Exception? startupError;
    private IntPtr hook;
    private uint threadId;
    private int active,count;
    public bool Active=>Volatile.Read(ref active)!=0;
    public bool Running=>thread.IsAlive;
    public event Action<int,bool>? Key;
    public ReservedFunctionHook()
    {
        callback=Hook;thread=new Thread(Run){IsBackground=true,Name="MiniDeck · F13–F24"};thread.Start();
        if(!ready.Wait(TimeSpan.FromSeconds(5)))throw new InvalidOperationException("O isolamento de atalhos não iniciou a tempo.");
        if(startupError!=null)throw new InvalidOperationException("Não foi possível reservar os atalhos de camada.",startupError);
    }
    public void Configure(bool enabled,int inputCount){Volatile.Write(ref count,Math.Clamp(inputCount,0,12));Volatile.Write(ref active,enabled?1:0);}
    private void Run()
    {
        try {
            threadId=GetCurrentThreadId();PeekMessage(out _,IntPtr.Zero,0,0,0);
            hook=SetWindowsHookEx(13,callback,GetModuleHandle(null),0);
            if(hook==IntPtr.Zero)throw new Win32Exception(Marshal.GetLastWin32Error());
            ready.Set();while(GetMessage(out var message,IntPtr.Zero,0,0)>0){TranslateMessage(ref message);DispatchMessage(ref message);}
        }catch(Exception e){startupError=e;ready.Set();}
        finally{Volatile.Write(ref active,0);if(hook!=IntPtr.Zero)UnhookWindowsHookEx(hook);}
    }
    private IntPtr Hook(int code,IntPtr message,IntPtr data)
    {
        if(code>=0&&message.ToInt64() is 0x100 or 0x101 or 0x104 or 0x105) {
            var key=Marshal.PtrToStructure<HookKey>(data);bool down=message.ToInt64() is 0x100 or 0x104;
            var decision=policy.Handle((int)key.Vk,down,(key.Flags&0x10)!=0,Active,Volatile.Read(ref count));
            if(decision.Notify){try{Key?.Invoke((int)key.Vk,down);}catch(InvalidOperationException){}}
            if(decision.Block)return new IntPtr(1);
        }
        return CallNextHookEx(hook,code,message,data);
    }
    public void Dispose(){Volatile.Write(ref active,0);if(threadId!=0)PostThreadMessage(threadId,0x12,UIntPtr.Zero,IntPtr.Zero);thread.Join(1500);}
    private delegate IntPtr HookProc(int code,IntPtr message,IntPtr data);
    [StructLayout(LayoutKind.Sequential)]private struct HookKey{public uint Vk,Scan,Flags,Time;public UIntPtr Extra;}
    [StructLayout(LayoutKind.Sequential)]private struct Message{public IntPtr Hwnd;public uint Id;public UIntPtr WParam;public IntPtr LParam;public uint Time;public int X,Y;public uint Private;}
    [DllImport("user32.dll",SetLastError=true,EntryPoint="SetWindowsHookExW")]private static extern IntPtr SetWindowsHookEx(int id,HookProc proc,IntPtr module,uint thread);
    [DllImport("user32.dll")]private static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")]private static extern IntPtr CallNextHookEx(IntPtr hook,int code,IntPtr message,IntPtr data);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]private static extern IntPtr GetModuleHandle(string? name);
    [DllImport("kernel32.dll")]private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll",EntryPoint="PeekMessageW")]private static extern bool PeekMessage(out Message message,IntPtr window,uint min,uint max,uint remove);
    [DllImport("user32.dll",EntryPoint="GetMessageW")]private static extern int GetMessage(out Message message,IntPtr window,uint min,uint max);
    [DllImport("user32.dll")]private static extern bool TranslateMessage(ref Message message);
    [DllImport("user32.dll",EntryPoint="DispatchMessageW")]private static extern IntPtr DispatchMessage(ref Message message);
    [DllImport("user32.dll",EntryPoint="PostThreadMessageW")]private static extern bool PostThreadMessage(uint thread,uint message,UIntPtr wParam,IntPtr lParam);
}
