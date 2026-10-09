import React, { useEffect, useRef, useState } from "react";
import { audioUrl, isAndroid } from "./bridge";
import {
  RotateCw,
  Trash2,
  FolderOpen,
  Check,
  AlertCircle,
  LoaderCircle,
} from "lucide-react";

export function ConfirmDialog({ title, children, onConfirm, onClose }) {
  const ref = useRef();
  const [busy, setBusy] = useState(false),
    [error, setError] = useState("");
  useEffect(() => {
    const d = ref.current;
    d.showModal();
    return () => d.close();
  }, []);
  return (
    <dialog
      ref={ref}
      className="regen-dialog"
      onCancel={(e) => {
        e.preventDefault();
        if (!busy) onClose();
      }}
    >
      <h2>{title}</h2>
      <div className="dialog-copy">{children}</div>
      {error && (
        <p role="alert" className="field-error">
          {error}
        </p>
      )}
      <div className="dialog-actions">
        <button className="button ghost" disabled={busy} onClick={onClose}>
          Cancel
        </button>
        <button
          className="button primary"
          disabled={busy}
          onClick={async () => {
            setBusy(true);
            try {
              await onConfirm();
              onClose();
            } catch (e) {
              setError(e.message);
              setBusy(false);
            }
          }}
        >
          {busy ? "Working…" : "Confirm"}
        </button>
      </div>
    </dialog>
  );
}

export function Recording({ job, act, onRegenerate, history = false }) {
  const audio = useRef();
  const [speed, setSpeed] = useState(1),
    [confirm, setConfirm] = useState(null),
    [error, setError] = useState("");
  const [ratings, setRatings] = useState(
    job.review || { voice: 0, tone: 0, emotion: 0, notes: "" },
  );
  const [saved, setSaved] = useState(false),
    [saving, setSaving] = useState(false);
  useEffect(() => {
    setSpeed(1);
    if (audio.current) {
      audio.current.playbackRate = 1;
      audio.current.defaultPlaybackRate = 1;
    }
    setRatings(job.review || { voice: 0, tone: 0, emotion: 0, notes: "" });
  }, [job.fileRevision]);
  const hasAudio = !!job.path;
  const update = async () => {
    if (audio.current) {
      audio.current.pause();
      audio.current.removeAttribute("src");
      audio.current.load();
    }
    try {
      await act("updateSpeed", { id: job.id, speed });
      setSpeed(1);
    } catch (e) {
      if (audio.current) {
        audio.current.src = audioUrl(job);
        audio.current.load();
      }
      throw e;
    }
  };
  const score = job.verification;
  return (
    <article className={`surface queue-card ${job.status}`}>
      <div className="job-top">
        <span className={`status ${job.status}`}>
          {job.status === "completed" ? (
            <Check size={14} />
          ) : (
            <AlertCircle size={14} />
          )}{" "}
          {job.status === "review"
            ? "Needs review"
            : job.status === "completed"
              ? score
                ? "Words checked"
                : "Legacy · not checked"
              : "Failed"}
        </span>
        <span className="small muted">
          {new Date(job.created).toLocaleString()} ·{" "}
          {job.options.format.toUpperCase()}
        </span>
      </div>
      <p className="job-text">{job.text}</p>
      {job.spokenText && job.spokenText !== job.text && (
        <p className="small muted">Spoken text: {job.spokenText}</p>
      )}
      {job.error && <p className="job-error">{job.error}</p>}
      {hasAudio && (
        <>
          <audio
            key={job.fileRevision || 0}
            ref={audio}
            controls
            preload="none"
            src={job.fileBusy ? undefined : audioUrl(job)}
            aria-label={`Recording: ${job.text}`}
            onPlay={(e) => {
              document.querySelectorAll("audio").forEach((a) => {
                if (a !== e.currentTarget) a.pause();
              });
            }}
          />
          <div className="speed-row">
            <label>
              Preview speed <strong>{speed.toFixed(2)}×</strong>
              <input
                aria-label="Preview speed"
                type="range"
                min="0.5"
                max="2"
                step="0.05"
                value={speed}
                disabled={job.fileBusy}
                onChange={(e) => {
                  const rate = Number(e.target.value);
                  setSpeed(rate);
                  if (audio.current) {
                    audio.current.preservesPitch = true;
                    audio.current.playbackRate = rate;
                  }
                }}
              />
            </label>
            <button
              className="button secondary"
              disabled={job.fileBusy || speed === 1}
              onClick={() => setConfirm("speed")}
            >
              {job.fileBusy ? (
                <LoaderCircle size={15} className="spin" />
              ) : null}{" "}
              Update File
            </button>
          </div>
          <p className="small muted">
            Preview starts at 1.0×. Update File saves this speed without
            changing pitch and checks the words again.
          </p>
          {score && (
            <details className="validation-details">
              <summary>
                {score.passed
                  ? "ASR word check passed"
                  : "ASR found differences"}{" "}
                · {job.attempts || 1} attempt(s)
              </summary>
              <p>
                <strong>Heard:</strong>{" "}
                {score.transcript || "(no speech detected)"}
              </p>
              {score.differences?.length > 0 && (
                <ul>
                  {score.differences.map((d, i) => (
                    <li key={i}>{d}</li>
                  ))}
                </ul>
              )}
              <p className="small muted">
                Recognition can make mistakes, especially with numbers and
                names. This does not certify voice similarity or emotion.
              </p>
            </details>
          )}
          <details className="validation-details">
            <summary>
              Listening review · {job.options.delivery || "Neutral"}
            </summary>
            <p className="small muted">
              Listen to the reference in Settings, then rate this take. Qwen
              Base follows the reference; delivery labels are comparison goals,
              not model commands.
            </p>
            <div className="rating-grid">
              {[
                ["voice", "Voice match"],
                ["tone", "Tone"],
                ["emotion", "Emotion"],
              ].map(([key, label]) => (
                <label key={key}>
                  {label}
                  <select
                    aria-label={label}
                    value={ratings[key] || 0}
                    onChange={(e) => {
                      setRatings({ ...ratings, [key]: Number(e.target.value) });
                      setSaved(false);
                    }}
                  >
                    <option value={0}>Not rated</option>
                    {[1, 2, 3, 4, 5].map((v) => (
                      <option key={v} value={v}>
                        {v} / 5
                      </option>
                    ))}
                  </select>
                </label>
              ))}
            </div>
            <textarea
              aria-label="Listening notes"
              placeholder="What sounded right or wrong?"
              value={ratings.notes}
              onChange={(e) => {
                setRatings({ ...ratings, notes: e.target.value });
                setSaved(false);
              }}
            />
            <button
              className="button secondary"
              disabled={saving || job.fileBusy}
              onClick={async () => {
                setSaving(true);
                try {
                  await act("listeningReview", { id: job.id, review: ratings });
                  setSaved(true);
                } catch (e) {
                  setError(e.message);
                } finally {
                  setSaving(false);
                }
              }}
            >
              {saved ? "Review saved" : "Save listening review"}
            </button>
          </details>
        </>
      )}
      {error && <p className="field-error">{error}</p>}
      <div className="job-actions">
        <button
          className="button ghost"
          disabled={job.fileBusy}
          onClick={() => onRegenerate(job)}
        >
          <RotateCw size={15} /> Regenerate
        </button>
        {hasAudio && (
          <button
            className="button ghost"
            onClick={() => act("openFolder", job.id).catch(() => {})}
          >
            <FolderOpen size={15} /> {isAndroid ? "Save a copy" : "Show file"}
          </button>
        )}
        {(
          <button
            className="button ghost danger"
            disabled={job.fileBusy}
            onClick={() => setConfirm("delete")}
          >
            <Trash2 size={15} /> Delete record & file
          </button>
        )}
        <span className="small muted push-right">
          {job.seconds > 0 ? `${job.seconds.toFixed(1)}s` : ""}
        </span>
      </div>
      {confirm && (
        <ConfirmDialog
          title={
            confirm === "delete"
              ? "Delete this recording?"
              : "Replace the saved audio?"
          }
          onClose={() => setConfirm(null)}
          onConfirm={
            confirm === "delete" ? () => act("deleteRecord", job.id) : update
          }
        >
          <p>
            {confirm === "delete"
              ? "This removes the history record and its audio file from disk."
              : `The current file will be replaced at ${speed.toFixed(2)}× speed. Playback resets to 1.0× afterward. This cannot be undone.`}
          </p>
          <p className="small path">{job.path}</p>
        </ConfirmDialog>
      )}
    </article>
  );
}

export function History({ jobs, act, onRegenerate }) {
  const [query, setQuery] = useState(""),
    [page, setPage] = useState(0);
  const rows = jobs
    .filter(
      (j) =>
        ["completed", "review", "failed"].includes(j.status) &&
        j.text.toLowerCase().includes(query.toLowerCase()),
    )
    .sort((a, b) => b.created.localeCompare(a.created));
  const pages = Math.max(1, Math.ceil(rows.length / 20));
  useEffect(() => setPage((p) => Math.min(p, pages - 1)), [pages]);
  return (
    <>
      <div className="heading">
        <span className="eyebrow">YOUR LIBRARY</span>
        <h1>Every take, in one place.</h1>
        <p>Find, compare, edit, or remove past recordings.</p>
      </div>
      <label className="history-search">
        Search history
        <input
          aria-label="Search history"
          value={query}
          onChange={(e) => {
            setQuery(e.target.value);
            setPage(0);
          }}
          placeholder="Search prompt text…"
        />
      </label>
      <p className="muted history-count">{rows.length} records</p>
      <div className="queue-list">
        {rows.slice(page * 20, page * 20 + 20).map((j) => (
          <Recording
            key={j.id}
            job={j}
            act={act}
            onRegenerate={onRegenerate}
            history
          />
        ))}
      </div>
      {!rows.length && (
        <div className="empty">
          <h2>No recordings here yet</h2>
          <p>Finished and failed generations appear here automatically.</p>
        </div>
      )}
      {pages > 1 && (
        <div className="pagination">
          <button
            className="button ghost"
            disabled={!page}
            onClick={() => setPage(page - 1)}
          >
            Previous
          </button>
          <span>
            {page + 1} / {pages}
          </span>
          <button
            className="button ghost"
            disabled={page + 1 >= pages}
            onClick={() => setPage(page + 1)}
          >
            Next
          </button>
        </div>
      )}
    </>
  );
}
