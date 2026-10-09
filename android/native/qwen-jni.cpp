#include <jni.h>
#include <atomic>
#include <string>
#include <vector>
#include <mutex>
#include <stdexcept>
#include "qwen.h"
static qt_context* model=nullptr;
static std::atomic<bool> cancelled{false};
static std::mutex guard;
static qt_voice_ref voice{};
static std::string voiceKey;
static std::string str(JNIEnv* e,jstring s){if(!s)return "";const char* p=e->GetStringUTFChars(s,nullptr);std::string r(p);e->ReleaseStringUTFChars(s,p);return r;}
static void fail(JNIEnv* e,const std::string& m){e->ThrowNew(e->FindClass("java/lang/IllegalStateException"),m.c_str());}
extern "C" JNIEXPORT void JNICALL Java_com_ultimatevoicegenerator_NativeEngine_cancel(JNIEnv*,jclass){cancelled=true;}
extern "C" JNIEXPORT void JNICALL Java_com_ultimatevoicegenerator_NativeEngine_resetCancel(JNIEnv*,jclass){cancelled=false;}
extern "C" JNIEXPORT void JNICALL Java_com_ultimatevoicegenerator_NativeEngine_unload(JNIEnv*,jclass){std::lock_guard<std::mutex> l(guard);qt_voice_ref_free(&voice);voiceKey.clear();qt_free(model);model=nullptr;}
extern "C" JNIEXPORT jfloatArray JNICALL Java_com_ultimatevoicegenerator_NativeEngine_generate(JNIEnv* e,jclass,jstring talker,jstring codec,jfloatArray ref,jstring key,jstring transcript,jstring prompt,jlong seed){
 std::lock_guard<std::mutex> l(guard);
 try{
  auto t=str(e,talker),c=str(e,codec),k=str(e,key),r=str(e,transcript),p=str(e,prompt);
  if(cancelled)throw std::runtime_error("Cancelled");
  if(!model){qt_init_params init;qt_init_default_params(&init);init.talker_path=t.c_str();init.codec_path=c.c_str();init.use_fa=false;init.codec_chunk_sec=4;model=qt_init(&init);if(!model)throw std::runtime_error(qt_last_error());}
  if(voiceKey!=k){qt_voice_ref_free(&voice);voiceKey.clear();std::vector<float> samples(e->GetArrayLength(ref));e->GetFloatArrayRegion(ref,0,samples.size(),samples.data());if(qt_extract_voice_ref(model,samples.data(),samples.size(),&voice)!=QT_STATUS_OK)throw std::runtime_error(qt_last_error());voiceKey=k;}
  qt_tts_params args;qt_tts_default_params(&args);args.text=p.c_str();args.lang="english";args.ref_text=r.c_str();args.seed=seed;args.max_new_tokens=1024;args.ref_spk_emb=voice.ref_spk_emb;args.ref_spk_dim=voice.ref_spk_dim;args.ref_codes=voice.ref_codes;args.ref_T=voice.ref_T;args.cancel=[](void*){return cancelled.load();};
  qt_audio out{};auto status=qt_synthesize(model,&args,&out);
  if(status!=QT_STATUS_OK){qt_audio_free(&out);throw std::runtime_error(qt_last_error());}
  auto result=e->NewFloatArray(out.n_samples);if(result)e->SetFloatArrayRegion(result,0,out.n_samples,out.samples);qt_audio_free(&out);return result;
 }catch(const std::exception& ex){fail(e,ex.what());return nullptr;}
}
