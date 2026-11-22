using System;
using System.Threading;
using System.Threading.Tasks;

namespace MiniDeck;
public sealed class SequenceRunner : IDisposable
{
    private CancellationTokenSource source=new();
    public bool Running { get; private set; }
    public void Cancel() { source.Cancel(); }
    public async Task Run(DeckAction sequence,Func<DeckAction,CancellationToken,Task> execute,Action<int,int>? progress=null)
    {
        ActionRules.Validate(sequence);
        if(Running)throw new InvalidOperationException("Já há uma sequência em execução. Use Parar sequências para interrompê-la.");
        source.Dispose();source=new();var token=source.Token;Running=true;
        try {
            int done=0,total=sequence.Repeat*sequence.Steps.Count;
            for(int repeat=0;repeat<sequence.Repeat;repeat++)foreach(var action in sequence.Steps) {
                token.ThrowIfCancellationRequested();progress?.Invoke(++done,total);
                if(action.Kind=="delay")await Task.Delay(action.DelayMs,token);else await execute(action,token);
            }
        } finally {Running=false;}
    }
    public void Dispose() {source.Cancel();source.Dispose();}
}
