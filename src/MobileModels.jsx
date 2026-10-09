import React, {useState} from 'react';
import {Download, Cpu, LoaderCircle, PackageOpen} from 'lucide-react';
export function MobileModels({state, act}) {
  const [error,setError]=useState('');
  const m=state.models;
  if(!m || m.ready) return null;
  return <section className="surface model-setup" aria-label="Offline voice setup">
    <div className="model-icon"><Cpu size={28}/></div>
    <div><span className="eyebrow">PRIVATE BY DESIGN</span><h2>Your voice, on your phone.</h2>
      <p>{m.bundled ? 'Your trained voice and transcription models are included. Preparing them for first use requires about 3 GB of additional free space. No download or training is needed.' : 'Install Qwen 1.7B and local transcription once (about 2.9 GB). Then create audio without internet or a PC. Keep at least 5 GB of storage free.'}</p>
      <p className="small muted">{m.bundled ? 'This happens only once. Keep the app open until preparation finishes.' : 'Connect to Wi-Fi. You can leave the app open or follow progress in its notification.'}</p>
      {m.busy && <><progress value={m.percent || 0} max="100" aria-label="Model setup progress"/><p role="status">{m.message} · {m.percent || 0}%</p></>}
      {(error || m.error) && <p role="alert" className="field-error">{error || m.error}</p>}
      <button className="button primary" disabled={m.busy} onClick={async()=>{setError('');try{await act('downloadModels')}catch(e){setError(e.message)}}}>
        {m.busy ? <LoaderCircle size={18} className="spin"/> : m.bundled ? <PackageOpen size={18}/> : <Download size={18}/>}{m.busy ? 'Preparing voice engine…' : m.bundled ? 'Prepare included models' : 'Install offline models'}
      </button>
    </div>
  </section>
}
