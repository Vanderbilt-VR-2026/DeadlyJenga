package com.deadlyjenga.nearby;

import android.app.Activity;
import android.bluetooth.*;
import android.bluetooth.le.*;
import android.content.Context;
import android.os.Handler;
import android.os.Looper;
import android.os.ParcelUuid;
import android.os.SystemClock;
import org.json.JSONObject;
import java.nio.charset.StandardCharsets;
import java.util.*;
import java.util.concurrent.ConcurrentLinkedQueue;

/** BLE discovery and approval only. Match traffic remains on Photon. */
public final class NearbyBluetooth {
    public static final UUID SERVICE=UUID.fromString("dc9e8001-b421-4678-9ba1-571b0f735110");
    public static final UUID INFO=UUID.fromString("dc9e8002-b421-4678-9ba1-571b0f735110");
    public static final UUID EXCHANGE=UUID.fromString("dc9e8003-b421-4678-9ba1-571b0f735110");
    private final Activity activity;
    private final Handler main=new Handler(Looper.getMainLooper());
    private final ConcurrentLinkedQueue<String> events=new ConcurrentLinkedQueue<>();
    private final Map<String,Request> requests=new LinkedHashMap<>();
    private final Map<String,BluetoothDevice> devices=new HashMap<>();
    private final Map<String,Long> probed=new HashMap<>();
    private final ArrayDeque<BluetoothDevice> probes=new ArrayDeque<>();
    private BluetoothAdapter adapter;
    private BluetoothGattServer server;
    private BluetoothLeAdvertiser advertiser;
    private BluetoothLeScanner scanner;
    private BluetoothGatt client;
    private boolean advertising, scanning, closed, enabled, host, requesting;
    private String room="", name="Player", protocol="", code="", selectedRoom="", requestId="";
    private int players, capacity=10, mtu;
    private long scanUntil, operationUntil, nextPoll;
    private static final class Request {
        String id, name, room, address, status="waiting", code="";
        long until;
    }
    public NearbyBluetooth(Activity activity) { this.activity=activity; main.post(this::tick); }
    private static long now(){return SystemClock.elapsedRealtime();}
    private static JSONObject json(String value) throws Exception { return new JSONObject(value); }
    private static byte[] bytes(String value){return value.getBytes(StandardCharsets.UTF_8);}
    private void emit(String kind, String value) {
        try { if(events.size()<128)events.add(new JSONObject().put("kind",kind).put("value",value).toString()); }
        catch(Exception ignored){}
    }
    public String poll(){String value=events.poll();return value==null?"":value;}
    public void configure(boolean enabled, boolean host, String room, String name, String protocol, String code, int players) {
        main.post(()->{
            if(closed)return;
            boolean changed=!this.room.equals(room)||this.host!=host||!this.protocol.equals(protocol);
            this.enabled=enabled;this.host=host;this.room=room;this.name=name;this.protocol=protocol;this.code=code;this.players=players;
            if(changed){requests.clear();stopHost();}
            if(!enabled){stopScan();stopClient();stopHost();requests.clear();return;}
            try {
                BluetoothManager manager=(BluetoothManager)activity.getSystemService(Context.BLUETOOTH_SERVICE);
                adapter=manager==null?null:manager.getAdapter();
                if(adapter==null||!adapter.isEnabled()){emit("status","Turn on Bluetooth in headset settings.");stopHost();return;}
                if(host&&players<capacity&&server==null)startHost(manager);
                else if(!host||players>=capacity)stopHost();
            }catch(SecurityException ex){emit("status","Nearby devices permission is required.");}
            catch(Exception ex){emit("status","Bluetooth setup failed: "+ex.getClass().getSimpleName());}
        });
    }
    private void startHost(BluetoothManager manager) {
        advertiser=adapter.getBluetoothLeAdvertiser();
        if(advertiser==null){emit("status","This device cannot advertise Bluetooth rooms.");return;}
        server=manager.openGattServer(activity,serverCallback);
        if(server==null){emit("status","This device could not open a Bluetooth room.");return;}
        BluetoothGattService service=new BluetoothGattService(SERVICE,BluetoothGattService.SERVICE_TYPE_PRIMARY);
        service.addCharacteristic(new BluetoothGattCharacteristic(INFO,BluetoothGattCharacteristic.PROPERTY_READ,BluetoothGattCharacteristic.PERMISSION_READ));
        service.addCharacteristic(new BluetoothGattCharacteristic(EXCHANGE,BluetoothGattCharacteristic.PROPERTY_READ|BluetoothGattCharacteristic.PROPERTY_WRITE,BluetoothGattCharacteristic.PERMISSION_READ|BluetoothGattCharacteristic.PERMISSION_WRITE));
        if(!server.addService(service)){stopHost();emit("status","Bluetooth service registration failed.");}
    }
    private final AdvertiseCallback advertiseCallback=new AdvertiseCallback(){
        @Override public void onStartSuccess(AdvertiseSettings settings){main.post(()->{advertising=true;emit("status","Nearby requests are on. Approve players in Rooms.");});}
        @Override public void onStartFailure(int error){main.post(()->{stopHost();emit("status","Bluetooth advertising failed ("+error+"). Room codes still work.");});}
    };
    private void stopHost(){
        try{if(advertiser!=null)advertiser.stopAdvertising(advertiseCallback);}catch(Exception ignored){}
        advertising=false;advertiser=null;
        if(server!=null){try{server.close();}catch(Exception ignored){}server=null;}
        if(!requests.isEmpty()){requests.clear();emit("requests","");}
    }
    private final BluetoothGattServerCallback serverCallback=new BluetoothGattServerCallback(){
        @Override public void onServiceAdded(int status,BluetoothGattService service){main.post(()->{
            if(server==null||!enabled||!host)return;
            if(status!=BluetoothGatt.GATT_SUCCESS){stopHost();emit("status","Bluetooth service unavailable.");return;}
            try{advertiser.startAdvertising(new AdvertiseSettings.Builder().setAdvertiseMode(AdvertiseSettings.ADVERTISE_MODE_LOW_POWER).setConnectable(true).setTimeout(0).build(),
                new AdvertiseData.Builder().addServiceUuid(new ParcelUuid(SERVICE)).setIncludeDeviceName(false).build(),advertiseCallback);}
            catch(Exception ex){stopHost();emit("status","Bluetooth advertising was denied.");}
        });}
        @Override public void onConnectionStateChange(BluetoothDevice device,int status,int state){main.post(()->{
            if(state==BluetoothProfile.STATE_DISCONNECTED){requests.remove(device.getAddress());emit("requests","");}
        });}
        @Override public void onCharacteristicReadRequest(BluetoothDevice device,int id,int offset,BluetoothGattCharacteristic characteristic){main.post(()->{
            if(server==null)return;
            try{
                JSONObject value=new JSONObject();
                if(INFO.equals(characteristic.getUuid()))value.put("room",room).put("name",name).put("protocol",protocol).put("players",players).put("capacity",capacity);
                else if(EXCHANGE.equals(characteristic.getUuid())){
                    Request request=requests.get(device.getAddress());
                    if(request==null||request.until<now()||!request.room.equals(room))value.put("status","expired");
                    else value.put("status",request.status).put("id",request.id).put("code",request.status.equals("approved")?request.code:"");
                }else{server.sendResponse(device,id,BluetoothGatt.GATT_REQUEST_NOT_SUPPORTED,offset,null);return;}
                byte[] data=bytes(value.toString());
                if(offset<0||offset>data.length){server.sendResponse(device,id,BluetoothGatt.GATT_INVALID_OFFSET,offset,null);return;}
                server.sendResponse(device,id,BluetoothGatt.GATT_SUCCESS,offset,Arrays.copyOfRange(data,offset,data.length));
            }catch(Exception ignored){}
        });}
        @Override public void onCharacteristicWriteRequest(BluetoothDevice device,int id,BluetoothGattCharacteristic characteristic,boolean prepared,boolean response,int offset,byte[] value){
            final byte[] copy=value.clone();main.post(()->{
                if(server==null)return;int result=BluetoothGatt.GATT_FAILURE;
                try{
                    if(!prepared&&offset==0&&EXCHANGE.equals(characteristic.getUuid())&&copy.length<=240&&enabled&&host&&players<capacity){
                        JSONObject packet=json(new String(copy,StandardCharsets.UTF_8));String token=packet.optString("id"), target=packet.optString("room"), player=packet.optString("name");
                        if(token.matches("[a-f0-9]{32}")&&target.equals(room)&&player.length()>0&&player.length()<=24&&requests.size()<10&&!requests.containsKey(device.getAddress())&&!requests.values().stream().anyMatch(r->r.id.equals(token))){
                            Request request=new Request();request.id=token;request.room=room;request.name=player;request.address=device.getAddress();request.until=now()+60000;requests.put(request.address,request);result=BluetoothGatt.GATT_SUCCESS;emit("requests","");
                        }
                    }
                }catch(Exception ignored){}
                if(response)try{server.sendResponse(device,id,result,offset,null);}catch(Exception ignored){}
            });
        }
    };
    public String requests(){
        // Snapshot is produced on Android's main thread by tick; Unity only reads the immutable string.
        return requestSnapshot;
    }
    private volatile String requestSnapshot="{\"items\":[]}";
    public void respond(String id,boolean accept){main.post(()->{
        for(Request r:requests.values())if(r.id.equals(id)&&r.status.equals("waiting")&&r.until>now()&&r.room.equals(room)){
            int reserved=0;for(Request other:requests.values())if(other.status.equals("approved")&&other.until>now())reserved++;
            boolean allow=accept&&enabled&&host&&players+reserved<capacity&&!code.isEmpty();
            r.status=allow?"approved":"denied";r.code=allow?code:"";r.until=now()+15000;emit("requests","");return;
        }
    });}
    public void scan(){main.post(()->{
        if(!enabled||closed||adapter==null){emit("status","Enable nearby Bluetooth first.");return;}
        try{
            stopScan();devices.clear();probed.clear();probes.clear();scanner=adapter.getBluetoothLeScanner();
            if(scanner==null){emit("status","Bluetooth scanning is unavailable.");return;}
            scanning=true;scanUntil=now()+20000;
            scanner.startScan(Collections.singletonList(new ScanFilter.Builder().setServiceUuid(new ParcelUuid(SERVICE)).build()),new ScanSettings.Builder().setScanMode(ScanSettings.SCAN_MODE_LOW_LATENCY).build(),scanCallback);
            emit("status","Searching nearby headsets for 20 seconds…");
        }catch(Exception ex){stopScan();emit("status","Bluetooth scan failed: "+ex.getClass().getSimpleName());}
    });}
    private final ScanCallback scanCallback=new ScanCallback(){
        @Override public void onScanResult(int type,ScanResult result){main.post(()->{
            if(!scanning||requesting)return;BluetoothDevice device=result.getDevice();String address=device.getAddress();
            if(probed.containsKey(address)||probes.size()>=12)return;probed.put(address,now());probes.add(device);
        });}
        @Override public void onScanFailed(int error){main.post(()->{stopScan();emit("status","Bluetooth scan failed ("+error+").");});}
    };
    private void stopScan(){try{if(scanner!=null)scanner.stopScan(scanCallback);}catch(Exception ignored){}scanning=false;scanner=null;probes.clear();}
    public void request(String roomId,String playerName,String id){main.post(()->{
        BluetoothDevice device=devices.get(roomId);
        if(!enabled||device==null){emit("join","Room expired. Search again.");return;}
        stopScan();stopClient();requesting=true;selectedRoom=roomId;requestId=id;name=playerName;connect(device);
    });}
    public void cancel(){main.post(()->{stopClient();emit("join","Request cancelled.");});}
    private void connect(BluetoothDevice device){
        mtu=23;operationUntil=now()+10000;nextPoll=0;
        try{client=device.connectGatt(activity,false,clientCallback,BluetoothDevice.TRANSPORT_LE);if(client==null)failed("Bluetooth connection could not start.");}
        catch(Exception ex){failed("Bluetooth connection denied.");}
    }
    private void stopClient(){BluetoothGatt old=client;client=null;requesting=false;operationUntil=0;nextPoll=0;if(old!=null){try{old.disconnect();old.close();}catch(Exception ignored){}}}
    private void failed(String message){boolean notify=requesting;stopClient();if(notify)emit("join",message);}
    private BluetoothGattCharacteristic characteristic(UUID uuid){BluetoothGattService service=client==null?null:client.getService(SERVICE);return service==null?null:service.getCharacteristic(uuid);}
    private void read(UUID uuid){BluetoothGattCharacteristic c=characteristic(uuid);if(c==null||!client.readCharacteristic(c))failed("Bluetooth read failed. Try again.");}
    private final BluetoothGattCallback clientCallback=new BluetoothGattCallback(){
        @Override public void onConnectionStateChange(BluetoothGatt gatt,int status,int state){main.post(()->{
            if(gatt!=client)return;
            if(status!=BluetoothGatt.GATT_SUCCESS||state==BluetoothProfile.STATE_DISCONNECTED){failed("The nearby host disconnected.");return;}
            if(state==BluetoothProfile.STATE_CONNECTED&&!gatt.discoverServices())failed("Bluetooth service discovery failed.");
        });}
        @Override public void onServicesDiscovered(BluetoothGatt gatt,int status){main.post(()->{
            if(gatt!=client)return;if(status!=BluetoothGatt.GATT_SUCCESS||characteristic(INFO)==null||characteristic(EXCHANGE)==null){failed("Room service is unavailable.");return;}
            if(!gatt.requestMtu(247))failed("Bluetooth packet negotiation failed.");
        });}
        @Override public void onMtuChanged(BluetoothGatt gatt,int size,int status){main.post(()->{if(gatt==client){mtu=size;if(status!=0||mtu<247){failed("This Bluetooth link cannot carry a join request.");return;}read(INFO);}});}
        @Override public void onCharacteristicRead(BluetoothGatt gatt,BluetoothGattCharacteristic c,int status){byte[] data=c.getValue();received(gatt,c.getUuid(),status,data==null?new byte[0]:data.clone());}
        @Override public void onCharacteristicRead(BluetoothGatt gatt,BluetoothGattCharacteristic c,byte[] data,int status){received(gatt,c.getUuid(),status,data.clone());}
        @Override public void onCharacteristicWrite(BluetoothGatt gatt,BluetoothGattCharacteristic c,int status){main.post(()->{
            if(gatt!=client)return;if(status!=0){failed("The host could not accept this request.");return;}
            operationUntil=now()+65000;nextPoll=now()+1000;emit("join","Waiting for the host to approve. You can cancel.");
        });}
    };
    private void received(BluetoothGatt gatt,UUID characteristic,int status,byte[] data){main.post(()->{
        if(gatt!=client)return;
        try{
            if(status!=0||data.length>512){failed("Invalid Bluetooth room response.");return;}
            JSONObject value=json(new String(data,StandardCharsets.UTF_8));
            if(INFO.equals(characteristic)){
                String id=value.optString("room");
                if(!value.optString("protocol").equals(protocol)||!id.matches("[a-f0-9]{32}")){failed("This room uses a different game build.");return;}
                devices.put(id,gatt.getDevice());emit("room",value.toString());
                if(!requesting){stopClient();return;}
                if(!id.equals(selectedRoom)||value.optInt("players",10)>=value.optInt("capacity",10)){failed("This room changed or is full. Search again.");return;}
                byte[] request=bytes(new JSONObject().put("id",requestId).put("room",id).put("name",name).toString());
                if(request.length>mtu-3||request.length>240){failed("Player name is too long for this Bluetooth link.");return;}
                BluetoothGattCharacteristic c=characteristic(EXCHANGE);c.setWriteType(BluetoothGattCharacteristic.WRITE_TYPE_DEFAULT);c.setValue(request);
                if(!gatt.writeCharacteristic(c))failed("Could not send the join request.");
            }else{
                String result=value.optString("status");
                if(result.equals("waiting")){nextPoll=now()+1000;return;}
                if(result.equals("approved")&&value.optString("id").equals(requestId)){
                    String address=value.optString("code");stopClient();emit("approved",address);
                }else{failed(result.equals("denied")?"The host declined your request.":"Your request expired. Try again.");}
            }
        }catch(Exception ex){failed("Could not read the nearby room.");}
    });}
    private void tick(){
        if(closed)return;
        try{
            if(scanning&&now()>scanUntil){stopScan();emit("status","Search finished. Refresh to find more rooms.");}
            if(client==null&&!probes.isEmpty())connect(probes.remove());
            if(client!=null&&now()>operationUntil)failed("Bluetooth request timed out. Try again.");
            if(client!=null&&nextPoll>0&&now()>=nextPoll){nextPoll=0;read(EXCHANGE);}
            requests.values().removeIf(r->r.until<now());
            org.json.JSONArray rows=new org.json.JSONArray();
            for(Request r:requests.values())if(r.status.equals("waiting"))rows.put(new JSONObject().put("id",r.id).put("name",r.name).put("seconds",Math.max(0,(r.until-now())/1000f)));
            requestSnapshot=new JSONObject().put("items",rows).toString();
        }catch(Exception ex){failed("Bluetooth is unavailable. Check headset settings.");}
        main.postDelayed(this::tick,200);
    }
    public void close(){main.post(()->{closed=true;stopScan();stopClient();stopHost();events.clear();});}
}
