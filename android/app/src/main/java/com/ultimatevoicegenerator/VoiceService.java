package com.ultimatevoicegenerator;

import android.app.*;
import android.content.*;
import android.net.Uri;
import android.os.*;
import android.util.AtomicFile;
import android.webkit.WebResourceResponse;
import androidx.documentfile.provider.DocumentFile;
import org.json.*;
import java.io.*;
import java.net.*;
import java.nio.file.*;
import java.security.*;
import java.time.Instant;
import java.util.*;
import java.util.concurrent.*;
import java.util.concurrent.atomic.AtomicBoolean;
import java.util.concurrent.locks.ReentrantLock;
import java.util.function.Consumer;

public final class VoiceService extends Service {
 public final class LocalBinder extends Binder {VoiceService get(){return VoiceService.this;}}
 private final IBinder binder=new LocalBinder();
 volatile Consumer<JSONObject> listener;
 private JSONObject settings;
 private JSONArray jobs=new JSONArray();
 private boolean paused=true;
 private volatile String phase="On-device engine · ready when needed",notice="";
 private final ExecutorService worker=Executors.newSingleThreadExecutor(),downloads=Executors.newSingleThreadExecutor();
 private final ReentrantLock inference=new ReentrantLock();
 private final AtomicBoolean draining=new AtomicBoolean(),cancelled=new AtomicBoolean();
 private volatile boolean downloading=false;
 private boolean bundledModels;
 private volatile int downloadPercent=0;
 private volatile String downloadMessage="",downloadError="";
 private int activeCommands=0;
 private PowerManager.WakeLock wake;
 private File base,models,refs,outputs;
 private AtomicFile state;
 private static final String[] MODEL_NAMES={"qwen-talker-1.7b-base-Q8_0.gguf","qwen-tokenizer-12hz-Q8_0.gguf","ggml-small.en.bin"};
 private static final String[] MODEL_HASH={"4b9a33a236908dd9435a42f7a396e38038329d053b704342a6413c08544c4fda","1883beeed99348fc35e23dd225e9082f93f6f8c109330a33d935baa8acdbfd94","c6138d6d58ecc8322097e0f987c32f1be8bb0a18532a3f88f734d1bbf9c41e5d"};
 private static final long[] MODEL_SIZE={2079448256L,291150624L,487614201L};
 @Override public void onCreate(){super.onCreate();base=getFilesDir();models=new File(base,"models");refs=new File(base,"references");outputs=new File(base,"recordings");models.mkdirs();refs.mkdirs();outputs.mkdirs();state=new AtomicFile(new File(base,"state.json"));
  try{String[] assets=getAssets().list("models");bundledModels=assets!=null&&Arrays.asList(assets).containsAll(Arrays.asList(MODEL_NAMES));}catch(IOException ignored){}
  try{settings=new JSONObject().put("folder","App storage").put("format","flac").put("reference","").put("referenceTranscript","").put("referenceHash","").put("speed",1).put("tone","natural").put("normalize",true).put("delivery","Neutral").put("lexicon",new JSONArray()).put("maxRetries",1).put("seed",42).put("backend","cpu");
   if(state.getBaseFile().exists()){try(InputStream in=state.openRead()){JSONObject saved=new JSONObject(new String(in.readAllBytes(),java.nio.charset.StandardCharsets.UTF_8));settings=saved.getJSONObject("settings");jobs=saved.getJSONArray("jobs");paused=true;for(int i=0;i<jobs.length();i++){JSONObject j=jobs.getJSONObject(i);j.put("fileBusy",false);if(j.optString("status").equals("processing"))j.put("status","pending").put("phase","Recovered · resume to continue");}}}
   // Scratch belongs exclusively to this application; user-selected files are never scanned.
   File[] scratch=getCacheDir().listFiles();if(scratch!=null)for(File f:scratch)if(f.getName().startsWith("uvg-")&&f.isFile())f.delete();
  }catch(Exception e){notice="Previous session could not be restored. Your recordings were kept.";}
  NotificationManager nm=getSystemService(NotificationManager.class);nm.createNotificationChannel(new NotificationChannel("voice-work","Offline voice processing",NotificationManager.IMPORTANCE_LOW));
  wake=((PowerManager)getSystemService(POWER_SERVICE)).newWakeLock(PowerManager.PARTIAL_WAKE_LOCK,"UltimateVoice:work");
 }
 @Override public IBinder onBind(Intent i){return binder;}
 @Override public int onStartCommand(Intent i,int flags,int id){startForeground(1,notification("Preparing offline processing…"));if(!wake.isHeld())wake.acquire(30*60*1000L);new Handler(getMainLooper()).postDelayed(this::idle,2000);return START_NOT_STICKY;}
 private Notification notification(String text){PendingIntent open=PendingIntent.getActivity(this,0,new Intent(this,MainActivity.class),PendingIntent.FLAG_IMMUTABLE|PendingIntent.FLAG_UPDATE_CURRENT);return new Notification.Builder(this,"voice-work").setSmallIcon(com.ultimatevoicegenerator.R.drawable.ic_voice).setContentTitle("Ultimate Voice Generator").setContentText(text).setContentIntent(open).setOngoing(true).setOnlyAlertOnce(true).build();}
 private void progress(String s){phase=s;getSystemService(NotificationManager.class).notify(1,notification(s));publish();}
 private synchronized void idle(){if(!draining.get()&&!downloading&&activeCommands==0){stopForeground(STOP_FOREGROUND_REMOVE);if(wake.isHeld())wake.release();stopSelf();}}
 private synchronized void save()throws Exception{FileOutputStream out=null;try{out=state.startWrite();out.write(new JSONObject().put("settings",settings).put("jobs",jobs).toString().getBytes(java.nio.charset.StandardCharsets.UTF_8));state.finishWrite(out);}catch(Exception e){if(out!=null)state.failWrite(out);throw e;}}
 private boolean modelReady(int i){
  if(new File(models,MODEL_NAMES[i]).length()!=MODEL_SIZE[i])return false;
  try{return new String(Files.readAllBytes(new File(models,MODEL_NAMES[i]+".verified").toPath()),java.nio.charset.StandardCharsets.US_ASCII).equals(MODEL_HASH[i]);}
  catch(IOException e){return false;}
 }
 private boolean modelsReady(){for(int i=0;i<MODEL_NAMES.length;i++)if(!modelReady(i))return false;return true;}
 boolean needsBundledPreparation(){return bundledModels&&!modelsReady()&&!downloading;}
 private synchronized void startBundledPreparation()throws Exception{
  if(downloading||modelsReady())return;
  long needed=200_000_000L;
  for(int i=0;i<MODEL_NAMES.length;i++)if(!modelReady(i))needed+=MODEL_SIZE[i];
  if(base.getUsableSpace()<needed)throw new IOException("Free about 3 GB of additional storage to prepare the included models, then tap Prepare included models.");
  downloading=true;downloadError="";downloadPercent=0;downloadMessage="Preparing included voice models";
  downloads.execute(()->{
   try{
    long complete=0,total=Arrays.stream(MODEL_SIZE).sum();
    for(int i=0;i<MODEL_NAMES.length;i++){
     if(modelReady(i)){complete+=MODEL_SIZE[i];continue;}
     downloadMessage="Preparing included model "+(i+1)+" of 3";
     progress(downloadMessage);
     final long offset=complete;
     final long[] last={0};
     ModelFileInstaller.install(getAssets().open("models/"+MODEL_NAMES[i]),new File(models,MODEL_NAMES[i]),MODEL_SIZE[i],MODEL_HASH[i],copied->{
      long now=System.currentTimeMillis();
      if(now-last[0]>700){last[0]=now;downloadPercent=(int)(100*(offset+copied)/total);progress(downloadMessage);}
     });
     complete+=MODEL_SIZE[i];
    }
    downloadPercent=100;downloadMessage="Included models ready";
   }catch(Exception e){downloadError=e.getMessage();}
   finally{downloading=false;phase=modelsReady()?"Models ready · choose a reference in Settings":"Model preparation needs attention";publish();idle();}
  });
  publish();
 }
 synchronized JSONObject snapshot()throws Exception{return new JSONObject().put("settings",new JSONObject(settings.toString())).put("jobs",new JSONArray(jobs.toString())).put("paused",paused).put("engine",phase).put("notice",notice.isEmpty()?JSONObject.NULL:notice).put("models",new JSONObject().put("ready",modelsReady()).put("bundled",bundledModels).put("busy",downloading).put("percent",downloadPercent).put("message",downloadMessage).put("error",downloadError));}
 private void publish(){try{Consumer<JSONObject> l=listener;if(l!=null)l.accept(new JSONObject().put("type","snapshot").put("data",snapshot()));}catch(Exception ignored){}}
 synchronized JSONObject job(String id)throws Exception{for(int i=0;i<jobs.length();i++){JSONObject j=jobs.getJSONObject(i);if(j.getString("id").equals(id))return j;}return null;}
 private JSONObject requiredJob(String id)throws Exception{JSONObject j=job(id);if(j==null)throw new IllegalArgumentException("Recording not found");return j;}
 private File reference(String path)throws Exception{File f=new File(path);if(!f.getCanonicalFile().getParentFile().equals(refs.getCanonicalFile())||!f.getName().matches("[a-f0-9]{32}\\.wav")||!f.isFile())throw new IOException("Choose a reference recording first");return f;}
 private File output(JSONObject j)throws Exception{File f=new File(j.getString("path"));if(!f.getCanonicalFile().getParentFile().equals(outputs.getCanonicalFile())||!f.getName().matches("voice-[a-f0-9]{32}\\.(wav|flac|mp3)"))throw new IOException("Invalid managed recording");return f;}
 private void validate(JSONObject s)throws Exception{
  if(!Set.of("flac","wav","mp3").contains(s.optString("format")))throw new IllegalArgumentException("Select FLAC, WAV or MP3");reference(s.optString("reference"));
  if(s.optString("referenceTranscript").isBlank())throw new IllegalArgumentException("Transcribe the reference or enter its words before saving");if(s.optString("referenceTranscript").length()>5000)throw new IllegalArgumentException("Reference transcript is too long");
  String folder=s.optString("folder");if(!folder.equals("App storage")){Uri uri=Uri.parse(folder);if(!"content".equals(uri.getScheme())||!DocumentFile.fromTreeUri(this,uri).canWrite())throw new IOException("Choose an accessible output folder again");}
  int retries=s.optInt("maxRetries",1);if(retries<0||retries>3)throw new IllegalArgumentException("Choose zero to three retries");JSONArray rules=s.optJSONArray("lexicon");if(rules==null)rules=new JSONArray();if(rules.length()>100)throw new IllegalArgumentException("At most 100 pronunciation rules");Set<String> keys=new HashSet<>();for(int i=0;i<rules.length();i++){JSONObject r=rules.getJSONObject(i);String k=r.optString("word"),v=r.optString("sayAs");if(k.isBlank()||v.isBlank()||k.length()>100||v.length()>300||!keys.add(k.toLowerCase(Locale.ROOT)))throw new IllegalArgumentException("Use unique, non-empty pronunciation rules");}
  s.put("speed",1).put("tone","natural").put("backend","cpu").put("lexicon",rules);
 }
 Object command(String cmd,Object p)throws Exception{
  switch(cmd){
   case "bootstrap":return snapshot();case "uiReady":if(needsBundledPreparation())startBundledPreparation();return true;
   case "downloadModels":if(bundledModels)startBundledPreparation();else startDownload();return snapshot();
   case "settings":{JSONObject s=new JSONObject(p.toString());validate(s);s.put("referenceHash",hash(reference(s.getString("reference"))));synchronized(this){settings=s;save();}publish();return snapshot();}
   case "draftReference":{synchronized(this){activeCommands++;}inference.lock();try{if(!modelsReady())throw new IOException("Install offline models first");progress("Transcribing reference on your phone…");return NativeEngine.transcribe(new File(models,MODEL_NAMES[2]).getPath(),AudioFiles.decode(this,Uri.fromFile(reference(p.toString())),16000,30)).trim();}finally{inference.unlock();synchronized(this){activeCommands--;}phase="On-device engine · ready";publish();idle();}}
   case "enqueue":enqueue((JSONArray)p);return snapshot();
   case "pause":synchronized(this){paused=(Boolean)p;save();}if(!paused)drain();publish();return snapshot();
   case "cancel":cancelled.set(true);NativeEngine.cancel();return true;
   case "remove":synchronized(this){JSONObject j=requiredJob(p.toString());if(!j.getString("status").equals("pending"))throw new IOException("Only waiting items can be removed");remove(j);save();}publish();return snapshot();
   case "clear":synchronized(this){for(int i=0;i<jobs.length();i++){JSONObject j=jobs.getJSONObject(i);if(!Set.of("pending","processing").contains(j.getString("status")))j.put("archived",true);}save();}publish();return snapshot();
   case "deleteRecord":deleteRecord(p.toString());return snapshot();
   case "listeningReview":{JSONObject payload=(JSONObject)p;JSONObject review=payload.getJSONObject("review");for(String k:new String[]{"voice","tone","emotion"})if(review.optInt(k)<0||review.optInt(k)>5)throw new IllegalArgumentException("Scores must be 0 to 5");if(review.optString("notes").length()>2000)throw new IllegalArgumentException("Notes too long");synchronized(this){requiredJob(payload.getString("id")).put("review",review);save();}publish();return snapshot();}
   case "updateSpeed":updateSpeed((JSONObject)p);return snapshot();
   case "unload":if(!inference.tryLock())throw new IOException("Wait until processing finishes");try{NativeEngine.unload();phase="Model memory released";}finally{inference.unlock();}publish();return snapshot();
   default:throw new IllegalArgumentException("Unknown command");
  }
 }
 String importReference(Uri uri)throws Exception{
  if(!modelsReady())throw new IOException("Install offline models before choosing a reference");try(android.os.ParcelFileDescriptor fd=getContentResolver().openFileDescriptor(uri,"r")){if(fd!=null&&fd.getStatSize()>25*1024*1024)throw new IOException("Reference must be under 25 MB");}
  float[] pcm=AudioFiles.decode(this,uri,24000,30);File out=new File(refs,UUID.randomUUID().toString().replace("-","")+".wav");try{NativeEngine.encode(pcm,out.getPath(),"wav",false);return out.getPath();}catch(Exception e){out.delete();throw e;}
 }
 private synchronized void enqueue(JSONArray prompts)throws Exception{
  if(!modelsReady())throw new IOException("Install offline models first");if(prompts.length()<1||prompts.length()>100)throw new IllegalArgumentException("Stage 1 to 100 prompts per mobile batch");if(jobs.length()+prompts.length()>1000)throw new IllegalArgumentException("Delete old history before adding more prompts");JSONArray incoming=new JSONArray();String batch=UUID.randomUUID().toString();
  for(int i=0;i<prompts.length();i++){JSONObject p=prompts.getJSONObject(i);String text=TextRules.clean(p.optString("text"));if(text.isEmpty()||text.length()>1500||TextRules.tokens(text).isEmpty())throw new IllegalArgumentException("Each prompt needs words, up to 1,500 characters");JSONObject options=new JSONObject(p.has("options")?p.getJSONObject("options").toString():settings.toString());validate(options);String spoken=TextRules.pronounce(text,options.getJSONArray("lexicon"));if(spoken.length()>4000)throw new IllegalArgumentException("Pronunciation-expanded prompt is too long");String id=UUID.randomUUID().toString().replace("-","");incoming.put(new JSONObject().put("id",id).put("batchId",batch).put("text",text).put("spokenText",spoken).put("notes",p.optString("notes")).put("options",options).put("created",Instant.now().toString()).put("status","pending").put("phase","Waiting").put("seconds",0).put("attempts",0).put("fileRevision",0).put("archived",false).put("fileBusy",false));}
  for(int i=0;i<incoming.length();i++)jobs.put(incoming.get(i));paused=false;save();publish();drain();
 }
 private void drain(){if(!draining.compareAndSet(false,true))return;worker.execute(()->{try{while(true){JSONObject j=null;synchronized(this){if(paused)break;for(int i=0;i<jobs.length();i++){JSONObject candidate=jobs.optJSONObject(i);if(candidate.optString("status").equals("pending")){j=candidate;break;}}if(j==null)break;}runJob(j);}}finally{draining.set(false);phase="On-device engine · ready";publish();boolean pending=false;synchronized(this){if(!paused)for(int i=0;i<jobs.length();i++)if(jobs.optJSONObject(i).optString("status").equals("pending"))pending=true;}if(pending)drain();idle();}});}
 private void runJob(JSONObject j){inference.lock();File partial=null;long start=System.nanoTime();try{cancelled.set(false);NativeEngine.resetCancel();synchronized(this){j.put("status","processing").put("error",JSONObject.NULL);save();}publish();JSONObject opt=j.getJSONObject("options");File ref=reference(opt.getString("reference"));String key=hash(ref);float[] reference=AudioFiles.decode(this,Uri.fromFile(ref),24000,30);
  File dest=new File(outputs,"voice-"+j.getString("id")+"."+opt.getString("format"));partial=new File(getCacheDir(),"uvg-"+j.getString("id")+".partial");
  for(int attempt=1;attempt<=opt.optInt("maxRetries",1)+1;attempt++){
   checkCancel();synchronized(this){j.put("attempts",attempt).put("phase","Generating take "+attempt);}progress("Generating take "+attempt+" on your phone…");
   float[] pcm=NativeEngine.generate(new File(models,MODEL_NAMES[0]).getPath(),new File(models,MODEL_NAMES[1]).getPath(),reference,key,opt.getString("referenceTranscript"),j.getString("spokenText"),opt.optLong("seed",42)+attempt-1);
   checkCancel();synchronized(this){j.put("phase","Exporting and checking words");}publish();NativeEngine.encode(pcm,partial.getPath(),opt.getString("format"),opt.optBoolean("normalize",true));pcm=null;
   String heard=NativeEngine.transcribe(new File(models,MODEL_NAMES[2]).getPath(),AudioFiles.decode(this,Uri.fromFile(partial),16000,120)).trim();JSONObject check=TextRules.compare(j.getString("spokenText"),heard,opt.getJSONArray("lexicon"));synchronized(this){j.put("verification",check);}checkCancel();if(check.getBoolean("passed")||attempt>opt.optInt("maxRetries",1)){Files.move(partial.toPath(),dest.toPath(),StandardCopyOption.REPLACE_EXISTING,StandardCopyOption.ATOMIC_MOVE);j.put("path",dest.getPath()).put("fileHash",hash(dest)).put("status",check.getBoolean("passed")?"completed":"review").put("phase",check.getBoolean("passed")?"Words checked":"Review word differences");try{exportFolder(j);}catch(Exception e){j.put("error","Audio saved in app storage. Folder export failed: "+e.getMessage());}break;}
  }
 }catch(Throwable e){try{j.put("status","failed").put("phase","Stopped").put("error",cancelled.get()?"Cancelled. Regenerate to try again.":e.getMessage()==null?e.toString():e.getMessage());}catch(Exception ignored){}}
 finally{if(partial!=null)partial.delete();try{synchronized(this){j.put("seconds",(System.nanoTime()-start)/1e9);save();}}catch(Exception e){notice="Could not save the latest history: "+e.getMessage();}inference.unlock();publish();}}
 private void checkCancel()throws IOException{if(cancelled.get())throw new IOException("Cancelled");}
 private synchronized void remove(JSONObject j){for(int i=0;i<jobs.length();i++)if(jobs.optJSONObject(i)==j){jobs.remove(i);break;}}
 private void checkEditable(JSONObject j)throws Exception{if(Set.of("pending","processing").contains(j.getString("status"))||j.optBoolean("fileBusy"))throw new IOException("Wait for this item to finish");}
 private void checkOwned(JSONObject j)throws Exception{if(j.has("path")&&!j.isNull("path")){File f=output(j);if(f.exists()&&!hash(f).equals(j.optString("fileHash")))throw new IOException("Recording was modified externally; it was preserved");}}
 private void deleteRecord(String id)throws Exception{inference.lock();try{JSONObject j=requiredJob(id);synchronized(this){checkEditable(j);j.put("fileBusy",true);}publish();try{checkOwned(j);if(j.has("path")&&!j.isNull("path"))Files.deleteIfExists(output(j).toPath());deleteExport(j);synchronized(this){remove(j);save();}}finally{j.put("fileBusy",false);publish();}}finally{inference.unlock();}}
 private void updateSpeed(JSONObject p)throws Exception{double speed=p.getDouble("speed");if(!Double.isFinite(speed)||speed<.5||speed>2)throw new IllegalArgumentException("Choose 0.5x to 2x");synchronized(this){activeCommands++;}inference.lock();JSONObject j=null;File temp=null;try{j=requiredJob(p.getString("id"));synchronized(this){checkEditable(j);j.put("fileBusy",true);}publish();checkOwned(j);File file=output(j);temp=new File(getCacheDir(),"uvg-speed-"+j.getString("id")+".partial");progress("Updating speed and checking words…");float[] pcm=AudioFiles.decode(this,Uri.fromFile(file),24000,120);pcm=NativeEngine.tempo(pcm,speed);NativeEngine.encode(pcm,temp.getPath(),j.getJSONObject("options").getString("format"),false);
  String heard=NativeEngine.transcribe(new File(models,MODEL_NAMES[2]).getPath(),AudioFiles.decode(this,Uri.fromFile(temp),16000,240)).trim();JSONObject check=TextRules.compare(j.getString("spokenText"),heard,j.getJSONObject("options").getJSONArray("lexicon"));Files.move(temp.toPath(),file.toPath(),StandardCopyOption.REPLACE_EXISTING,StandardCopyOption.ATOMIC_MOVE);j.put("verification",check).put("status",check.getBoolean("passed")?"completed":"review").put("fileHash",hash(file)).put("fileRevision",j.optInt("fileRevision")+1).put("appliedSpeed",speed);try{exportFolder(j);}catch(Exception e){j.put("error","Main audio updated. Export copy could not be refreshed: "+e.getMessage());}
 }finally{if(temp!=null)temp.delete();try{if(j!=null){synchronized(this){j.put("fileBusy",false);save();}}}finally{inference.unlock();synchronized(this){activeCommands--;}publish();idle();}}}
 private void exportFolder(JSONObject j)throws Exception{String folder=j.getJSONObject("options").getString("folder");if(folder.equals("App storage"))return;DocumentFile tree=DocumentFile.fromTreeUri(this,Uri.parse(folder));if(tree==null||!tree.canWrite())throw new IOException("Folder permission is unavailable");File source=output(j);DocumentFile doc=tree.createFile(AudioFiles.mime(j.getJSONObject("options").getString("format")),source.getName());if(doc==null)throw new IOException("Could not create export");try{copy(source,doc.getUri());}catch(Exception e){doc.delete();throw e;}deleteExport(j);j.put("exportUri",doc.getUri().toString()).put("exportHash",hash(source));}
 private void deleteExport(JSONObject j){String uri=j.optString("exportUri","");if(uri.isEmpty())return;try(InputStream in=getContentResolver().openInputStream(Uri.parse(uri))){if(in!=null&&hash(in).equals(j.optString("exportHash"))){DocumentFile doc=DocumentFile.fromSingleUri(this,Uri.parse(uri));if(doc!=null)doc.delete();}}catch(Exception ignored){}j.remove("exportUri");j.remove("exportHash");}
 void exportTo(String id,Uri dest)throws Exception{inference.lock();try{JSONObject j=requiredJob(id);checkEditable(j);copy(output(j),dest);}finally{inference.unlock();}}
 private void copy(File source,Uri target)throws Exception{try(InputStream in=new FileInputStream(source);OutputStream out=getContentResolver().openOutputStream(target,"wt")){if(out==null)throw new IOException("Could not open destination");in.transferTo(out);}}
 static String hash(File f)throws Exception{try(InputStream in=new FileInputStream(f)){return hash(in);}}
 static String hash(InputStream in)throws Exception{MessageDigest d=MessageDigest.getInstance("SHA-256");byte[] b=new byte[65536];int n;while((n=in.read(b))!=-1)d.update(b,0,n);StringBuilder s=new StringBuilder();for(byte x:d.digest())s.append(String.format(Locale.ROOT,"%02x",x&255));return s.toString();}
 private synchronized void startDownload()throws Exception{if(downloading)return;if(modelsReady())return;long needed=600_000_000L;for(int i=0;i<MODEL_NAMES.length;i++){File f=new File(models,MODEL_NAMES[i]);if(f.length()==MODEL_SIZE[i]&&new File(models,MODEL_NAMES[i]+".verified").exists())continue;needed+=Math.max(0,MODEL_SIZE[i]-new File(models,MODEL_NAMES[i]+".partial").length());}if(base.getUsableSpace()<needed)throw new IOException("Not enough free storage to finish model installation");downloading=true;downloadError="";downloads.execute(()->{try{long total=Arrays.stream(MODEL_SIZE).sum(),complete=0;for(int i=0;i<MODEL_NAMES.length;i++){File dest=new File(models,MODEL_NAMES[i]);if(dest.length()==MODEL_SIZE[i]&&new File(models,MODEL_NAMES[i]+".verified").exists()){complete+=MODEL_SIZE[i];continue;}downloadMessage="Downloading "+(i+1)+" of 3";String url=i==2?"https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-small.en.bin":"https://huggingface.co/Serveurperso/Qwen3-TTS-GGUF/resolve/main/"+MODEL_NAMES[i];File partial=new File(models,MODEL_NAMES[i]+".partial");HttpURLConnection conn=(HttpURLConnection)new URL(url).openConnection();conn.setConnectTimeout(30000);conn.setReadTimeout(60000);long received=partial.length();if(received>0)conn.setRequestProperty("Range","bytes="+received+"-");int code=conn.getResponseCode();if(code!=200&&code!=206)throw new IOException("Model server returned "+code+". Try again.");boolean append=code==206&&received>0;if(!append)received=0;long last=0;try(InputStream in=conn.getInputStream();OutputStream out=new FileOutputStream(partial,append)){byte[] b=new byte[65536];int n;while((n=in.read(b))!=-1){out.write(b,0,n);received+=n;if(received>MODEL_SIZE[i])throw new IOException("Unexpected model size");if(System.currentTimeMillis()-last>700){downloadPercent=(int)(100*(complete+received)/total);progress(downloadMessage);last=System.currentTimeMillis();}}}finally{conn.disconnect();}downloadMessage="Checking model integrity";progress(downloadMessage);if(partial.length()!=MODEL_SIZE[i]||!hash(partial).equals(MODEL_HASH[i])){partial.delete();throw new IOException("Model integrity check failed. Please retry.");}Files.move(partial.toPath(),dest.toPath(),StandardCopyOption.REPLACE_EXISTING);Files.write(new File(models,MODEL_NAMES[i]+".verified").toPath(),MODEL_HASH[i].getBytes(java.nio.charset.StandardCharsets.US_ASCII));complete+=MODEL_SIZE[i];}downloadPercent=100;}catch(Exception e){downloadError=e.getMessage();}finally{downloading=false;phase=modelsReady()?"Models installed · choose a reference in Settings":"Offline models need installation";publish();idle();}});publish();}
 WebResourceResponse audio(Uri uri,String range)throws Exception{String path=uri.getPath();File file=null;String format="wav";synchronized(this){if(path.startsWith("/references/")){String name=Uri.decode(path.substring(12));file=reference(new File(refs,name).getPath());}else{String id=path.substring(1).split("\\.")[0];JSONObject j=requiredJob(id);if(j.optBoolean("fileBusy"))return MainActivity.missing();file=output(j);format=j.getJSONObject("options").getString("format");}}
  long size=file.length(),start=0,end=size-1;boolean partial=range!=null&&range.startsWith("bytes=");if(partial){String[] r=range.substring(6).split("-",-1);if(r.length!=2)return MainActivity.missing();if(r[0].isEmpty())start=Math.max(0,size-Long.parseLong(r[1]));else{start=Long.parseLong(r[0]);if(!r[1].isEmpty())end=Math.min(end,Long.parseLong(r[1]));}}if(start<0||start>=size||end<start)return new WebResourceResponse("text/plain","UTF-8",416,"Range not satisfiable",Map.of("Content-Range","bytes */"+size),new ByteArrayInputStream(new byte[0]));
  RandomAccessFile raf=new RandomAccessFile(file,"r");raf.seek(start);final long count=end-start+1;InputStream input=new InputStream(){long remaining=count;public int read()throws IOException{if(remaining<=0)return -1;int r=raf.read();if(r>=0)remaining--;return r;}public int read(byte[] b,int off,int n)throws IOException{if(remaining<=0)return -1;int r=raf.read(b,off,(int)Math.min(n,remaining));if(r>0)remaining-=r;return r;}public void close()throws IOException{raf.close();}};Map<String,String> h=new HashMap<>();h.put("Access-Control-Allow-Origin","https://jarvis.local");h.put("Accept-Ranges","bytes");h.put("Content-Length",Long.toString(count));h.put("Cache-Control","no-store");if(partial)h.put("Content-Range","bytes "+start+"-"+end+"/"+size);return new WebResourceResponse(AudioFiles.mime(format),null,partial?206:200,partial?"Partial Content":"OK",h,input);
 }
 @Override public void onDestroy(){cancelled.set(true);try{NativeEngine.cancel();}catch(Throwable ignored){}worker.shutdown();downloads.shutdown();if(wake!=null&&wake.isHeld())wake.release();listener=null;super.onDestroy();}
}
