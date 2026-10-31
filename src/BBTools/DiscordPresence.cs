using System.Buffers.Binary;
using System.IO;
using System.IO.Pipes;
using System.Net.Sockets;
using System.Text.Json;

namespace BBTools;

// Local desktop IPC only; no token, OAuth, Discord account data or game writes.
public sealed class DiscordPresence : IDisposable
{
    public const string ApplicationId="1196921008809066616";
    private sealed record PresenceState(string Runtime,DiscordGameDetails? Details);
    private readonly CancellationTokenSource lifetime=new();
    private readonly SemaphoreSlim changed=new(0,1);
    private readonly Func<CancellationToken,Task<Stream>> connect;
    private readonly Task worker;
    private Stream? pipe;
    private PresenceState? desired;
    private long started;
    private TaskCompletionSource cleared=new(TaskCreationOptions.RunContinuationsAsynchronously);
    private volatile bool disposed;
    private string status="Waiting for a connected game.";
    public string Status=>Volatile.Read(ref status);
    public DiscordPresence(Func<CancellationToken,Task<Stream>>? connector=null)
    {connect=connector??ConnectAsync;worker=Task.Run(RunAsync);}
    public void Update(string? runtime,DiscordGameDetails? details=null)
    {
        if(disposed)return;
        lock(changed)
        {
            var next=runtime==null?null:new PresenceState(runtime,details);
            if(desired==next)return;
            if(desired==null&&runtime!=null)started=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            desired=next;
            if(runtime==null)cleared=new(TaskCreationOptions.RunContinuationsAsynchronously);
            if(changed.CurrentCount==0)changed.Release();
        }
    }
    private static async Task<Stream> ConnectAsync(CancellationToken token)
    {
        if(OperatingSystem.IsLinux())
        {
            var runtime=Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
            var roots=new[]{runtime,Path.GetTempPath(),runtime==null?null:Path.Combine(runtime,"app/com.discordapp.Discord")}
                .Where(x=>x!=null&&Path.IsPathFullyQualified(x)).Distinct();
            foreach(var root in roots)for(var i=0;i<10;i++)
            {
                var socket=new Socket(AddressFamily.Unix,SocketType.Stream,ProtocolType.Unspecified);
                using var attempt=CancellationTokenSource.CreateLinkedTokenSource(token);attempt.CancelAfter(80);
                try{await socket.ConnectAsync(new UnixDomainSocketEndPoint(Path.Combine(root!,"discord-ipc-"+i)),attempt.Token).ConfigureAwait(false);return new NetworkStream(socket,true);}
                catch(Exception ex)when(ex is SocketException or IOException or OperationCanceledException){socket.Dispose();token.ThrowIfCancellationRequested();}
                catch{socket.Dispose();throw;}
            }
            throw new IOException("Discord desktop IPC is not available.");
        }
        for(var i=0;i<10;i++)
        {
            var candidate=new NamedPipeClientStream(".",$"discord-ipc-{i}",PipeDirection.InOut,PipeOptions.Asynchronous);
            try{await candidate.ConnectAsync(80,token).ConfigureAwait(false);return candidate;}
            catch(Exception ex)when(ex is TimeoutException or IOException){candidate.Dispose();}
            catch{candidate.Dispose();throw;}
        }
        throw new IOException("Discord desktop is not available.");
    }
    internal static byte[] Activity(string? runtime,long start,string nonce,DiscordGameDetails? extra=null)
    {
        object? activity=runtime==null?null:new {
            type=0,status_display_type=2,
            details=extra?.DetailsText??"Bloodborne · BB Tools",
            state=extra?.StateText(runtime)??runtime,
            timestamps=new{start},instance=false,
            buttons=new[]{new{label="BBTools",url=AppInfo.RepositoryUrl}}
        };
        return JsonSerializer.SerializeToUtf8Bytes(new{cmd="SET_ACTIVITY",args=new{pid=Environment.ProcessId,activity},nonce});
    }
    internal static async Task WriteFrame(Stream stream,int opcode,byte[] payload,CancellationToken token)
    {
        if(payload.Length>65536)throw new IOException("Discord payload exceeds the limit.");
        var frame=new byte[payload.Length+8];BinaryPrimitives.WriteInt32LittleEndian(frame,opcode);BinaryPrimitives.WriteInt32LittleEndian(frame.AsSpan(4),payload.Length);payload.CopyTo(frame,8);
        await stream.WriteAsync(frame,token).ConfigureAwait(false);await stream.FlushAsync(token).ConfigureAwait(false);
    }
    internal static async Task<(int Opcode,byte[] Payload)> ReadFrame(Stream stream,CancellationToken token)
    {
        var header=new byte[8];await stream.ReadExactlyAsync(header,token).ConfigureAwait(false);
        var opcode=BinaryPrimitives.ReadInt32LittleEndian(header);var size=BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(4));
        if(size is <0 or >65536||opcode is <0 or >4)throw new IOException("Invalid Discord IPC frame.");
        var payload=new byte[size];await stream.ReadExactlyAsync(payload,token).ConfigureAwait(false);return(opcode,payload);
    }
    private async Task Receive(string? nonce,CancellationToken token)
    {
        for(var i=0;i<32;i++)
        {
            var frame=await ReadFrame(pipe!,token).ConfigureAwait(false);
            if(frame.Opcode==3){await WriteFrame(pipe!,4,frame.Payload,token).ConfigureAwait(false);continue;}
            if(frame.Opcode==2)throw new IOException("Discord closed the presence connection.");
            if(frame.Opcode!=1)continue;
            using var json=JsonDocument.Parse(frame.Payload);var root=json.RootElement;
            if(root.TryGetProperty("evt",out var ev)&&ev.GetString()=="ERROR")throw new IOException("Discord rejected the presence request.");
            if(nonce==null&&root.TryGetProperty("evt",out ev)&&ev.GetString()=="READY"||nonce!=null&&root.TryGetProperty("nonce",out var value)&&value.GetString()==nonce)return;
        }
        throw new IOException("Discord response did not match the request.");
    }
    private async Task RunAsync()
    {
        var lastSent=System.Diagnostics.Stopwatch.StartNew();var hasSent=false;var hadExtraDetails=false;
        while(!lifetime.IsCancellationRequested)
        {
            try
            {
                PresenceState? target;long start;lock(changed){target=desired;start=started;}
                // Coalesce frequent detail changes. Clearing presence bypasses
                // the refresh delay so opting out never waits for the next tick.
                var refreshDelay=TimeSpan.FromSeconds(5)-lastSent.Elapsed;
                if(target!=null&&pipe!=null&&hasSent&&!(hadExtraDetails&&target.Details==null)&&refreshDelay>TimeSpan.Zero)
                {await changed.WaitAsync(refreshDelay,lifetime.Token).ConfigureAwait(false);continue;}
                using var timeout=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);timeout.CancelAfter(TimeSpan.FromSeconds(3));
                if(target!=null&&pipe==null)
                {
                    pipe=await connect(timeout.Token).ConfigureAwait(false);
                    await WriteFrame(pipe,0,JsonSerializer.SerializeToUtf8Bytes(new{v=1,client_id=ApplicationId}),timeout.Token).ConfigureAwait(false);
                    await Receive(null,timeout.Token).ConfigureAwait(false);
                }
                if(pipe!=null)
                {
                    lock(changed){target=desired;start=started;}
                    // Refresh unchanged activity too: detects client restarts and answers pings.
                    var nonce=Guid.NewGuid().ToString("N");await WriteFrame(pipe,1,Activity(target?.Runtime,start,nonce,target?.Details),timeout.Token).ConfigureAwait(false);
                    await Receive(nonce,timeout.Token).ConfigureAwait(false);
                    lastSent.Restart();hasSent=true;hadExtraDetails=target?.Details!=null;
                    if(target==null){pipe.Dispose();pipe=null;}
                }
                Volatile.Write(ref status,target==null?"Waiting for a connected game.":"Showing Bloodborne activity on Discord.");
                if(target==null){lock(changed){if(desired==null)cleared.TrySetResult();}}
            }
            catch(Exception ex)when(ex is IOException or OperationCanceledException or JsonException or UnauthorizedAccessException or ObjectDisposedException or InvalidOperationException)
            {pipe?.Dispose();pipe=null;Volatile.Write(ref status,"Discord unavailable; retrying automatically.");}
            try{await changed.WaitAsync(TimeSpan.FromSeconds(15),lifetime.Token).ConfigureAwait(false);}catch(OperationCanceledException){break;}
        }
    }
    public async Task StopAsync()
    {
        Update(null);Task done;lock(changed){done=cleared.Task;if(pipe==null)cleared.TrySetResult();}
        try{await done.WaitAsync(TimeSpan.FromMilliseconds(700)).ConfigureAwait(false);}catch(TimeoutException){}
        Dispose();try{await worker.WaitAsync(TimeSpan.FromMilliseconds(500)).ConfigureAwait(false);}catch(TimeoutException){}
    }
    public void Dispose(){if(disposed)return;disposed=true;lifetime.Cancel();pipe?.Dispose();}
}
