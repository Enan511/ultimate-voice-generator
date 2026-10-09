package com.ultimatevoicegenerator;

import android.app.*;
import android.content.*;
import android.net.Uri;
import android.os.*;
import android.webkit.*;
import android.view.*;
import android.graphics.Color;
import android.Manifest;
import org.json.*;
import java.io.*;
import java.util.*;
import java.util.concurrent.*;

public final class MainActivity extends Activity {
 private WebView web;
 private VoiceService service;
 private final ExecutorService io=Executors.newFixedThreadPool(2);
 private final HashMap<Integer,String> requests=new HashMap<>();
 private int picker=200;
 private ValueCallback<Uri[]> browserPicker;
 private final ServiceConnection connection=new ServiceConnection(){
  public void onServiceConnected(ComponentName name,IBinder binder){service=((VoiceService.LocalBinder)binder).get();service.listener=MainActivity.this::send;web.loadUrl("https://jarvis.local/index.html");}
  public void onServiceDisconnected(ComponentName name){service=null;}
 };
 @Override public void onCreate(Bundle state){super.onCreate(state);
  web=new WebView(this);web.setBackgroundColor(Color.rgb(11,16,24));setContentView(web);
  web.setOnApplyWindowInsetsListener((v,insets)->{WindowInsetsController bars=v.getWindowInsetsController();if(bars!=null)bars.setSystemBarsAppearance(0,WindowInsetsController.APPEARANCE_LIGHT_STATUS_BARS|WindowInsetsController.APPEARANCE_LIGHT_NAVIGATION_BARS);android.graphics.Insets edges=insets.getInsets(WindowInsets.Type.systemBars()|WindowInsets.Type.displayCutout()|WindowInsets.Type.ime());v.setPadding(edges.left,edges.top,edges.right,edges.bottom);return insets;});
  WebSettings s=web.getSettings();s.setJavaScriptEnabled(true);s.setDomStorageEnabled(true);s.setAllowFileAccess(false);s.setAllowContentAccess(false);s.setMixedContentMode(WebSettings.MIXED_CONTENT_NEVER_ALLOW);s.setMediaPlaybackRequiresUserGesture(true);s.setSupportMultipleWindows(false);s.setTextZoom(100);
  CookieManager.getInstance().setAcceptCookie(false);
  web.addJavascriptInterface(new Bridge(),"UltimateAndroid");
  web.setWebViewClient(new WebViewClient(){
   @Override public boolean shouldOverrideUrlLoading(WebView view,WebResourceRequest r){return !"https://jarvis.local/index.html".equals(r.getUrl().toString());}
   @Override public WebResourceResponse shouldInterceptRequest(WebView view,WebResourceRequest r){
    try{Uri u=r.getUrl();if(!"https".equals(u.getScheme()))return missing();
     if("jarvis.local".equals(u.getHost())){String p=u.getPath();if(p==null||p.contains("..")||p.contains("\\"))return missing();if(p.equals("/"))p="/index.html";String mime=p.endsWith(".js")?"application/javascript":p.endsWith(".css")?"text/css":p.endsWith(".ico")?"image/x-icon":"text/html";return new WebResourceResponse(mime,"UTF-8",getAssets().open("ui"+p));}
     if("audio.jarvis.local".equals(u.getHost())&&service!=null)return service.audio(u,r.getRequestHeaders().get("Range"));
    }catch(Exception ignored){}return missing();
   }
  });
  web.setWebChromeClient(new WebChromeClient(){
   @Override public boolean onShowFileChooser(WebView v,ValueCallback<Uri[]> callback,FileChooserParams params){if(browserPicker!=null)browserPicker.onReceiveValue(null);browserPicker=callback;Intent i=new Intent(Intent.ACTION_OPEN_DOCUMENT).setType("text/*").addCategory(Intent.CATEGORY_OPENABLE).putExtra(Intent.EXTRA_ALLOW_MULTIPLE,true);startActivityForResult(i,100);return true;}
   @Override public void onPermissionRequest(PermissionRequest r){r.deny();}
  });
  getOnBackInvokedDispatcher().registerOnBackInvokedCallback(0,()->web.evaluateJavascript("(()=>{const d=document.querySelector('dialog[open]');if(d){d.dispatchEvent(new Event('cancel',{cancelable:true}));return;}const b=[...document.querySelectorAll('nav button')].find(x=>x.textContent.includes('Create'));if(b&&!b.classList.contains('active'))b.click();else UltimateAndroid.minimize();})()",null));
  bindService(new Intent(this,VoiceService.class),connection,BIND_AUTO_CREATE);
 }
 static WebResourceResponse missing(){return new WebResourceResponse("text/plain","UTF-8",404,"Not found",Map.of(),new ByteArrayInputStream(new byte[0]));}
 void send(JSONObject message){runOnUiThread(()->{if(web!=null)web.evaluateJavascript("window.__uvgReceive&&window.__uvgReceive("+message+")",null);});}
 void result(String id,Object value,Throwable error){try{JSONObject m=new JSONObject().put("id",id);if(error!=null)m.put("error",error.getMessage()==null?error.toString():error.getMessage());else m.put("result",value==null?JSONObject.NULL:value);send(m);}catch(Exception ignored){}}
 void foreground(){runOnUiThread(()->{if(checkSelfPermission(Manifest.permission.POST_NOTIFICATIONS)!=android.content.pm.PackageManager.PERMISSION_GRANTED)requestPermissions(new String[]{Manifest.permission.POST_NOTIFICATIONS},10);startForegroundService(new Intent(this,VoiceService.class));});}
 final class Bridge {
  @JavascriptInterface public void minimize(){runOnUiThread(()->moveTaskToBack(true));}
  @JavascriptInterface public void postMessage(String raw){if(raw.length()>2_000_000)return;io.execute(()->{String id=null;try{JSONObject m=new JSONObject(raw);id=m.getString("id");String cmd=m.getString("command");Object payload=m.opt("payload");if(service==null)throw new IllegalStateException("Engine is starting. Reopen the app in a moment.");
   if(cmd.equals("pickReference")||cmd.equals("pickFolder")||cmd.equals("openFolder")){String rid=id;runOnUiThread(()->pick(cmd,rid,payload));return;}
   if(Set.of("downloadModels","enqueue","draftReference","updateSpeed").contains(cmd)||(cmd.equals("pause")&&Boolean.FALSE.equals(payload))||(cmd.equals("uiReady")&&service.needsBundledPreparation()))foreground();
   result(id,service.command(cmd,payload),null);
  }catch(Throwable e){result(id,null,e);}});}
 }
 void pick(String cmd,String id,Object payload){try{
  int code=++picker;requests.put(code,id);
  if(cmd.equals("pickFolder")){startActivityForResult(new Intent(Intent.ACTION_OPEN_DOCUMENT_TREE).addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION|Intent.FLAG_GRANT_WRITE_URI_PERMISSION|Intent.FLAG_GRANT_PERSISTABLE_URI_PERMISSION),code);return;}
  if(cmd.equals("pickReference")){Intent i=new Intent(Intent.ACTION_OPEN_DOCUMENT).addCategory(Intent.CATEGORY_OPENABLE).setType("audio/*").putExtra(Intent.EXTRA_MIME_TYPES,new String[]{"audio/wav","audio/x-wav","audio/mpeg","audio/flac","audio/mp4"});requests.put(-code,"reference");startActivityForResult(i,code);return;}
  requests.remove(code);String job=String.valueOf(payload);JSONObject j=service.job(job);if(j==null)throw new IllegalArgumentException("Recording not found");Intent i=new Intent(Intent.ACTION_CREATE_DOCUMENT).addCategory(Intent.CATEGORY_OPENABLE).setType(AudioFiles.mime(j.getJSONObject("options").getString("format"))).putExtra(Intent.EXTRA_TITLE,"voice-"+job.substring(0,8)+"."+j.getJSONObject("options").getString("format"));requests.put(code,id);requests.put(-code,job);startActivityForResult(i,code);
 }catch(Exception e){result(id,null,e);}}
 @Override protected void onActivityResult(int code,int status,Intent data){super.onActivityResult(code,status,data);
  if(code==100){if(browserPicker!=null){browserPicker.onReceiveValue(WebChromeClient.FileChooserParams.parseResult(status,data));browserPicker=null;}return;}
  String id=requests.remove(code),kind=requests.remove(-code);if(id==null)return;if(status!=RESULT_OK||data==null||data.getData()==null){result(id,null,null);return;}Uri uri=data.getData();
  if(kind!=null&&kind.equals("reference"))foreground();
  io.execute(()->{try{
   if(kind==null){
    if((data.getFlags()&Intent.FLAG_GRANT_WRITE_URI_PERMISSION)==0)throw new IOException("Choose a folder that allows saving files");
    int flags=(data.getFlags()&Intent.FLAG_GRANT_READ_URI_PERMISSION)!=0?Intent.FLAG_GRANT_READ_URI_PERMISSION|Intent.FLAG_GRANT_WRITE_URI_PERMISSION:Intent.FLAG_GRANT_WRITE_URI_PERMISSION;
    getContentResolver().takePersistableUriPermission(uri,flags);result(id,uri.toString(),null);
   }
   else if(kind.equals("reference"))result(id,service.importReference(uri),null);
   else{service.exportTo(kind,uri);result(id,true,null);}
  }catch(Exception e){result(id,null,e);}});
 }
 @Override protected void onDestroy(){if(service!=null)service.listener=null;unbindService(connection);if(browserPicker!=null)browserPicker.onReceiveValue(null);web.removeJavascriptInterface("UltimateAndroid");web.destroy();web=null;io.shutdown();super.onDestroy();}
}
