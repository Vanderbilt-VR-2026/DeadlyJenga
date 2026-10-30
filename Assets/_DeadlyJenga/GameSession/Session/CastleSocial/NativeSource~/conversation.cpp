#include "llama.h"
#include "sherpa-c-api.h"
#include <algorithm>
#include <atomic>
#include <chrono>
#include <cstring>
#include <map>
#include <memory>
#include <mutex>
#include <string>
#include <thread>
#include <vector>
#ifdef _WIN32
#define DJ extern "C" __declspec(dllexport)
#else
#define DJ extern "C" __attribute__((visibility("default")))
#endif
using Clock=std::chrono::steady_clock;
static thread_local std::string error;
struct Conversation {
    llama_model *model=nullptr;llama_context *context=nullptr;
    const SherpaOnnxOnlineRecognizer *recognizer=nullptr;
    const SherpaOnnxOfflineTts *tts=nullptr;
    std::map<int,const SherpaOnnxOnlineStream*> streams;
    std::atomic<bool> cancelled{false};Clock::time_point deadline;
    std::vector<float> audio;
    ~Conversation(){for(auto &s:streams)SherpaOnnxDestroyOnlineStream(s.second);if(tts)SherpaOnnxDestroyOfflineTts(tts);if(recognizer)SherpaOnnxDestroyOnlineRecognizer(recognizer);if(context)llama_free(context);if(model)llama_model_free(model);}
};
static bool abort_work(void *data){auto *c=static_cast<Conversation*>(data);return c->cancelled||Clock::now()>c->deadline;}
static int copy(const std::string &s,char *output,int capacity){int n=std::min(static_cast<int>(s.size()),capacity-1);if(n<0)return 0;std::memcpy(output,s.data(),n);output[n]=0;return n;}
DJ int dj_api_version(){return 1;}
DJ const char *dj_error(){return error.c_str();}
DJ void dj_cancel(Conversation *c){if(c)c->cancelled=true;}
DJ void dj_destroy(Conversation *c){delete c;}
DJ Conversation *dj_create(const char *backend,const char *model,const char *encoder,const char *decoder,const char *joiner,const char *tokens,const char *voiceModel,const char *voices,const char *voiceTokens,const char *espeak,int threads,int contextSize){
    try {
        auto c=std::make_unique<Conversation>();
        static std::once_flag initialized;
        std::call_once(initialized,[backend](){ggml_backend_load_all_from_path(backend);
#ifdef __ANDROID__
        if(ggml_backend_dev_count()==0)ggml_backend_load("libggml-cpu-android_armv8.0_1.so");
#endif
        llama_backend_init();});
        auto mp=llama_model_default_params();mp.n_gpu_layers=0;
        c->model=llama_model_load_from_file(model,mp);if(!c->model){error="Could not load the language model";return nullptr;}
        auto cp=llama_context_default_params();cp.n_ctx=std::clamp(contextSize,512,2048);cp.n_batch=32;cp.n_ubatch=32;cp.n_threads=cp.n_threads_batch=std::clamp(threads,1,2);
        c->context=llama_init_from_model(c->model,cp);if(!c->context){error="Could not allocate the language-model context";return nullptr;}
        llama_set_abort_callback(c->context,abort_work,c.get());
        SherpaOnnxOnlineRecognizerConfig asr{};asr.feat_config.sample_rate=16000;asr.feat_config.feature_dim=80;
        asr.model_config.transducer={encoder,decoder,joiner};asr.model_config.tokens=tokens;asr.model_config.provider="cpu";asr.model_config.num_threads=std::clamp(threads,1,2);asr.model_config.model_type="zipformer";
        asr.decoding_method="greedy_search";asr.max_active_paths=4;
        c->recognizer=SherpaOnnxCreateOnlineRecognizer(&asr);if(!c->recognizer){error="Could not load streaming speech recognition";return nullptr;}
        SherpaOnnxOfflineTtsConfig speech{};speech.model.kitten={voiceModel,voices,voiceTokens,espeak,1};speech.model.provider="cpu";speech.model.num_threads=std::clamp(threads,1,2);speech.max_num_sentences=1; speech.silence_scale=.15f;
        c->tts=SherpaOnnxCreateOfflineTts(&speech);if(!c->tts){error="Could not load the voice model";return nullptr;}
        error.clear();return c.release();
    }catch(const std::exception &e){error=e.what();return nullptr;}
}
DJ int dj_generate(Conversation *c,const char *system,const char *message,int tokens,int timeoutMs,char *output,int capacity){
    if(!c)return -1;
    try {
        c->cancelled=false;c->deadline=Clock::now()+std::chrono::milliseconds(std::clamp(timeoutMs,250,15000));
        std::string prompt="<|im_start|>system\n"+std::string(system)+"<|im_end|>\n<|im_start|>user\n"+std::string(message)+"<|im_end|>\n<|im_start|>assistant\n<think>\n\n</think>\n\n";
        auto vocab=llama_model_get_vocab(c->model);int count=-llama_tokenize(vocab,prompt.c_str(),static_cast<int>(prompt.size()),nullptr,0,true,true);
        if(count<1||count+tokens+8>static_cast<int>(llama_n_ctx(c->context))){error="Conversation context exceeds its token budget";return -1;}
        std::vector<llama_token> input(count);llama_tokenize(vocab,prompt.c_str(),static_cast<int>(prompt.size()),input.data(),count,true,true);
        llama_memory_clear(llama_get_memory(c->context),true);
        for(int i=0;i<count;i+=32){if(abort_work(c))return -2;auto batch=llama_batch_get_one(input.data()+i,std::min(32,count-i));if(llama_decode(c->context,batch)!=0){error="Language-model prefill was cancelled or failed";return -2;}std::this_thread::sleep_for(std::chrono::milliseconds(2));}
        auto *sampler=llama_sampler_chain_init(llama_sampler_chain_default_params());
        std::unique_ptr<llama_sampler,decltype(&llama_sampler_free)> guard(sampler,llama_sampler_free);
        llama_sampler_chain_add(sampler,llama_sampler_init_top_k(20));llama_sampler_chain_add(sampler,llama_sampler_init_top_p(.8f,1));llama_sampler_chain_add(sampler,llama_sampler_init_temp(.7f));llama_sampler_chain_add(sampler,llama_sampler_init_dist(LLAMA_DEFAULT_SEED));
        std::string text;
        for(int i=0;i<std::clamp(tokens,8,64);i++){
            if(abort_work(c))return -2;
            auto token=llama_sampler_sample(sampler,c->context,-1);if(llama_vocab_is_eog(vocab,token))break;
            char piece[512];int length=llama_token_to_piece(vocab,token,piece,sizeof(piece),0,false);if(length>0)text.append(piece,length);
            if(text.size()>12&&(text.back()=='.'||text.back()=='!'||text.back()=='?'||text.back()=='\n'))break;
            auto batch=llama_batch_get_one(&token,1);if(llama_decode(c->context,batch)!=0)return -2;
            std::this_thread::sleep_for(std::chrono::milliseconds(3));
        }
        return copy(text,output,capacity);
    }catch(const std::exception &e){error=e.what();return -1;}
}
DJ int dj_listen(Conversation *c,int speaker,const float *audio,int count,int final,char *output,int capacity){
    if(!c||count<0||count>96000)return -1;
    try {
        auto found=c->streams.find(speaker);
        if(found==c->streams.end()){if(c->streams.size()>=16)return -1;found=c->streams.emplace(speaker,SherpaOnnxCreateOnlineStream(c->recognizer)).first;}
        auto *stream=found->second;
        if(count)SherpaOnnxOnlineStreamAcceptWaveform(stream,16000,audio,count);
        if(final){std::vector<float> silence(9600,0);SherpaOnnxOnlineStreamAcceptWaveform(stream,16000,silence.data(),static_cast<int>(silence.size()));SherpaOnnxOnlineStreamInputFinished(stream);}
        while(SherpaOnnxIsOnlineStreamReady(c->recognizer,stream))SherpaOnnxDecodeOnlineStream(c->recognizer,stream);
        auto *result=SherpaOnnxGetOnlineStreamResult(c->recognizer,stream);int n=result?copy(result->text,output,capacity):0;
        if(result)SherpaOnnxDestroyOnlineRecognizerResult(result);
        if(final){SherpaOnnxDestroyOnlineStream(stream);c->streams.erase(found);}return n;
    }catch(const std::exception &e){error=e.what();return -1;}
}
DJ void dj_forget(Conversation *c,int speaker){if(!c)return;auto s=c->streams.find(speaker);if(s!=c->streams.end()){SherpaOnnxDestroyOnlineStream(s->second);c->streams.erase(s);}}
static int audio_callback(const float*,int,void *arg){return !static_cast<Conversation*>(arg)->cancelled;}
DJ int dj_synthesize(Conversation *c,const char *text,int speaker,float speed,const float **samples,int *sampleRate){
    if(!c)return -1;
    try {
        c->cancelled=false;
        auto *audio=SherpaOnnxOfflineTtsGenerateWithCallbackWithArg(c->tts,text,std::abs(speaker)%std::max(1,SherpaOnnxOfflineTtsNumSpeakers(c->tts)),std::clamp(speed,.8f,1.2f),audio_callback,c);
        if(!audio){error="Voice synthesis failed";return -1;}
        if(audio->n>480000){SherpaOnnxDestroyOfflineTtsGeneratedAudio(audio);error="Voice reply exceeds its audio budget";return -1;}
        c->audio.assign(audio->samples,audio->samples+audio->n);*sampleRate=audio->sample_rate;SherpaOnnxDestroyOfflineTtsGeneratedAudio(audio);*samples=c->audio.data();
        return c->cancelled?-2:static_cast<int>(c->audio.size());
    }catch(const std::exception &e){error=e.what();return -1;}
}
