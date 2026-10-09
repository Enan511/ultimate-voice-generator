import React, { useState, useEffect, useRef, useMemo, memo } from "react";
import { createRoot } from "react-dom/client";
import {
  AudioLines,
  Plus,
  Layers,
  Settings2,
  ChevronRight,
  ArrowLeft,
  Trash2,
  Check,
  AlertCircle,
  Clock,
  LoaderCircle,
  FolderOpen,
  RotateCw,
  Pause,
  Play,
  Square,
  Upload,
  X,
  ArrowUpRight,
  ShieldCheck,
  Volume2,
} from "lucide-react";
import { sanitize, stageLines, pronounce } from "./text";
import { command, subscribe, audioUrl, isAndroid } from "./bridge";
import { MobileModels } from "./MobileModels";
import { History, Recording } from "./Recordings";
import { ReferenceSettings, LexiconSettings } from "./VoiceSettings";
import "./styles.css";
import "./mobile.css";

const initial = {
  settings: {
    folder: "",
    format: "flac",
    reference: "",
    speed: 1,
    tone: "natural",
    normalize: true,
    referenceTranscript: "",
    referenceVerified: false,
    lexicon: [],
    maxRetries: 1,
    delivery: "Neutral",
    seed: 42,
  },
  jobs: [],
  paused: false,
  engine: "Engine idle · loads when needed",
};
const statusNames = {
  pending: "Waiting",
  processing: "Processing",
  completed: "Completed",
  failed: "Failed",
  review: "Needs review",
};
const statusIcons = {
  pending: Clock,
  processing: LoaderCircle,
  completed: Check,
  failed: AlertCircle,
  review: AlertCircle,
};
const nameOf = (path) => path?.split(/[\\/]/).pop() || "Choose a file";

function App() {
  const [state, setState] = useState(initial),
    [ready, setReady] = useState(false),
    [view, setView] = useState("compose");
  const [raw, setRaw] = useState(
      () => localStorage.getItem("jarvis.raw") || "",
    ),
    [staged, setStaged] = useState([]);
  const [error, setError] = useState(""),
    [busy, setBusy] = useState(false),
    [regen, setRegen] = useState(null);
  const inputFile = useRef();
  useEffect(() => {
    const off = subscribe(setState);
    command("bootstrap")
      .then((s) => {
        setState(s);
        setReady(true);
        return command("uiReady");
      })
      .catch((e) => setError(e.message));
    return off;
  }, []);
  useEffect(() => {
    const timer = setTimeout(() => {
      try {
        localStorage.setItem("jarvis.raw", raw);
      } catch {}
    }, 300);
    return () => clearTimeout(timer);
  }, [raw]);
  const act = async (name, payload) => {
    try {
      const result = await command(name, payload);
      if (result?.jobs) setState(result);
      return result;
    } catch (e) {
      setError(e.message);
      throw e;
    }
  };
  const safe = (name, payload) => act(name, payload).catch(() => {});
  const review = () => {
    setStaged(
      stageLines(raw).map((text) => ({ id: crypto.randomUUID(), text })),
    );
    setView("review");
  };
  const confirm = async () => {
    setBusy(true);
    try {
      await act(
        "enqueue",
        staged.map((p) => ({ text: sanitize(p.text) })),
      );
      setStaged([]);
      setRaw("");
      setView("queue");
    } catch {
    } finally {
      setBusy(false);
    }
  };
  const importFiles = async (event) => {
    try {
      const texts = await Promise.all(
        [...event.target.files].map((f) => f.text()),
      );
      setRaw((previous) => [previous, ...texts].filter(Boolean).join("\n"));
    } catch (e) {
      setError(e.message);
    }
    event.target.value = "";
  };
  const queueJobs = useMemo(
    () => state.jobs.filter((j) => !j.archived),
    [state.jobs],
  );
  const counts = useMemo(
    () => ({
      waiting: queueJobs.filter((j) => j.status === "pending").length,
      processing: queueJobs.filter((j) => j.status === "processing").length,
      done: queueJobs.filter((j) => j.status === "completed").length,
      failed: queueJobs.filter((j) => j.status === "failed").length,
      review: queueJobs.filter((j) => j.status === "review").length,
    }),
    [queueJobs],
  );
  return (
    <div className="app-shell">
      <aside className="sidebar">
        <a
          className="brand"
          href="#"
          onClick={(e) => {
            e.preventDefault();
            setView("compose");
          }}
        >
          <span className="brand-icon">
            <img src="/app.ico" alt="" className="app-logo" />
          </span>
          <span>
            ULTIMATE<span className="brand-sub">VOICE GENERATOR</span>
          </span>
        </a>
        <div className="nav-label">WORKSPACE</div>
        <nav aria-label="Main navigation">
          <Nav
            icon={Plus}
            active={view === "compose" || view === "review"}
            onClick={() => setView(staged.length ? "review" : "compose")}
          >
            Create audio
          </Nav>
          <Nav
            icon={Layers}
            active={view === "queue"}
            onClick={() => setView("queue")}
          >
            Queue{" "}
            <span className="nav-count">
              {counts.waiting + counts.processing || queueJobs.length}
            </span>
          </Nav>
          <Nav
            icon={Clock}
            active={view === "history"}
            onClick={() => setView("history")}
          >
            History
          </Nav>
          <Nav
            icon={Settings2}
            active={view === "settings"}
            onClick={() => setView("settings")}
          >
            Settings
          </Nav>
        </nav>
        <div className="sidebar-bottom">
          <div className="local-badge">
            <span className="dot" /> Local processing
          </div>
          <p>Your scripts stay on this device.</p>
          <div className="engine-label">{state.engine}</div>
        </div>
      </aside>
      <div className="main-shell">
        <header className="topbar">
          <span>
            Workspace <ChevronRight size={14} />{" "}
            <strong>
              {view === "review"
                ? "Review batch"
                : view === "queue"
                  ? "Generation queue"
                  : view === "settings"
                    ? "Settings"
                    : view === "history"
                      ? "History"
                      : "Create audio"}
            </strong>
          </span>
          <span className="version">VERSION 3.2</span>
        </header>
        <main>
          {isAndroid && <MobileModels state={state} act={act} />}
          {error && (
            <div className="alert" role="alert">
              <AlertCircle size={18} />
              <span>{error}</span>
              <button
                className="icon-btn"
                aria-label="Dismiss error"
                onClick={() => setError("")}
              >
                <X size={16} />
              </button>
            </div>
          )}
          {state.notice && <div className="alert">{state.notice}</div>}
          {view === "compose" && (
            <>
              <Heading
                eyebrow="01 / COMPOSE"
                title="A voice for every line."
                description="Write your prompts, review pronunciation, then generate and check every line locally."
              />
              <section className="surface compose-panel">
                <div className="section-head">
                  <div>
                    <h2>Your script</h2>
                    <p>One line = one recording.</p>
                  </div>
                  <button
                    className="button secondary"
                    onClick={() => inputFile.current.click()}
                  >
                    <Upload size={16} /> Import text
                  </button>
                  <input
                    ref={inputFile}
                    type="file"
                    accept=".txt,text/plain"
                    multiple
                    hidden
                    onChange={importFiles}
                  />
                </div>
                <textarea
                  className="script-input"
                  aria-label="Batch prompts"
                  placeholder={
                    "Good morning, sir. Systems awake.\nDriver status remains under evaluation.\nAll systems are operating within normal parameters."
                  }
                  value={raw}
                  maxLength={500000}
                  onChange={(e) => setRaw(e.target.value)}
                />
                <div className="input-footer">
                  <span>
                    {stageLines(raw).length} prompts detected{" "}
                    <span className="separator">/</span>{" "}
                    {raw.length.toLocaleString()} characters
                  </span>
                  <span>Draft saved on this device</span>
                </div>
              </section>
              <div className="info-strip">
                <ShieldCheck size={19} />
                <div>
                  <strong>Your punctuation stays intact.</strong>
                  <p>
                    Quotes, apostrophes, brackets, dashes, and other punctuation
                    are preserved. Only spacing is normalized. Review
                    pronunciation changes before generation.
                  </p>
                </div>
              </div>
              <div className="action-footer">
                <div className="output-summary">
                  <FolderOpen size={18} />
                  <div>
                    <span>
                      EXPORTING AS {state.settings.format.toUpperCase()}
                    </span>
                    <button
                      className="text-link path"
                      onClick={() => setView("settings")}
                    >
                      {state.settings.folder ||
                        "Choose your output folder in Settings"}
                    </button>
                  </div>
                </div>
                <button
                  className="button primary"
                  disabled={
                    !ready ||
                    !stageLines(raw).length ||
                    stageLines(raw).length > 500
                  }
                  onClick={review}
                >
                  Review {stageLines(raw).length || ""} prompts{" "}
                  <ChevronRight size={18} />
                </button>
              </div>
              {stageLines(raw).length > 500 && (
                <p className="field-error">Use up to 500 prompts per batch.</p>
              )}
            </>
          )}
          {view === "review" && (
            <>
              <Heading
                eyebrow="02 / REVIEW"
                title="Make every word count."
                description="Edit each prompt and inspect pronunciation replacements before confirming."
              />
              <div className="review-toolbar">
                <span className="pill">{staged.length} prompts staged</span>
                <button
                  className="button ghost"
                  onClick={() => {
                    setStaged([]);
                    setView("compose");
                  }}
                >
                  <ArrowLeft size={16} /> Revert to raw input
                </button>
              </div>
              <div className="review-list">
                {staged.map((p, index) => (
                  <div className="surface review-card" key={p.id}>
                    <span className="item-number">
                      {String(index + 1).padStart(2, "0")}
                    </span>
                    <div className="grow">
                      <textarea
                        aria-label={`Prompt ${index + 1}`}
                        value={p.text}
                        rows={2}
                        maxLength={1500}
                        onChange={(e) =>
                          setStaged((items) =>
                            items.map((x) =>
                              x.id === p.id
                                ? { ...x, text: e.target.value }
                                : x,
                            ),
                          )
                        }
                        onBlur={() =>
                          setStaged((items) =>
                            items.map((x) =>
                              x.id === p.id
                                ? { ...x, text: sanitize(x.text) }
                                : x,
                            ),
                          )
                        }
                      />
                      {p.text !== sanitize(p.text) && (
                        <p className="clean-preview">
                          Will synthesize: {sanitize(p.text) || "(empty)"}
                        </p>
                      )}
                      {!sanitize(p.text) && (
                        <p className="field-error">
                          Enter a prompt or remove this item.
                        </p>
                      )}
                      <span className="small muted">
                        {sanitize(p.text).length} characters
                      </span>
                      <p className="clean-preview">
                        Model input:{" "}
                        {pronounce(sanitize(p.text), state.settings.lexicon)}
                      </p>
                    </div>
                    <button
                      className="icon-btn"
                      aria-label={`Delete prompt ${index + 1}`}
                      onClick={() =>
                        setStaged((items) => items.filter((x) => x.id !== p.id))
                      }
                    >
                      <Trash2 size={17} />
                    </button>
                  </div>
                ))}
              </div>
              {!staged.length && (
                <Empty
                  title="Nothing staged"
                  description="Return to your script and add a few lines."
                />
              )}
              <div className="action-footer sticky-footer">
                <span className="muted">
                  {state.settings.format.toUpperCase()} · Original speed · Words
                  checked locally
                  {state.paused ? " · Queue paused" : ""}
                </span>
                <button
                  className="button primary"
                  disabled={
                    busy ||
                    !staged.length ||
                    staged.some(
                      (p) =>
                        !sanitize(p.text) || sanitize(p.text).length > 1500,
                    )
                  }
                  onClick={confirm}
                >
                  {busy ? (
                    <LoaderCircle className="spin" size={18} />
                  ) : (
                    <Play size={18} />
                  )}{" "}
                  Confirm & Start Generation
                </button>
              </div>
            </>
          )}
          {view === "queue" && (
            <Queue
              state={{ ...state, jobs: queueJobs }}
              counts={counts}
              act={act}
              onRegenerate={setRegen}
              onCreate={() => setView("compose")}
            />
          )}
          {view === "history" && (
            <History jobs={state.jobs} act={act} onRegenerate={setRegen} />
          )}
          {view === "settings" && (
            <Settings
              settings={state.settings}
              act={act}
              onError={setError}
              processing={counts.processing > 0}
            />
          )}
        </main>
      </div>
      {regen && (
        <Regenerate
          job={regen}
          settings={state.settings}
          onClose={() => setRegen(null)}
          onSubmit={async (prompts) => {
            await act("enqueue", prompts);
            setRegen(null);
            setView("queue");
          }}
        />
      )}
    </div>
  );
}

function Nav({ icon: Icon, active, onClick, children }) {
  return (
    <button className={`nav-item ${active ? "active" : ""}`} onClick={onClick}>
      <Icon size={19} />
      {children}
    </button>
  );
}
function Heading({ eyebrow, title, description }) {
  return (
    <div className="heading">
      <span className="eyebrow">{eyebrow}</span>
      <h1>{title}</h1>
      <p>{description}</p>
    </div>
  );
}
function Empty({ title, description }) {
  return (
    <div className="empty">
      <AudioLines size={34} />
      <h2>{title}</h2>
      <p>{description}</p>
    </div>
  );
}

function Queue({ state, counts, act, onRegenerate, onCreate }) {
  const [filter, setFilter] = useState("all"),
    [page, setPage] = useState(0);
  const filtered = state.jobs.filter(
    (j) => filter === "all" || j.status === filter,
  );
  const pageCount = Math.max(1, Math.ceil(filtered.length / 30));
  useEffect(() => {
    setPage((p) => Math.min(p, pageCount - 1));
  }, [pageCount]);
  const finished = counts.done + counts.failed + counts.review;
  const progress = state.jobs.length
    ? Math.round((finished / state.jobs.length) * 100)
    : 0;
  return (
    <>
      <Heading
        eyebrow="03 / GENERATE"
        title="Your words, taking shape."
        description="The queue runs in the background. Keep writing, review another batch, or listen to completed recordings."
      />
      <section className="surface progress-panel">
        <div className="section-head">
          <div>
            <h2>
              {state.paused
                ? "Queue paused"
                : counts.processing
                  ? "Generation in progress"
                  : counts.waiting
                    ? "Ready to continue"
                    : state.jobs.length
                      ? "All tasks finished"
                      : "Ready when you are"}
            </h2>
            <p>
              {finished} of {state.jobs.length} finished · {counts.done}{" "}
              completed{counts.failed ? ` · ${counts.failed} failed` : ""}
              {counts.review ? ` · ${counts.review} need review` : ""}
            </p>
          </div>
          <strong className="percent">
            {progress}
            <span>%</span>
          </strong>
        </div>
        <div
          className="progress-track"
          role="progressbar"
          aria-valuenow={progress}
          aria-valuemin={0}
          aria-valuemax={100}
          aria-label="Overall queue progress"
        >
          <div style={{ width: progress + "%" }} />
        </div>
        <div className="queue-actions">
          <button
            className="button secondary"
            onClick={() => act("pause", !state.paused).catch(()=>{})}
            disabled={!counts.waiting && !counts.processing}
          >
            {state.paused ? <Play size={16} /> : <Pause size={16} />}{" "}
            {state.paused ? "Resume queue" : "Pause after current"}
          </button>
          <button
            className="button ghost"
            disabled={!counts.processing}
            onClick={() => act("cancel").catch(()=>{})}
          >
            <Square size={14} /> Cancel current
          </button>
          <button
            className="button ghost push-right"
            disabled={!finished}
            onClick={() => act("clear").catch(()=>{})}
          >
            Move finished to history
          </button>
        </div>
      </section>
      <div className="filters" role="group" aria-label="Queue filter">
        {[
          ["all", "All", state.jobs.length],
          ["pending", "Waiting", counts.waiting],
          ["processing", "Processing", counts.processing],
          ["completed", "Completed", counts.done],
          ["failed", "Failed", counts.failed],
          ["review", "Needs review", counts.review],
        ].map(([key, title, count]) => (
          <button
            key={key}
            className={filter === key ? "selected" : ""}
            onClick={() => {
              setFilter(key);
              setPage(0);
            }}
          >
            {title}
            <span>{count}</span>
          </button>
        ))}
      </div>
      <div className="queue-list">
        {filtered.slice(page * 30, (page + 1) * 30).map((job) => (
          <QueueItem
            key={job.id}
            job={job}
            act={act}
            onRegenerate={onRegenerate}
          />
        ))}
      </div>
      {!filtered.length && (
        <Empty
          title={
            state.jobs.length ? "No tasks in this view" : "Your queue is clear"
          }
          description={
            state.jobs.length
              ? "Try another status filter."
              : "Add a batch to create your first recordings."
          }
        />
      )}
      <div className="action-footer">
        <button className="button secondary" onClick={onCreate}>
          <Plus size={16} /> Create another batch
        </button>
        {pageCount > 1 && (
          <div className="pagination">
            <button
              className="button ghost"
              disabled={page === 0}
              onClick={() => setPage((p) => p - 1)}
            >
              Previous
            </button>
            <span>
              {page + 1} / {pageCount}
            </span>
            <button
              className="button ghost"
              disabled={page + 1 >= pageCount}
              onClick={() => setPage((p) => p + 1)}
            >
              Next
            </button>
          </div>
        )}
      </div>
    </>
  );
}
const QueueItem = memo(function QueueItem({ job, act, onRegenerate }) {
  if (["completed", "review", "failed"].includes(job.status))
    return <Recording job={job} act={act} onRegenerate={onRegenerate} />;
  const Icon = statusIcons[job.status] || Clock;
  return (
    <article className={`surface queue-card ${job.status}`}>
      <div className="job-top">
        <span className={`status ${job.status}`}>
          <Icon
            size={14}
            className={job.status === "processing" ? "spin" : ""}
          />
          {statusNames[job.status]}
        </span>
        <span className="small muted">
          {job.options.format.toUpperCase()} · {job.options.speed}×
        </span>
      </div>
      <p className="job-text">{job.text}</p>
      {job.status === "processing" && (
        <div className="processing-line">
          <span className="pulse" />
          {job.phase}
        </div>
      )}
      {job.error && (
        <div className="job-error">
          <AlertCircle size={16} />
          <span>{job.error}</span>
        </div>
      )}
      {job.status === "completed" && (
        <audio
          controls
          preload="none"
          src={audioUrl(job)}
          aria-label={`Recording: ${job.text}`}
          onPlay={(e) =>
            document.querySelectorAll("audio").forEach((a) => {
              if (a !== e.currentTarget) a.pause();
            })
          }
        />
      )}
      {(job.status === "completed" || job.status === "failed") && (
        <div className="job-actions">
          <button className="button ghost" onClick={() => onRegenerate(job)}>
            <RotateCw size={15} /> Regenerate
          </button>
          {job.status === "completed" && (
            <button
              className="button ghost"
              onClick={() => act("openFolder", job.id)}
            >
              <FolderOpen size={15} /> {isAndroid ? "Save a copy" : "Show file"}
            </button>
          )}
          {job.seconds > 0 && (
            <span className="small muted push-right">
              Generated in {job.seconds.toFixed(1)}s
            </span>
          )}
        </div>
      )}
      {job.status === "pending" && (
        <button
          className="button ghost compact"
          onClick={() => act("remove", job.id).catch(()=>{})}
        >
          <X size={14} /> Remove from queue
        </button>
      )}
    </article>
  );
});

function Settings({ settings, act, onError, processing }) {
  const [transcribing, setTranscribing] = useState(false);
  const [value, setValue] = useState(settings),
    [saved, setSaved] = useState(false),
    [saving, setSaving] = useState(false);
  const change = (v) => {
    setValue(v);
    setSaved(false);
  };
  const pick = async (command, field) => {
    try {
      const path = await act(command);
      if (path) change({ ...value, [field]: path });
    } catch {}
  };
  const save = async () => {
    setSaving(true);
    try {
      const result = await act("settings", value);
      if (result?.settings) setValue(result.settings);
      setSaved(true);
    } catch (e) {
      onError(e.message);
    } finally {
      setSaving(false);
    }
  };
  return (
    <>
      <Heading
        eyebrow="PREFERENCES"
        title="Set your defaults."
        description="These settings are captured when you confirm a batch. Existing queued items keep their own settings."
      />
      <section className="surface settings-section">
        <h2>Export destination</h2>
        <p className="muted">
          Every prompt gets a unique filename. Use Update File on a recording to
          deliberately replace its speed.
        </p>
        <div className="path-picker">
          <FolderOpen size={20} />
          <span className="path">{isAndroid && value.folder?.startsWith("content:") ? "Selected phone folder" : value.folder || "No folder selected"}</span>
          <button
            className="button secondary"
            onClick={() => pick("pickFolder", "folder")}
          >
            Choose folder
          </button>
        </div>
        <h3>Audio format</h3>
        <div className="format-grid">
          {[
            ["flac", "FLAC", "Lossless, smaller files"],
            ["wav", "WAV", "Uncompressed audio"],
            ["mp3", "MP3", "Easy sharing"],
          ].map(([key, title, desc]) => (
            <button
              key={key}
              className={`format-option ${value.format === key ? "chosen" : ""}`}
              onClick={() => change({ ...value, format: key })}
              aria-pressed={value.format === key}
            >
              <div>
                <strong>{title}</strong>
                {value.format === key && <Check size={17} />}
              </div>
              <span>{desc}</span>
            </button>
          ))}
        </div>
      </section>
      <ReferenceSettings value={value} change={change} act={act} onBusy={setTranscribing} />
      <LexiconSettings value={value} change={change} />
      <section className="surface settings-section">
        <label className="checkbox">
          <input
            type="checkbox"
            checked={value.normalize}
            onChange={(e) => change({ ...value, normalize: e.target.checked })}
          />{" "}
          Normalize loudness
        </label>
      </section>
      <section className="surface settings-section engine-settings">
        <div>
          <h2>Local engine</h2>
          {!isAndroid && <label>Compute<select aria-label="Compute" value={value.backend||'auto'} onChange={e=>change({...value,backend:e.target.value})}><option value="auto">GPU when available · Vulkan</option><option value="cpu">CPU · compatibility mode</option></select></label>}
          {isAndroid && <p className="muted">On-device ARM64 processing · no PC connection. Large batches can warm your phone and take time.</p>}
          <p className="muted">
            Qwen3-TTS 1.7B Base · local C++ inference. Whisper checks spoken
            words. The model loads only when generation starts. Release it to
            free memory between sessions.
          </p>
        </div>
        <button
          className="button secondary"
          disabled={processing}
          onClick={() => act("unload").catch(() => {})}
        >
          Release model memory
        </button>
      </section>
      <div className="action-footer">
        <span className="muted">
          {saved
            ? "Preferences saved on this device."
            : "Changes apply to new batches."}
        </span>
        <button
          className="button primary"
          disabled={saving || transcribing || !value.folder}
          onClick={save}
        >
          {saving ? (
            <LoaderCircle size={17} className="spin" />
          ) : (
            <Check size={17} />
          )}{" "}
          Save settings
        </button>
      </div>
    </>
  );
}

function Regenerate({ job, settings, onClose, onSubmit }) {
  const ref = useRef();
  const [text, setText] = useState(job.text),
    [notes, setNotes] = useState(""),
    [compare, setCompare] = useState(false),
    [busy, setBusy] = useState(false),
    [error, setError] = useState("");
  useEffect(() => {
    const dialog = ref.current;
    dialog.showModal();
    return () => dialog.close();
  }, []);
  const submit = async () => {
    setBusy(true);
    try {
      const options = {
        ...settings,
        seed: Math.floor(Math.random() * 2000000000),
        speed: 1,
      };
      const prompts = [{ text: sanitize(text), notes, options }];
      if (compare)
        prompts.push({
          text: sanitize(text),
          notes: notes + " · comparison take",
          options: { ...options, seed: options.seed + 100 },
        });
      await onSubmit(prompts);
    } catch (e) {
      setError(e.message);
      setBusy(false);
    }
  };
  return (
    <dialog
      className="regen-dialog"
      ref={ref}
      onCancel={(e) => {
        e.preventDefault();
        if (!busy) onClose();
      }}
    >
      <div className="dialog-head">
        <div>
          <span className="eyebrow">ANOTHER TAKE</span>
          <h2>Refine this recording.</h2>
        </div>
        <button
          className="icon-btn"
          aria-label="Close regeneration"
          disabled={busy}
          onClick={onClose}
        >
          <X />
        </button>
      </div>
      <p className="muted">
        New takes use your current reference and pronunciation settings. The
        original remains in History.
      </p>
      <label>
        Text to synthesize
        <textarea
          aria-label="Regeneration text"
          rows={4}
          value={text}
          maxLength={1500}
          onChange={(e) => setText(e.target.value)}
        />
      </label>
      <p className="clean-preview">
        Model input: {pronounce(sanitize(text), settings.lexicon)}
      </p>
      <label>
        Listening goals / notes
        <input
          aria-label="Additional instructions"
          value={notes}
          onChange={(e) => setNotes(e.target.value)}
          placeholder="For example: compare warmth and emphasis on sir"
        />
      </label>
      <p className="small muted">
        Notes are saved for comparison, not spoken or sent as emotion commands.
        Qwen Base uses the reference delivery.
      </p>
      <label className="checkbox">
        <input
          type="checkbox"
          checked={compare}
          onChange={(e) => setCompare(e.target.checked)}
        />{" "}
        Generate two takes for a listening comparison
      </label>
      {error && <p className="field-error">{error}</p>}
      <div className="dialog-actions">
        <button className="button ghost" disabled={busy} onClick={onClose}>
          Cancel
        </button>
        <button
          className="button primary"
          disabled={busy || !sanitize(text)}
          onClick={submit}
        >
          {busy ? (
            <LoaderCircle size={16} className="spin" />
          ) : (
            <Plus size={16} />
          )}{" "}
          {compare ? "Add two comparison takes" : "Add new take to queue"}
        </button>
      </div>
    </dialog>
  );
}
createRoot(document.getElementById("root")).render(<App />);
