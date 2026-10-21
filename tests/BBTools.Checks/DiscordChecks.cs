using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using BBTools;

internal static partial class Program
{
    private static void DiscordChecks()
    {
        Check(new AppSettings().DiscordRichPresence&&JsonSerializer.Deserialize<AppSettings>("{}")!.DiscordRichPresence,"Discord Rich Presence defaults on for both new and older settings");
        Check(!JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(new AppSettings(DiscordRichPresence:false)))!.DiscordRichPresence,"Discord opt-out survives settings serialization");
        using(var data=JsonDocument.Parse(DiscordPresence.Activity("shadPS4",123,"test")))
        {
            var activity=data.RootElement.GetProperty("args").GetProperty("activity");
            Check(activity.GetProperty("details").GetString()=="Playing Bloodborne"&&activity.GetProperty("state").GetString()=="shadPS4"&&activity.EnumerateObject().Select(x=>x.Name).Order().SequenceEqual(new[]{"details","instance","state","timestamps"}),"Discord activity contains only game/runtime and elapsed time, without character data or location");
        }
        var broken=new byte[8];BitConverter.GetBytes(65537).CopyTo(broken,4);
        Reject<IOException>(()=>DiscordPresence.ReadFrame(new MemoryStream(broken),CancellationToken.None).GetAwaiter().GetResult(),"Discord rejects oversized IPC payloads before allocation");
        DiscordRoundtrip().GetAwaiter().GetResult();
    }
    private static async Task DiscordRoundtrip()
    {
        var name="BBTools-discord-test-"+Guid.NewGuid().ToString("N");
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(6));
        using var server=new NamedPipeServerStream(name,PipeDirection.InOut,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous);
        var shown=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var serverWork=Task.Run(async()=>
        {
            await server.WaitForConnectionAsync(timeout.Token);
            var handshake=await DiscordPresence.ReadFrame(server,timeout.Token);using var json=JsonDocument.Parse(handshake.Payload);
            Check(handshake.Opcode==0&&json.RootElement.GetProperty("client_id").GetString()==DiscordPresence.ApplicationId,"Discord handshake uses the project's public application ID without credentials");
            await DiscordPresence.WriteFrame(server,1,"{\"evt\":\"READY\"}"u8.ToArray(),timeout.Token);
            var set=await DiscordPresence.ReadFrame(server,timeout.Token);using var request=JsonDocument.Parse(set.Payload);var nonce=request.RootElement.GetProperty("nonce").GetString();
            await DiscordPresence.WriteFrame(server,3,[1,2,3],timeout.Token);var pong=await DiscordPresence.ReadFrame(server,timeout.Token);
            Check(pong.Opcode==4&&pong.Payload.SequenceEqual(new byte[]{1,2,3}),"Discord IPC answers pings while awaiting the activity acknowledgement");
            await DiscordPresence.WriteFrame(server,1,JsonSerializer.SerializeToUtf8Bytes(new{nonce}),timeout.Token);shown.SetResult();
            // A queued first update may refresh before the test's disable arrives.
            for(var i=0;i<4;i++)
            {
                var frame=await DiscordPresence.ReadFrame(server,timeout.Token);using var clear=JsonDocument.Parse(frame.Payload);var root=clear.RootElement;
                await DiscordPresence.WriteFrame(server,1,JsonSerializer.SerializeToUtf8Bytes(new{nonce=root.GetProperty("nonce").GetString()}),timeout.Token);
                if(root.GetProperty("args").GetProperty("activity").ValueKind==JsonValueKind.Null){Check(true,"disabling or disconnecting sends an explicit Discord activity clear");return;}
            }
            throw new Exception("Discord activity was not cleared.");
        });
        using var presence=new DiscordPresence(async token=>{var pipe=new NamedPipeClientStream(".",name,PipeDirection.InOut,PipeOptions.Asynchronous);await pipe.ConnectAsync(token);return pipe;});
        presence.Update("Bloodborne PC");await shown.Task.WaitAsync(timeout.Token);presence.Update(null);await serverWork;await presence.StopAsync();
    }
}
