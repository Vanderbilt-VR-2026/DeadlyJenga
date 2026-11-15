package com.deadlyjenga.nearby;

import android.app.Activity;
import android.os.Bundle;
import android.os.Handler;
import android.content.pm.PackageManager;
import android.view.View;
import android.widget.*;
import org.json.*;
import java.util.UUID;

/** A flat Android diagnostic panel. Uses the exact game bridge; no Photon gameplay. */
public final class ProbeActivity extends Activity {
    private final Handler handler=new Handler();
    private NearbyBluetooth bridge;
    private TextView status;
    private EditText name,protocol,code;
    private LinearLayout rooms,requests;
    private boolean hosting,resumed;
    private final String room=UUID.randomUUID().toString().replace("-","");
    private String lastRequests="";
    private final String[] permissions={"android.permission.BLUETOOTH_SCAN","android.permission.BLUETOOTH_ADVERTISE","android.permission.BLUETOOTH_CONNECT"};
    @Override public void onCreate(Bundle saved){
        super.onCreate(saved);
        ScrollView scroll=new ScrollView(this);LinearLayout content=new LinearLayout(this);content.setOrientation(LinearLayout.VERTICAL);content.setPadding(24,16,24,16);scroll.addView(content);setContentView(scroll);
        label(content,"DEADLY JENGA · BLUETOOTH TEST ONLY",24);
        label(content,"No game or Photon connection. Tests real Bluetooth permissions, room discovery and approval. Different Wi-Fi networks are fine.",16);
        name=field(content,"Your test name",getPreferences(0).getString("name","Quest test player"));
        protocol=field(content,"Protocol (match PC test host)","ble-probe-1");
        code=field(content,"Test code returned ONLY after approval","US-TEST01");
        status=label(content,"Tap Enable permissions, then Advertise or Find rooms.",18);
        button(content,"1. Enable permissions",()->{if(!permitted())requestPermissions(permissions,1);else status.setText("Permissions granted. Ready to advertise or scan.");});
        button(content,"2. Advertise test room",()->{if(!ready())return;hosting=true;saveName();configure();status.setText("Starting test room…");});
        button(content,"2. Find nearby rooms",()->{if(!ready())return;hosting=false;saveName();configure();rooms.removeAllViews();bridge.scan();});
        button(content,"Stop / cancel",()->{hosting=false;if(bridge!=null){bridge.cancel();bridge.configure(false,false,room,name.getText().toString(),protocol.getText().toString(),"",1);}status.setText("Stopped.");});
        label(content,"HOST REQUESTS",20);requests=new LinearLayout(this);requests.setOrientation(LinearLayout.VERTICAL);content.addView(requests);
        label(content,"ROOMS FOUND",20);rooms=new LinearLayout(this);rooms.setOrientation(LinearLayout.VERTICAL);content.addView(rooms);
    }
    private void saveName(){String value=name.getText().toString().trim();if(value.length()>24)value=value.substring(0,24);if(value.isEmpty())value="Quest test player";name.setText(value);getPreferences(0).edit().putString("name",value).apply();}
    private boolean permitted(){for(String permission:permissions)if(checkSelfPermission(permission)!=PackageManager.PERMISSION_GRANTED)return false;return true;}
    private boolean ready(){if(!permitted()){status.setText("Grant Nearby devices permission first.");return false;}if(bridge==null)bridge=new NearbyBluetooth(this);return true;}
    private void configure(){bridge.configure(true,hosting,room,name.getText().toString(),protocol.getText().toString(),code.getText().toString(),1);}
    private TextView label(LinearLayout parent,String text,int size){TextView view=new TextView(this);view.setText(text);view.setTextSize(size);view.setPadding(0,8,0,8);parent.addView(view);return view;}
    private EditText field(LinearLayout parent,String hint,String value){EditText view=new EditText(this);view.setHint(hint);view.setText(value);view.setSingleLine(true);parent.addView(view);return view;}
    private void button(LinearLayout parent,String text,Runnable action){Button button=new Button(this);button.setText(text);button.setOnClickListener(v->action.run());parent.addView(button);}
    private final Runnable poll=new Runnable(){public void run(){
        if(!resumed)return;
        try{
            if(bridge!=null){
                for(int i=0;i<32;i++){
                    String raw=bridge.poll();if(raw.isEmpty())break;JSONObject event=new JSONObject(raw);String kind=event.optString("kind"),value=event.optString("value");
                    if(kind.equals("room")){
                        JSONObject found=new JSONObject(value);String id=found.getString("room");
                        button(rooms,found.optString("name")+"'s room — request approval",()->bridge.request(id,name.getText().toString(),UUID.randomUUID().toString().replace("-","")));
                    }else if(kind.equals("approved"))status.setText("APPROVED — received code: "+value+"\nDiagnostic only: no game was joined.");
                    else if(kind.equals("status")||kind.equals("join"))status.setText(value);
                }
                String snapshot=bridge.requests();
                if(!snapshot.equals(lastRequests)){
                    lastRequests=snapshot;requests.removeAllViews();JSONArray pending=new JSONObject(snapshot).getJSONArray("items");
                    for(int i=0;i<pending.length();i++){
                        JSONObject request=pending.getJSONObject(i);String id=request.getString("id");label(requests,request.optString("name")+" wants to join ("+request.optInt("seconds")+"s)",18);
                        button(requests,"Approve code share",()->bridge.respond(id,true));button(requests,"Deny",()->bridge.respond(id,false));
                    }
                }
            }
        }catch(Exception e){status.setText("Diagnostic error: "+e.getClass().getSimpleName()+" "+e.getMessage());}
        handler.postDelayed(this,500);
    }};
    @Override public void onResume(){super.onResume();resumed=true;handler.post(poll);}
    @Override public void onPause(){resumed=false;handler.removeCallbacks(poll);hosting=false;if(bridge!=null){bridge.close();bridge=null;}super.onPause();}
}
