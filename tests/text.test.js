import { describe, it, expect } from "vitest";
import { sanitize, stageLines, pronounce } from "../src/text.js";
describe("punctuation and pronunciation", () => {
  it("keeps all punctuation and symbols", () => {
    const s = `Don't strip [sir]: ‘hello’ — 2+2=4 / "yes" (now)… 🤖!`;
    expect(sanitize(s)).toBe(s);
  });
  it("normalizes spacing and Unicode only", () =>
    expect(sanitize(" Cafe\u0301\t東京 ١٢٣ 😀 ")).toBe("Café 東京 ١٢٣ 😀"));
  it("splits independent lines and discards blank lines", () =>
    expect(stageLines("First!\r\n\nSecond?\rThird—yes")).toEqual([
      "First!",
      "Second?",
      "Third—yes",
    ]));
  it("preserves dashes", () =>
    expect(stageLines("one\n---\ntwo")).toEqual(["one", "---", "two"]));
  it("replaces words, not substrings, case insensitively", () =>
    expect(
      pronounce("API, apiary, Api!", [{ word: "API", sayAs: "A P I" }]),
    ).toBe("A P I, apiary, A P I!"));
  it("does not cascade rules", () =>
    expect(
      pronounce("a b", [
        { word: "a", sayAs: "b" },
        { word: "b", sayAs: "c" },
      ]),
    ).toBe("b c"));
  it("handles punctuation and longest phrases", () =>
    expect(
      pronounce("Dr. Smith's report", [
        { word: "Dr.", sayAs: "Doctor" },
        { word: "Dr. Smith", sayAs: "Doctor Smyth" },
      ]),
    ).toBe("Doctor Smyth's report"));
});
