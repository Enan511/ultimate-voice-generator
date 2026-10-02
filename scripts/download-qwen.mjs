import fs from 'node:fs';
import path from 'node:path';
import {createHash} from 'node:crypto';
import {pipeline} from 'node:stream/promises';
const root=path.resolve('engines');
async function sha(file){const h=createHash('sha256');for await(const data of fs.createReadStream(file))h.update(data);return h.digest('hex');}
async function download(url,dest,hash){
  fs.mkdirSync(path.dirname(dest),{recursive:true});
  if(fs.existsSync(dest)&&await sha(dest)===hash){console.log('Ready',path.basename(dest));return;}
  for(let attempt=0;attempt<5;attempt++){
    try{const r=await fetch(url,{signal:AbortSignal.timeout(1800000)});if(!r.ok)throw Error(`${r.status}: ${url}`);
      await pipeline(r.body,fs.createWriteStream(dest+'.partial'));
      if(await sha(dest+'.partial')!==hash)throw Error('SHA256 mismatch: '+dest);
      fs.renameSync(dest+'.partial',dest);console.log('Verified',path.basename(dest));return;
    }catch(e){if(attempt===4)throw e;console.log(e.message);await new Promise(r=>setTimeout(r,5000*(attempt+1)));}
  }
}
const models=[
 ['qwen-talker-1.7b-base-Q8_0.gguf','4b9a33a236908dd9435a42f7a396e38038329d053b704342a6413c08544c4fda'],
 ['qwen-tokenizer-12hz-Q8_0.gguf','1883beeed99348fc35e23dd225e9082f93f6f8c109330a33d935baa8acdbfd94']
];
for(const [name,hash] of models)await download('https://huggingface.co/Serveurperso/Qwen3-TTS-GGUF/resolve/main/'+name,path.join(root,'qwen/models',name),hash);
await download('https://github.com/ggml-org/whisper.cpp/releases/download/b5130/whisper-bin-x64.zip',path.join(root,'downloads/whisper.zip'),'f9ec6c52a2e949b62ab51fa21d0d497958f9e41c3010c157c4e42932d5316f3c');
const model={path:'ggml-small.en.bin',lfs:{oid:'c6138d6d58ecc8322097e0f987c32f1be8bb0a18532a3f88f734d1bbf9c41e5d'}};
await download('https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-small.en.bin',path.join(root,'whisper/ggml-small.en.bin'),model.lfs.oid);
fs.writeFileSync(path.join(root,'manifest.json'),JSON.stringify({qwen:models,whisper:{name:model.path,sha256:model.lfs.oid},qwenSource:'ServeurpersoCom/qwentts.cpp',whisperVersion:'b5130'},null,2));
