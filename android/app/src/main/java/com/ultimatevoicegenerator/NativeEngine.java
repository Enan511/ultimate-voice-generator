package com.ultimatevoicegenerator;
final class NativeEngine {
 static {System.loadLibrary("uvg_qwen");System.loadLibrary("uvg_whisper");System.loadLibrary("uvg_audio");}
 static native float[] generate(String talker,String codec,float[] reference,String referenceKey,String transcript,String prompt,long seed);
 static native String transcribe(String model,float[] pcm16k);
 static native float[] tempo(float[] pcm24k,double speed);
 static native void encode(float[] pcm24k,String path,String format,boolean normalize);
 static native void unload();
 static native void cancel();
 static native void resetCancel();
}
