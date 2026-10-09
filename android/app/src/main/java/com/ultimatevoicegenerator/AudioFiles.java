package com.ultimatevoicegenerator;
import android.content.Context;
import android.media.*;
import android.net.Uri;
import java.nio.*;
import java.io.*;
import java.util.*;

final class AudioFiles {
 static String mime(String f){return f.equals("mp3")?"audio/mpeg":f.equals("flac")?"audio/flac":"audio/wav";}
 static float[] decode(Context c,Uri source,int rate,int maxSeconds)throws Exception {
  MediaExtractor extractor=new MediaExtractor();MediaCodec codec=null;
  try{extractor.setDataSource(c,source,null);MediaFormat format=null;String mime=null;
   for(int n=0;n<extractor.getTrackCount();n++){MediaFormat f=extractor.getTrackFormat(n);String m=f.getString(MediaFormat.KEY_MIME);if(m!=null&&m.startsWith("audio/")){extractor.selectTrack(n);format=f;mime=m;break;}}
   if(format==null)throw new IOException("Choose a supported audio recording");
   if(format.containsKey(MediaFormat.KEY_DURATION)&&format.getLong(MediaFormat.KEY_DURATION)>maxSeconds*1_000_000L)throw new IOException("Recording exceeds "+maxSeconds+" seconds");
   int inputRate=format.getInteger(MediaFormat.KEY_SAMPLE_RATE),channels=format.getInteger(MediaFormat.KEY_CHANNEL_COUNT),encoding=AudioFormat.ENCODING_PCM_16BIT;
   codec=MediaCodec.createDecoderByType(mime);codec.configure(format,null,null,0);codec.start();boolean inDone=false,outDone=false;MediaCodec.BufferInfo info=new MediaCodec.BufferInfo();ArrayList<float[]> blocks=new ArrayList<>();int total=0;long deadline=System.currentTimeMillis()+180000;
   while(!outDone){if(System.currentTimeMillis()>deadline)throw new IOException("Audio decoding timed out");
    if(!inDone){int i=codec.dequeueInputBuffer(10000);if(i>=0){ByteBuffer b=codec.getInputBuffer(i);int n=extractor.readSampleData(b,0);if(n<0){codec.queueInputBuffer(i,0,0,0,MediaCodec.BUFFER_FLAG_END_OF_STREAM);inDone=true;}else{codec.queueInputBuffer(i,0,n,extractor.getSampleTime(),0);extractor.advance();}}}
    int i=codec.dequeueOutputBuffer(info,10000);
    if(i==MediaCodec.INFO_OUTPUT_FORMAT_CHANGED){MediaFormat f=codec.getOutputFormat();inputRate=f.getInteger(MediaFormat.KEY_SAMPLE_RATE);channels=f.getInteger(MediaFormat.KEY_CHANNEL_COUNT);encoding=f.containsKey(MediaFormat.KEY_PCM_ENCODING)?f.getInteger(MediaFormat.KEY_PCM_ENCODING):AudioFormat.ENCODING_PCM_16BIT;}
    else if(i>=0){ByteBuffer b=codec.getOutputBuffer(i).order(ByteOrder.LITTLE_ENDIAN);b.position(info.offset);b.limit(info.offset+info.size);int bytes=encoding==AudioFormat.ENCODING_PCM_FLOAT?4:2;if(encoding!=AudioFormat.ENCODING_PCM_FLOAT&&encoding!=AudioFormat.ENCODING_PCM_16BIT)throw new IOException("Unsupported decoded PCM format");float[] block=new float[info.size/bytes/channels];for(int n=0;n<block.length;n++){float sum=0;for(int ch=0;ch<channels;ch++)sum+=bytes==4?b.getFloat():b.getShort()/32768f;block[n]=sum/channels;}blocks.add(block);total+=block.length;codec.releaseOutputBuffer(i,false);outDone=(info.flags&MediaCodec.BUFFER_FLAG_END_OF_STREAM)!=0;if(total>(long)inputRate*maxSeconds)throw new IOException("Recording exceeds "+maxSeconds+" seconds");}
   }
   if(total<inputRate/3)throw new IOException("Recording is too short or empty");float[] pcm=new float[total];int at=0;for(float[] b:blocks){System.arraycopy(b,0,pcm,at,b.length);at+=b.length;}return resample(pcm,inputRate,rate);
  }finally{if(codec!=null){try{codec.stop();}catch(Exception ignored){}codec.release();}extractor.release();}
 }
 // Windowed-sinc low-pass resampling avoids aliasing when preparing ASR/reference audio.
 static float[] resample(float[] input,int from,int to){if(from==to)return input;float[] out=new float[(int)((long)input.length*to/from)];double cutoff=Math.min(1.,(double)to/from)*.94;
  for(int i=0;i<out.length;i++){double center=(double)i*from/to,sum=0,weight=0;int base=(int)center;
   for(int j=base-24;j<=base+24;j++){if(j<0||j>=input.length)continue;double distance=center-j;if(Math.abs(distance)>24)continue;double x=Math.PI*distance*cutoff;double w=(Math.abs(x)<1e-9?1:Math.sin(x)/x)*(.5+.5*Math.cos(Math.PI*distance/24))*cutoff;sum+=input[j]*w;weight+=w;}out[i]=(float)(sum/Math.max(weight,1e-9));}return out;}
}
