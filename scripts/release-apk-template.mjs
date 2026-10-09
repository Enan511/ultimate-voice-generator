// Preserve the signed APK byte-for-byte while fetching its unchanged, stored model entries in CI.
import fs from 'node:fs';
import path from 'node:path';
import {createHash} from 'node:crypto';
const modelInfo=[
 ['qwen-talker-1.7b-base-Q8_0.gguf',2079448256,'4b9a33a236908dd9435a42f7a396e38038329d053b704342a6413c08544c4fda'],
 ['qwen-tokenizer-12hz-Q8_0.gguf',291150624,'1883beeed99348fc35e23dd225e9082f93f6f8c109330a33d935baa8acdbfd94'],
 ['ggml-small.en.bin',487614201,'c6138d6d58ecc8322097e0f987c32f1be8bb0a18532a3f88f734d1bbf9c41e5d']
];
const folder=path.resolve('release/cloud');
const template=path.join(folder,'Android-template.bin'),recipeFile=path.join(folder,'Android-template.json');
fs.mkdirSync(folder,{recursive:true});
function readAt(fd,length,position){const b=Buffer.alloc(length);let n=0;while(n<length){const got=fs.readSync(fd,b,n,length-n,position+n);if(!got)throw Error('Unexpected end of file');n+=got;}return b;}
function copyRange(input,output,start,bytes,hash){let done=0;while(done<bytes){const b=readAt(input,Math.min(1048576,bytes-done),start+done);let n=0;while(n<b.length)n+=fs.writeSync(output,b,n,b.length-n);hash?.update(b);done+=b.length;}}
async function sha(file){const hash=createHash('sha256');for await(const b of fs.createReadStream(file))hash.update(b);return hash.digest('hex');}
if(process.argv[2]==='create'){
 const apk=path.resolve('release/Ultimate Voice Generator Android.apk');
 const bytes=fs.statSync(apk).size,fd=fs.openSync(apk,'r');
 try{
  const tail=readAt(fd,Math.min(bytes,65557),Math.max(0,bytes-65557));
  let end=-1;for(let i=tail.length-22;i>=0;i--)if(tail.readUInt32LE(i)===0x06054b50&&i+22+tail.readUInt16LE(i+20)===tail.length){end=i;break;}
  if(end<0)throw Error('APK central directory not found');
  let central=tail.readUInt32LE(end+16);const models=[];
  for(let i=0;i<tail.readUInt16LE(end+10);i++){
   const h=readAt(fd,46,central);if(h.readUInt32LE(0)!==0x02014b50)throw Error('Invalid ZIP directory');
   const name=readAt(fd,h.readUInt16LE(28),central+46).toString('utf8');
   const match=modelInfo.find(m=>name===`assets/models/${m[0]}`);
   if(match){
    if(h.readUInt16LE(10)!==0||h.readUInt32LE(20)!==match[1]||h.readUInt32LE(24)!==match[1])throw Error('Model must be an uncompressed, exact-size APK entry');
    const local=h.readUInt32LE(42),header=readAt(fd,30,local);
    if(header.readUInt32LE(0)!==0x04034b50)throw Error('Invalid local ZIP header');
    models.push({name:match[0],bytes:match[1],sha256:match[2],start:local+30+header.readUInt16LE(26)+header.readUInt16LE(28)});
   }
   central+=46+h.readUInt16LE(28)+h.readUInt16LE(30)+h.readUInt16LE(32);
  }
  if(models.length!==3)throw Error('Expected all three bundled models');
  models.sort((a,b)=>a.start-b.start);const segments=[],out=fs.openSync(template,'w');let position=0;
  try{for(const m of models){if(m.start<position)throw Error('Overlapping ZIP entries');segments.push({kind:'template',bytes:m.start-position});copyRange(fd,out,position,m.start-position);segments.push({kind:'model',name:m.name,bytes:m.bytes,sha256:m.sha256});position=m.start+m.bytes;}segments.push({kind:'template',bytes:bytes-position});copyRange(fd,out,position,bytes-position);}finally{fs.closeSync(out);}
  const recipe={bytes,sha256:await sha(apk),templateSha256:await sha(template),segments};
  fs.writeFileSync(recipeFile,JSON.stringify(recipe,null,2)+'\n');console.log(`Prepared ${fs.statSync(template).size} byte APK template; signature remains unchanged after verified restoration.`);
 }finally{fs.closeSync(fd);}
}else if(process.argv[2]==='restore'){
 const cache=path.resolve(process.argv[3]),destination=path.resolve(process.argv[4]||'release/Ultimate Voice Generator Android.apk');
 const recipe=JSON.parse(fs.readFileSync(recipeFile,'utf8'));
 if(await sha(template)!==recipe.templateSha256)throw Error('Template checksum mismatch');
 for(const [name,bytes,hash] of modelInfo){const f=path.join(cache,name);if(fs.statSync(f).size!==bytes||await sha(f)!==hash)throw Error(`Model checksum mismatch: ${name}`);}
 if(fs.existsSync(destination))throw Error('Destination already exists');
 const partial=destination+'.partial',input=fs.openSync(template,'r'),output=fs.openSync(partial,'wx'),whole=createHash('sha256');let offset=0,total=0;
 try{
  for(const segment of recipe.segments){
   if(!Number.isSafeInteger(segment.bytes)||segment.bytes<0)throw Error('Invalid segment size');
   if(segment.kind==='template'){copyRange(input,output,offset,segment.bytes,whole);offset+=segment.bytes;}
   else{const model=modelInfo.find(m=>m[0]===segment.name&&m[1]===segment.bytes&&m[2]===segment.sha256);if(!model)throw Error('Unexpected model segment');const source=fs.openSync(path.join(cache,model[0]),'r');try{copyRange(source,output,0,segment.bytes,whole);}finally{fs.closeSync(source);}}
   total+=segment.bytes;
  }
 }finally{fs.closeSync(input);fs.closeSync(output);}
 if(total!==recipe.bytes||whole.digest('hex')!==recipe.sha256){fs.unlinkSync(partial);throw Error('Restored APK differs from original signed APK');}
 fs.renameSync(partial,destination);console.log('Restored original signed APK with matching SHA-256: '+recipe.sha256);
}else throw Error('Use create or restore <model-cache> [destination]');
