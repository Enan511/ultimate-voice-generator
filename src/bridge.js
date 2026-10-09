const waiting = new Map();
const listeners = new Set();
export const isAndroid = !!window.UltimateAndroid || !!window.__UVG_MOBILE_TEST__;
if (isAndroid) document.documentElement.classList.add("android", "dark");
const receive = (event) => {
  const m = event.data;
  if (m.type === "snapshot") {
    listeners.forEach((fn) => fn(m.data));
    return;
  }
  const request = waiting.get(m.id);
  if (!request) return;
  clearTimeout(request.timer);
  waiting.delete(m.id);
  if (m.error) request.reject(new Error(m.error));
  else request.resolve(m.result);
};
window.chrome?.webview?.addEventListener("message", receive);
window.__uvgReceive = (data) => receive({data});
export function command(command, payload) {
  if (window.__JARVIS_TEST__)
    return window.__JARVIS_TEST__.command(command, payload);
  if (!window.chrome?.webview && !window.UltimateAndroid)
    return Promise.reject(
      new Error(
        "Open Ultimate Voice Generator.exe to connect to the native desktop engine.",
      ),
    );
  return new Promise((resolve, reject) => {
    const id = crypto.randomUUID();
    const timer = setTimeout(() => {
      waiting.delete(id);
      reject(new Error("The request timed out. Check the queue before trying again."));
    }, 1800000);
    waiting.set(id, { resolve, reject, timer });
    if(window.UltimateAndroid) window.UltimateAndroid.postMessage(JSON.stringify({id, command, payload}));
    else window.chrome.webview.postMessage({ id, command, payload });
  });
}
export function subscribe(fn) {
  listeners.add(fn);
  return () => listeners.delete(fn);
}
export const audioUrl = (job) =>
  window.__JARVIS_TEST__?.audioUrl ??
  `https://audio.jarvis.local/${job.id}.${job.options.format}?v=${job.fileRevision || 0}`;
