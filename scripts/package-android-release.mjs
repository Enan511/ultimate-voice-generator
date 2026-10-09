import fs from 'node:fs';
import path from 'node:path';
import {createHash} from 'node:crypto';
const destination=path.resolve(process.argv[2] || 'release/github-v3.2.1');
const source=path.resolve('release/Ultimate Voice Generator Android.apk');
fs.mkdirSync(destination,{recursive:true});
const limit=1_500_000_000, whole=createHash('sha256');
const manifest={fileName:path.basename(source),bytes:fs.statSync(source).size,sha256:'',parts:[]};
let output,partHash,partBytes=0,partName;
function finishPart(){if(output===undefined)return;fs.closeSync(output);manifest.parts.push({name:partName,bytes:partBytes,sha256:partHash.digest('hex')});output=undefined;}
try{
 for await(const chunk of fs.createReadStream(source,{highWaterMark:1024*1024})){
  whole.update(chunk);
  for(let offset=0;offset<chunk.length;){
   if(output===undefined){partName=`Ultimate-Voice-Generator-Android.apk.${String(manifest.parts.length+1).padStart(3,'0')}`;output=fs.openSync(path.join(destination,partName),'w');partHash=createHash('sha256');partBytes=0;}
   const slice=chunk.subarray(offset,offset+Math.min(chunk.length-offset,limit-partBytes));
   let written=0;while(written<slice.length)written+=fs.writeSync(output,slice,written,slice.length-written);
   partHash.update(slice);partBytes+=slice.length;offset+=slice.length;
   if(partBytes===limit)finishPart();
  }
 }
 finishPart();
}finally{if(output!==undefined)fs.closeSync(output);}
if(manifest.parts.length!==2)throw Error('Expected two APK parts; update join instructions for this package size.');
manifest.sha256=whole.digest('hex');
fs.writeFileSync(path.join(destination,'Android-parts.json'),JSON.stringify(manifest,null,2)+'\n');
fs.copyFileSync('scripts/Join-Android-APK.ps1',path.join(destination,'Join-Android-APK.ps1'));
console.log(JSON.stringify(manifest,null,2));
