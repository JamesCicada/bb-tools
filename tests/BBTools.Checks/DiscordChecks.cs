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
            Check(activity.GetProperty("details").GetString()=="Bloodborne · BB Tools"&&activity.GetProperty("state").GetString()=="shadPS4"&&activity.EnumerateObject().Select(x=>x.Name).Order().SequenceEqual(new[]{"buttons","details","instance","state","status_display_type","timestamps","type"}),"basic Discord activity includes public BB Tools branding but no character data or location");
            Check(activity.GetProperty("buttons")[0].GetProperty("url").GetString()==AppInfo.RepositoryUrl&&activity.GetProperty("buttons")[0].GetProperty("label").GetString()=="BBTools","Discord BBTools button points to the repository");
            Check(!activity.TryGetProperty("assets",out _)&&activity.GetProperty("status_display_type").GetInt32()==2&&DiscordPresence.ApplicationId=="1196921008809066616","Discord uses the selected application's default logo without an image override and requests details in the member-list status");
        }
        Check(!(new AppSettings().DiscordDetails??new()).Enabled&&!(JsonSerializer.Deserialize<AppSettings>("{}")!.DiscordDetails??new()).Enabled,"extra Discord details default off for new and legacy settings");
        var preferences=new AppSettings(DiscordDetails:new(true,true,false,true,false,true));
        Check(JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(preferences))!.DiscordDetails==preferences.DiscordDetails,"individual Discord detail choices survive settings serialization");
        using(var data=JsonDocument.Parse(DiscordPresence.Activity("Bloodborne PC",123,"extra",new("Fishing Hamlet",120,42,"Hunter",2))))
        {
            var activity=data.RootElement.GetProperty("args").GetProperty("activity");
            Check(activity.GetProperty("details").GetString()=="Bloodborne · Hunter · BL 120 · NG+2"&&activity.GetProperty("state").GetString()=="Fishing Hamlet · Deaths: 42 · Bloodborne PC","extra presence formats character, level, area, total deaths and NG cycle");
        }
        var bounded=new DiscordGameDetails(new string('A',200),CharacterName:new string('B',200));
        Check(bounded.DetailsText.Length<=128&&bounded.StateText("shadPS4").Length<=128,"Discord text respects field limits");
        Check(BBTools.Memory.PlayerSession.PresenceArea(0x18010000)=="Central Yharnam"&&BBTools.Memory.PlayerSession.PresenceArea(0x24000000)=="Fishing Hamlet"&&BBTools.Memory.PlayerSession.PresenceArea(0x1D010000)=="Chalice dungeon","presence areas map loaded base-game, DLC and chalice IDs");
        var nameBytes=new byte[34];System.Text.Encoding.Unicode.GetBytes("狩人").CopyTo(nameBytes,0);
        Check(BBTools.Memory.PlayerSession.DecodePresenceName(nameBytes)=="狩人","presence character names decode bounded UTF-16 including non-Latin names");
        nameBytes[0]=0;nameBytes[1]=0xD8;nameBytes[2]=0;nameBytes[3]=0;
        Check(BBTools.Memory.PlayerSession.DecodePresenceName(nameBytes)==null&&BBTools.Memory.PlayerSession.DecodePresenceName(Enumerable.Repeat((byte)65,34).ToArray())==null,"invalid Unicode and unterminated character names are omitted");
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
        var basicShown=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
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
            var basicFrame=await DiscordPresence.ReadFrame(server,timeout.Token);using var basicJson=JsonDocument.Parse(basicFrame.Payload);
            var basic=basicJson.RootElement.GetProperty("args").GetProperty("activity");
            Check(basic.GetProperty("details").GetString()=="Bloodborne · BB Tools"&&!System.Text.Encoding.UTF8.GetString(basicFrame.Payload).Contains("Private Hunter"),"turning off extra sharing publishes a basic activity without retaining character details");
            Check(basic.GetProperty("timestamps").GetProperty("start").GetInt64()==request.RootElement.GetProperty("args").GetProperty("activity").GetProperty("timestamps").GetProperty("start").GetInt64(),"changing extra-detail sharing preserves the connection's elapsed-time origin");
            await DiscordPresence.WriteFrame(server,1,JsonSerializer.SerializeToUtf8Bytes(new{nonce=basicJson.RootElement.GetProperty("nonce").GetString()}),timeout.Token);basicShown.SetResult();
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
        presence.Update("Bloodborne PC",new(CharacterName:"Private Hunter"));await shown.Task.WaitAsync(timeout.Token);
        presence.Update("Bloodborne PC");await basicShown.Task.WaitAsync(timeout.Token);
        presence.Update(null);await serverWork;await presence.StopAsync();
    }
}
