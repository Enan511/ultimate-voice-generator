import { chromium } from "@playwright/test";
import { createServer } from "node:http";
import fs from "node:fs";
import path from "node:path";
import assert from "node:assert/strict";
import { execFileSync } from "node:child_process";
const root = path.resolve("dist");
// Native speech generation is covered by --verify-app. This disposable tone tests decoding/seeking.
fs.mkdirSync("test-artifacts", { recursive: true });
const sample = path.resolve("test-artifacts/audio-ui.flac");
execFileSync(path.resolve("engines/ffmpeg.exe"), ["-hide_banner", "-loglevel", "error", "-y", "-f", "lavfi", "-i", "sine=frequency=440:duration=2", "-c:a", "flac", sample], { windowsHide: true });
fs.mkdirSync("test-artifacts/screenshots-v3", { recursive: true });
const server = createServer((req, res) => {
  const name = req.url.split("?")[0];
  const file =
    name === "/sample.flac"
      ? sample
      : path.join(root, name === "/" ? "index.html" : decodeURIComponent(name));
  if (!file.startsWith(root) && name !== "/sample.flac") {
    res.writeHead(403).end();
    return;
  }
  if (!fs.existsSync(file)) {
    res.writeHead(404).end();
    return;
  }
  res.setHeader(
    "Content-Type",
    {
      ".js": "text/javascript",
      ".css": "text/css",
      ".html": "text/html",
      ".flac": "audio/flac",
    }[path.extname(file)] || "application/octet-stream",
  );
  fs.createReadStream(file).pipe(res);
});
await new Promise((r) => server.listen(0, "127.0.0.1", r));
const browser = await chromium.launch({
  channel: "msedge",
  headless: true,
  args: ["--autoplay-policy=no-user-gesture-required"],
});
const page = await browser.newPage({ viewport: { width: 1280, height: 950 } });
const errors = [];
page.on("pageerror", (e) => errors.push(e.message));
await page.addInitScript(() => {
  const state = {
    settings: {
      folder: "D:\\Recordings",
      format: "flac",
      reference: "reference.mp3",
      speed: 1,
      tone: "natural",
      normalize: true,
      referenceTranscript: "Good morning.",
      referenceVerified: true,
      referenceHash: "test",
      delivery: "Calm and formal",
      lexicon: [{ word: "API", sayAs: "A P I" }],
      maxRetries: 1,
      seed: 42,
    },
    jobs: [],
    paused: false,
    engine: "Qwen3-TTS 1.7B Base ready",
  };
  window.__requests = [];
  window.__JARVIS_TEST__ = {
    audioUrl: "/sample.flac",
    command: async (name, payload) => {
      window.__requests.push({ name, payload });
      if (name === "enqueue")
        state.jobs.push(
          ...payload.map((p, i) => ({
            id: crypto.randomUUID(),
            text: p.text,
            spokenText: p.text,
            notes: p.notes || "",
            created: new Date().toISOString(),
            status: i ? "pending" : "completed",
            path: i ? null : "D:\\Recordings\\sample.flac",
            options: p.options || structuredClone(state.settings),
            seconds: 15,
            attempts: 1,
            fileRevision: 0,
            verification: {
              passed: true,
              transcript: p.text,
              expected: p.text,
              errors: 0,
              differences: [],
            },
            review: { voice: 0, tone: 0, emotion: 0, notes: "" },
          })),
        );
      if (name === "settings") state.settings = payload;
      if (name === "pickFolder") return "D:\\New output";
      if (name === "pickReference") return "D:\\references\\new.wav";
      if (name === "draftReference")
        return "Good morning. All systems are operational.";
      if (name === "pause") state.paused = payload;
      if (name === "clear")
        state.jobs.forEach((j) => {
          if (j.status !== "pending") j.archived = true;
        });
      if (name === "updateSpeed")
        state.jobs.find((j) => j.id === payload.id).fileRevision++;
      if (name === "deleteRecord")
        state.jobs = state.jobs.filter((j) => j.id !== payload);
      if (name === "listeningReview")
        state.jobs.find((j) => j.id === payload.id).review = payload.review;
      return structuredClone(state);
    },
  };
});
const nav = (name) =>
  page.getByRole("navigation").getByRole("button", { name, exact: true });
try {
  await page.goto(`http://127.0.0.1:${server.address().port}`);
  const text = `Good morning, sir. Don't skip [this]: API!\nSecond line—ready?`;
  await page.getByLabel("Batch prompts").fill(text);
  await page.getByRole("button", { name: "Review 2 prompts" }).click();
  assert.equal(
    await page.getByLabel("Prompt 1", { exact: true }).inputValue(),
    text.split("\n")[0],
  );
  assert.ok(
    await page
      .getByText("Model input: Good morning, sir. Don't skip [this]: A P I!")
      .count(),
  );
  assert.equal(
    await page.evaluate(
      () => window.__requests.filter((x) => x.name === "enqueue").length,
    ),
    0,
  );
  await page.getByLabel("Delete prompt 2").click();
  await page.getByRole("button", { name: "Revert to raw input" }).click();
  assert.equal(await page.getByLabel("Batch prompts").inputValue(), text);
  await page.getByRole("button", { name: "Review 2 prompts" }).click();
  await page
    .getByRole("button", { name: "Confirm & Start Generation" })
    .click();
  assert.equal(await page.getByRole("button", { name: "Delete record & file" }).count(),1);
  assert.equal(await page.locator(".app-logo").getAttribute("src"),"/app.ico");
  const player = page.locator("audio").first();
  await player.evaluate((a) => {
    a.load();
  });
  await page.waitForFunction(() =>
    Number.isFinite(document.querySelector("audio")?.duration),
  );
  assert.equal(await player.evaluate((a) => a.playbackRate), 1);
  await player.evaluate(async (a) => {
    await a.play();
    a.currentTime = 0.3;
    a.pause();
  });
  const slider = page.getByLabel("Preview speed");
  await slider.fill("1.25");
  assert.equal(await player.evaluate((a) => a.playbackRate), 1.25);
  await page.getByRole("button", { name: "Update File", exact: true }).click();
  await page.getByRole("button", { name: "Confirm", exact: true }).click();
  await page.waitForFunction(() =>
    window.__requests.some((x) => x.name === "updateSpeed"),
  );
  await page.waitForTimeout(200);
  assert.equal(await page.getByLabel("Preview speed").inputValue(), "1");
  assert.equal(
    await page
      .locator("audio")
      .first()
      .evaluate((a) => a.playbackRate),
    1,
  );
  await page.getByRole("button", { name: "Regenerate", exact: true }).click();
  await page.getByLabel("Regeneration text").fill("Good evening, sir!");
  await page
    .getByLabel("Generate two takes for a listening comparison")
    .check();
  await page.getByRole("button", { name: "Add two comparison takes" }).click();
  const batch = await page.evaluate(
    () => window.__requests.filter((x) => x.name === "enqueue").at(-1).payload,
  );
  assert.equal(batch.length, 2);
  assert.equal(batch[0].text, "Good evening, sir!");
  assert.notEqual(batch[0].options.seed, batch[1].options.seed);
  await nav("History").click();
  assert.equal(await page.locator("article").count(), 2);
  const first = page.locator("article").first();
  await first
    .locator("summary")
    .filter({ hasText: "Listening review" })
    .click();
  await first.getByLabel("Voice match").selectOption("4");
  await first.getByRole("button", { name: "Save listening review" }).click();
  await first.getByRole("button", { name: "Delete record & file" }).click();
  await page.getByRole("button", { name: "Cancel", exact: true }).click();
  assert.equal(await page.locator("article").count(), 2);
  await first.getByRole("button", { name: "Delete record & file" }).click();
  await page.getByRole("button", { name: "Confirm", exact: true }).click();
  await page.waitForTimeout(200);
  assert.equal(await page.locator("article").count(), 1);
  for (const width of [1280, 760, 420, 390]) {
    await page.setViewportSize({ width, height: 950 });
    for (const view of ["History", "Settings", "Create audio"]) {
      await nav(view).click();
      await page.waitForTimeout(60);
      assert.ok(
        await page.evaluate(
          () => document.documentElement.scrollWidth <= innerWidth + 1,
        ),
        `${view} overflows at ${width}`,
      );
      if (width === 1280 || width === 390)
        await page.screenshot({
          path: `test-artifacts/screenshots-v3/${view.replaceAll(" ", "-")}-${width}.png`,
          fullPage: true,
        });
    }
  }
  await nav("Settings").click();
  await page.getByRole("button", { name: "Choose folder" }).click();
  await page
    .getByRole("button", { name: "Draft transcript with local ASR" })
    .click();
  assert.equal(
    await page.getByLabel("Reference transcript").inputValue(),
    "Good morning. All systems are operational.",
  );
  assert.equal(await page.getByLabel("I listened and confirmed", { exact: false }).count(),0);
  await page.getByRole("button", { name: "Save settings" }).click();
  assert.deepEqual(errors, []);
  fs.writeFileSync(
    "test-artifacts/ui-v3-results.json",
    JSON.stringify(
      {
        passed: true,
        viewports: [1280, 760, 420, 390],
        checks: [
          "punctuation",
          "lexicon preview",
          "staging/revert/delete",
          "confirmation",
          "real FLAC playback",
          "1x default/reset",
          "speed update confirmation",
          "edited regeneration and two takes",
          "history deletion/cancel",
          "listening scores",
          "reference draft without acknowledgement",
          "responsive layouts",
          "no React errors",
        ],
      },
      null,
      2,
    ),
  );
  console.log(
    "PASS: v3 UI, real audio playback, history, speed controls, reference setup and responsive layouts.",
  );
} finally {
  await browser.close();
  server.close();
}


