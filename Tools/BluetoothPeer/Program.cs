using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Storage.Streams;

// A real Windows BLE peer for testing one headset. It is not a Quest emulator.
internal static class Program
{
    static readonly Guid Service=new("dc9e8001-b421-4678-9ba1-571b0f735110"),Info=new("dc9e8002-b421-4678-9ba1-571b0f735110"),Exchange=new("dc9e8003-b421-4678-9ba1-571b0f735110");
    static string[] options=Array.Empty<string>();
    static string Option(string key,string fallback=""){int i=Array.IndexOf(options,key);return i>=0&&i+1<options.Length?options[i+1]:fallback;}
    static IBuffer Buffer(string text){using var writer=new DataWriter();writer.WriteBytes(Encoding.UTF8.GetBytes(text));return writer.DetachBuffer();}
    static string Text(IBuffer buffer){using var reader=DataReader.FromBuffer(buffer);byte[] data=new byte[buffer.Length];reader.ReadBytes(data);return Encoding.UTF8.GetString(data);}
    static async Task<int> Main(string[] args)
    {
        options=args;
        try
        {
            var adapter=await BluetoothAdapter.GetDefaultAsync();
            if(adapter==null)throw new Exception("No Bluetooth adapter");
            Console.WriteLine(JsonSerializer.Serialize(new{adapter.IsCentralRoleSupported,adapter.IsPeripheralRoleSupported,adapter.IsLowEnergySupported}));
            if(args.Contains("--probe"))return 0;
            if(args.Contains("--host")||args.Contains("--probe-host"))await Host();else await Guest();
            return 0;
        }
        catch(Exception e){Console.Error.WriteLine(e.GetType().Name+": "+e.Message);return 2;}
    }
    static async Task Guest()
    {
        var found=new ConcurrentDictionary<ulong,bool>();
        var watcher=new BluetoothLEAdvertisementWatcher{ScanningMode=BluetoothLEScanningMode.Active};
        watcher.AdvertisementFilter.Advertisement.ServiceUuids.Add(Service);
        watcher.Received+=(_,e)=>found.TryAdd(e.BluetoothAddress,true);
        BluetoothError scanError=BluetoothError.Success;
        watcher.Stopped+=(_,e)=>scanError=e.Error;
        watcher.Start();Console.WriteLine("Scanning for Deadly Jenga rooms for 12 seconds.");
        try{await Task.Delay(12000);}finally{watcher.Stop();}
        if(scanError!=BluetoothError.Success)throw new Exception("Bluetooth scan failed: "+scanError);
        Console.WriteLine("Matching BLE advertisements: "+found.Count);
        foreach(ulong address in found.Keys)
        {
            using var device=await BluetoothLEDevice.FromBluetoothAddressAsync(address);
            if(device==null)continue;
            var services=await device.GetGattServicesForUuidAsync(Service,BluetoothCacheMode.Uncached);
            if(services.Status!=GattCommunicationStatus.Success)continue;
            foreach(var service in services.Services)
            using(service)
            {
                var characteristics=await service.GetCharacteristicsAsync(BluetoothCacheMode.Uncached);
                var info=characteristics.Characteristics.SingleOrDefault(c=>c.Uuid==Info);
                var exchange=characteristics.Characteristics.SingleOrDefault(c=>c.Uuid==Exchange);
                if(info==null||exchange==null)continue;
                var read=await info.ReadValueAsync(BluetoothCacheMode.Uncached);
                if(read.Status!=GattCommunicationStatus.Success)continue;
                using var room=JsonDocument.Parse(Text(read.Value));var row=room.RootElement;
                Console.WriteLine("ROOM "+row.GetRawText());
                if(options.Contains("--scan-only"))continue;
                string filter=Option("--host-name");if(filter.Length>0&&row.GetProperty("name").GetString()!=filter)continue;
                string id=Guid.NewGuid().ToString("N");
                string packet=JsonSerializer.Serialize(new{id,room=row.GetProperty("room").GetString(),name=Option("--name","PC test guest")});
                var written=await exchange.WriteValueWithResultAsync(Buffer(packet),GattWriteOption.WriteWithResponse);
                if(written.Status!=GattCommunicationStatus.Success)throw new Exception("Request write failed: "+written.Status);
                Console.WriteLine("REQUEST SENT. Approve or deny PC test guest in the headset Room panel.");
                for(int i=0;i<65;i++)
                {
                    await Task.Delay(1000);var response=await exchange.ReadValueAsync(BluetoothCacheMode.Uncached);
                    if(response.Status!=GattCommunicationStatus.Success)throw new Exception("Response read failed: "+response.Status);
                    using var document=JsonDocument.Parse(Text(response.Value));var r=document.RootElement;string state=r.GetProperty("status").GetString()??"";
                    if(state!="approved"&&r.TryGetProperty("code",out var earlyCode)&&!string.IsNullOrEmpty(earlyCode.GetString()))throw new Exception("Code was exposed before approval");
                    if(state=="waiting")continue;
                    if(state=="approved"&&r.GetProperty("id").GetString()==id&&r.GetProperty("code").GetString()?.Length>0)
                    {Console.WriteLine("PASS: approval delivered a code only to this request. Code: "+r.GetProperty("code").GetString());return;}
                    Console.WriteLine("RESPONSE: "+state);return;
                }
                throw new TimeoutException("No decision before request expiry");
            }
        }
        if(!options.Contains("--scan-only"))throw new Exception("No selected room found. Enable nearby on a hosting headset.");
    }
    sealed record Pending(string Id,string Name,DateTime Until,string Status);
    static async Task Host()
    {
        bool probe=options.Contains("--probe-host");
        string protocol=Option("--protocol"),code=Option("--code"),room=Guid.NewGuid().ToString("N");
        if(!probe&&(protocol.Length==0||code.Length==0))throw new Exception("Host needs --protocol from a scanned room and --code of a real Photon room. This helper does not host gameplay.");
        var created=await GattServiceProvider.CreateAsync(Service);
        if(created.Error!=BluetoothError.Success)throw new Exception("GATT server: "+created.Error);
        var provider=created.ServiceProvider;
        var info=await provider.Service.CreateCharacteristicAsync(Info,new GattLocalCharacteristicParameters{CharacteristicProperties=GattCharacteristicProperties.Read,ReadProtectionLevel=GattProtectionLevel.Plain});
        var exchange=await provider.Service.CreateCharacteristicAsync(Exchange,new GattLocalCharacteristicParameters{CharacteristicProperties=GattCharacteristicProperties.Read|GattCharacteristicProperties.Write,ReadProtectionLevel=GattProtectionLevel.Plain,WriteProtectionLevel=GattProtectionLevel.Plain});
        if(info.Error!=BluetoothError.Success||exchange.Error!=BluetoothError.Success)throw new Exception("GATT characteristics unavailable");
        var pending=new ConcurrentDictionary<string,Pending>();
        info.Characteristic.ReadRequested+=async(_,args)=>
        {
            var hold=args.GetDeferral();try{var r=await args.GetRequestAsync();if(r!=null)Reply(r,JsonSerializer.Serialize(new{room,name="PC test host",protocol,players=1,capacity=10}));}finally{hold.Complete();}
        };
        exchange.Characteristic.WriteRequested+=async(_,args)=>
        {
            var hold=args.GetDeferral();try
            {
                var r=await args.GetRequestAsync();if(r==null)return;
                try
                {
                    using var doc=JsonDocument.Parse(Text(r.Value));var row=doc.RootElement;
                    string id=row.GetProperty("id").GetString()??"",name=row.GetProperty("name").GetString()??"";
                    if(r.Offset!=0||row.GetProperty("room").GetString()!=room||!Guid.TryParseExact(id,"N",out var parsedId)||name.Length>24)throw new Exception();
                    string status=Option("--decision","deny");status=status=="approve"?"approved":status=="wait"?"waiting":"denied";
                    pending[args.Session.DeviceId.Id]=new Pending(id,name,DateTime.UtcNow.AddSeconds(60),status);
                    Console.WriteLine("REQUEST "+name+" → "+status);r.Respond();
                }catch{r.RespondWithProtocolError(0x0d);}
            }finally{hold.Complete();}
        };
        exchange.Characteristic.ReadRequested+=async(_,args)=>
        {
            var hold=args.GetDeferral();try
            {
                var r=await args.GetRequestAsync();if(r==null)return;
                bool valid=pending.TryGetValue(args.Session.DeviceId.Id,out var p)&&p.Until>DateTime.UtcNow;
                Reply(r,JsonSerializer.Serialize(new{status=valid?p!.Status:"expired",id=valid?p!.Id:"",code=valid&&p!.Status=="approved"?code:""}));
            }finally{hold.Complete();}
        };
        var ready=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        provider.AdvertisementStatusChanged+=(_,e)=>{Console.WriteLine("ADVERTISING "+e.Status+" / "+e.Error);if(e.Status==GattServiceProviderAdvertisementStatus.Started)ready.TrySetResult(true);if(e.Status==GattServiceProviderAdvertisementStatus.Aborted)ready.TrySetException(new Exception("Windows advertising aborted ("+e.Error+")"));};
        try
        {
            provider.StartAdvertising(new GattServiceProviderAdvertisingParameters{IsConnectable=true,IsDiscoverable=true});
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(15));
            if(probe){Console.WriteLine("PASS: Windows registered and started the real BLE GATT service. No remote device tested.");await Task.Delay(2000);}
            else {Console.WriteLine("Test host active for 120 seconds; decision="+Option("--decision","deny"));await Task.Delay(120000);}
        }
        finally{provider.StopAdvertising();}
    }
    static void Reply(GattReadRequest r,string text)
    {
        byte[] data=Encoding.UTF8.GetBytes(text);
        if(r.Offset>data.Length){r.RespondWithProtocolError(0x07);return;}
        using var writer=new DataWriter();writer.WriteBytes(data.Skip((int)r.Offset).Take((int)r.Length).ToArray());r.RespondWithValue(writer.DetachBuffer());
    }
}
