#include <jni.h>
#include <string>
#include <vector>
#include "whisper.h"
static std::string str(JNIEnv* e,jstring s){const char* p=e->GetStringUTFChars(s,nullptr);std::string r(p);e->ReleaseStringUTFChars(s,p);return r;}
extern "C" JNIEXPORT jstring JNICALL Java_com_ultimatevoicegenerator_NativeEngine_transcribe(JNIEnv* e,jclass,jstring path,jfloatArray pcm){
 auto file=str(e,path);auto options=whisper_context_default_params();options.use_gpu=false;
 auto* context=whisper_init_from_file_with_params(file.c_str(),options);
 if(!context){e->ThrowNew(e->FindClass("java/lang/IllegalStateException"),"Could not load the local transcription model");return nullptr;}
 std::vector<float> samples(e->GetArrayLength(pcm));e->GetFloatArrayRegion(pcm,0,samples.size(),samples.data());
 auto params=whisper_full_default_params(WHISPER_SAMPLING_GREEDY);params.n_threads=4;params.language="en";params.translate=false;params.no_context=true;params.no_timestamps=true;params.print_progress=false;params.print_realtime=false;params.print_timestamps=false;params.single_segment=false;params.initial_prompt=nullptr;
 int status=whisper_full(context,params,samples.data(),samples.size());std::string text;
 if(status==0)for(int i=0;i<whisper_full_n_segments(context);i++)text+=whisper_full_get_segment_text(context,i);
 whisper_free(context);
 if(status!=0){e->ThrowNew(e->FindClass("java/lang/IllegalStateException"),"Local transcription failed");return nullptr;}
 return e->NewStringUTF(text.c_str());
}
