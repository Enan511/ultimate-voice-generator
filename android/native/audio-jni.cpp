#include <jni.h>
#include <vector>
#include <string>
#include <fstream>
#include <cmath>
#include <algorithm>
#include <stdexcept>
#include <cstdint>
#include "FLAC/stream_encoder.h"
#include "SoundTouch.h"
#include "lame.h"
static std::string str(JNIEnv* e,jstring s){auto p=e->GetStringUTFChars(s,nullptr);std::string r(p);e->ReleaseStringUTFChars(s,p);return r;}
static void u16(std::ofstream& f,uint16_t x){char b[]={char(x),char(x>>8)};f.write(b,2);}
static void u32(std::ofstream& f,uint32_t x){u16(f,x);u16(f,x>>16);}
extern "C" JNIEXPORT jfloatArray JNICALL Java_com_ultimatevoicegenerator_NativeEngine_tempo(JNIEnv* e,jclass,jfloatArray data,jdouble speed){
 try{if(speed<.5||speed>2||!std::isfinite(speed))throw std::runtime_error("Speed must be 0.5 to 2.0");std::vector<float> input(e->GetArrayLength(data));e->GetFloatArrayRegion(data,0,input.size(),input.data());soundtouch::SoundTouch st;st.setSampleRate(24000);st.setChannels(1);st.setTempo(speed);std::vector<float> out;float buffer[4096];
  for(size_t i=0;i<input.size();i+=4096){st.putSamples(input.data()+i,std::min(size_t(4096),input.size()-i));unsigned n;while((n=st.receiveSamples(buffer,4096)))out.insert(out.end(),buffer,buffer+n);}st.flush();unsigned n;while((n=st.receiveSamples(buffer,4096)))out.insert(out.end(),buffer,buffer+n);
  auto result=e->NewFloatArray(out.size());if(result)e->SetFloatArrayRegion(result,0,out.size(),out.data());return result;
 }catch(const std::exception& ex){e->ThrowNew(e->FindClass("java/lang/IllegalStateException"),ex.what());return nullptr;}
}
extern "C" JNIEXPORT void JNICALL Java_com_ultimatevoicegenerator_NativeEngine_encode(JNIEnv* e,jclass,jfloatArray data,jstring path,jstring fmt,jboolean normalize){
 try{std::vector<float> input(e->GetArrayLength(data));e->GetFloatArrayRegion(data,0,input.size(),input.data());auto p=str(e,path),format=str(e,fmt);float peak=.001f;for(float x:input)peak=std::max(peak,std::abs(x));float gain=normalize?std::min(4.f,.94f/peak):1.f;std::vector<int32_t> pcm(input.size());for(size_t i=0;i<input.size();i++)pcm[i]=int32_t(std::clamp(input[i]*gain,-1.f,1.f)*32767);
 if(format=="flac"){
  auto* enc=FLAC__stream_encoder_new();if(!enc)throw std::runtime_error("FLAC allocation failed");FLAC__stream_encoder_set_channels(enc,1);FLAC__stream_encoder_set_bits_per_sample(enc,16);FLAC__stream_encoder_set_sample_rate(enc,24000);FLAC__stream_encoder_set_compression_level(enc,5);FLAC__stream_encoder_set_total_samples_estimate(enc,pcm.size());
  bool ok=FLAC__stream_encoder_init_file(enc,p.c_str(),nullptr,nullptr)==FLAC__STREAM_ENCODER_INIT_STATUS_OK;
  if(ok)ok=FLAC__stream_encoder_process_interleaved(enc,pcm.data(),pcm.size());ok=FLAC__stream_encoder_finish(enc)&&ok;FLAC__stream_encoder_delete(enc);if(!ok)throw std::runtime_error("FLAC export failed");
 }else if(format=="mp3"){
  auto* enc=lame_init();if(!enc)throw std::runtime_error("MP3 allocation failed");lame_set_num_channels(enc,1);lame_set_in_samplerate(enc,24000);lame_set_out_samplerate(enc,24000);lame_set_brate(enc,160);lame_set_quality(enc,2);lame_set_bWriteVbrTag(enc,0);
  if(lame_init_params(enc)<0){lame_close(enc);throw std::runtime_error("MP3 configuration failed");}std::vector<short> samples(pcm.begin(),pcm.end());std::vector<unsigned char> bytes(input.size()*1.3+16384);int n=lame_encode_buffer(enc,samples.data(),samples.data(),samples.size(),bytes.data(),bytes.size());if(n<0){lame_close(enc);throw std::runtime_error("MP3 encode failed");}int tail=lame_encode_flush(enc,bytes.data()+n,bytes.size()-n);lame_close(enc);if(tail<0)throw std::runtime_error("MP3 flush failed");std::ofstream out(p,std::ios::binary);out.write((char*)bytes.data(),n+tail);if(!out)throw std::runtime_error("Could not save MP3");
 }else if(format=="wav"){
  std::ofstream out(p,std::ios::binary);out.write("RIFF",4);u32(out,36+pcm.size()*2);out.write("WAVEfmt ",8);u32(out,16);u16(out,1);u16(out,1);u32(out,24000);u32(out,48000);u16(out,2);u16(out,16);out.write("data",4);u32(out,pcm.size()*2);for(auto x:pcm)u16(out,uint16_t(x));if(!out)throw std::runtime_error("Could not save WAV");
 }else throw std::runtime_error("Unsupported audio format");
 }catch(const std::exception& ex){e->ThrowNew(e->FindClass("java/lang/IllegalStateException"),ex.what());}
}
