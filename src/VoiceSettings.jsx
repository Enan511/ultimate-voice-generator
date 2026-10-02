import React, { useState } from "react";
import { Plus, Trash2, Volume2, LoaderCircle } from "lucide-react";

export function ReferenceSettings({ value, change, act }) {
  const [busy, setBusy] = useState(false),
    [error, setError] = useState("");
  const name = value.reference?.split(/[\\/]/).pop();
  const pick = async () => {
    try {
      const path = await act("pickReference");
      if (path)
        change({
          ...value,
          reference: path,
          referenceTranscript: "",
          referenceVerified: false,
          referenceHash: "",
        });
    } catch (e) {
      setError(e.message);
    }
  };
  return (
    <section className="surface settings-section">
      <h2>Reference voice & exact transcript</h2>
      <p className="muted">
        Use one clean, expressive speaker, ideally 5–20 seconds, with no music
        or overlapping speech. References can be up to 30 seconds.
      </p>
      <div className="path-picker">
        <Volume2 size={20} />
        <span className="path">{name || "Choose a reference"}</span>
        <button className="button secondary" onClick={pick}>
          Choose reference
        </button>
      </div>
      {name && (
        <audio
          controls
          preload="none"
          src={`https://audio.jarvis.local/references/${encodeURIComponent(name)}`}
          aria-label="Reference recording"
        />
      )}
      <label>
        Exact words in the reference
        <textarea
          aria-label="Reference transcript"
          rows={4}
          value={value.referenceTranscript || ""}
          onChange={(e) =>
            change({
              ...value,
              referenceTranscript: e.target.value,
              referenceVerified: false,
              referenceHash: "",
            })
          }
        />
      </label>
      <button
        className="button secondary"
        disabled={busy || !value.reference}
        onClick={async () => {
          setBusy(true);
          setError("");
          try {
            const text = await act("draftReference", value.reference);
            change({
              ...value,
              referenceTranscript: text,
              referenceVerified: false,
              referenceHash: "",
            });
          } catch (e) {
            setError(e.message);
          } finally {
            setBusy(false);
          }
        }}
      >
        {busy ? <LoaderCircle className="spin" size={16} /> : null}
        {busy ? "Transcribing locally…" : "Draft transcript with local ASR"}
      </button>
      <p className="small muted">The bundled voice is ready to use. For a new reference, a transcript is prepared automatically when you save; you can edit it here.</p>
      <label>
        Delivery goal
        <input
          aria-label="Delivery goal"
          value={value.delivery || "Neutral"}
          onChange={(e) => change({ ...value, delivery: e.target.value })}
          placeholder="For example: calm, formal, reassuring"
        />
      </label>
      <p className="small muted">
        Qwen Base follows the reference. This label helps compare and rate
        takes; it is not an emotion instruction. Choose a reference with the
        delivery you want.
      </p>
      {error && <p className="field-error">{error}</p>}
    </section>
  );
}

export function LexiconSettings({ value, change }) {
  const rows = value.lexicon || [];
  const edit = (i, key, text) =>
    change({
      ...value,
      lexicon: rows.map((r, n) => (n === i ? { ...r, [key]: text } : r)),
    });
  return (
    <section className="surface settings-section">
      <h2>Pronunciation rules</h2>
      <p className="muted">
        Replace complete words or phrases before synthesis. Use spoken
        spellings, such as API → A P I. The review screen shows the resulting
        text. Replacements do not cascade.
      </p>
      <div className="lexicon-list">
        {rows.map((r, i) => (
          <div className="lexicon-row" key={i}>
            <label>
              Written form
              <input
                aria-label={`Rule ${i + 1} word`}
                value={r.word}
                onChange={(e) => edit(i, "word", e.target.value)}
              />
            </label>
            <label>
              Say as
              <input
                aria-label={`Rule ${i + 1} pronunciation`}
                value={r.sayAs}
                onChange={(e) => edit(i, "sayAs", e.target.value)}
              />
            </label>
            <button
              className="icon-btn"
              aria-label={`Delete rule ${i + 1}`}
              onClick={() =>
                change({ ...value, lexicon: rows.filter((_, n) => n !== i) })
              }
            >
              <Trash2 size={17} />
            </button>
          </div>
        ))}
      </div>
      <button
        className="button secondary"
        disabled={rows.length >= 100}
        onClick={() =>
          change({ ...value, lexicon: [...rows, { word: "", sayAs: "" }] })
        }
      >
        <Plus size={16} /> Add pronunciation rule
      </button>
      <h3>Local word verification</h3>
      <p className="muted">
        Every exported file is transcribed with Whisper small.en and compared to
        its spoken text. Any word difference triggers a retry; unresolved
        differences are kept for review.
      </p>
      <label>
        Automatic retries
        <select
          aria-label="Automatic retries"
          value={value.maxRetries ?? 1}
          onChange={(e) =>
            change({ ...value, maxRetries: Number(e.target.value) })
          }
        >
          {[0, 1, 2, 3].map((v) => (
            <option key={v} value={v}>
              {v} {v === 1 ? "retry" : "retries"}
              {v === 0 ? " · flag only" : ""}
            </option>
          ))}
        </select>
      </label>
    </section>
  );
}
