package com.ultimatevoicegenerator;

import org.junit.Test;
import org.junit.Rule;
import org.junit.rules.TemporaryFolder;
import java.io.*;
import java.nio.file.Files;
import java.security.MessageDigest;
import java.util.HexFormat;
import java.util.concurrent.atomic.AtomicLong;
import static org.junit.Assert.*;

public class ModelFileInstallerTest {
 @Rule public TemporaryFolder folder = new TemporaryFolder();
 private String hash(byte[] bytes) throws Exception { return HexFormat.of().formatHex(MessageDigest.getInstance("SHA-256").digest(bytes)); }

 @Test public void streamsAndVerifiesBeforeMarkingReady() throws Exception {
  byte[] bytes = new byte[2_500_000];
  new java.util.Random(42).nextBytes(bytes);
  File destination = new File(folder.getRoot(), "model.gguf");
  AtomicLong progress = new AtomicLong();
  ModelFileInstaller.install(new ByteArrayInputStream(bytes), destination, bytes.length, hash(bytes), progress::set);
  assertArrayEquals(bytes, Files.readAllBytes(destination.toPath()));
  assertEquals(bytes.length, progress.get());
  assertEquals(hash(bytes), new String(Files.readAllBytes(new File(folder.getRoot(), "model.gguf.verified").toPath()), java.nio.charset.StandardCharsets.US_ASCII));
  assertFalse(new File(folder.getRoot(), "model.gguf.partial").exists());
 }

 @Test public void corruptionPreservesExistingModelAndCleansScratch() throws Exception {
  File destination = folder.newFile("model.gguf");
  Files.write(destination.toPath(), "previous model".getBytes(java.nio.charset.StandardCharsets.UTF_8));
  byte[] corrupt = "corrupt".getBytes();
  assertThrows(IOException.class, () -> ModelFileInstaller.install(new ByteArrayInputStream(corrupt), destination, corrupt.length, hash("correct".getBytes()), ignored -> {}));
  assertEquals("previous model", new String(Files.readAllBytes(destination.toPath()), java.nio.charset.StandardCharsets.UTF_8));
  assertFalse(new File(folder.getRoot(), "model.gguf.verified").exists());
  assertFalse(new File(folder.getRoot(), "model.gguf.partial").exists());
 }

 @Test public void interruptedCopyLeavesNoReadyMarker() throws Exception {
  File destination = new File(folder.getRoot(), "model.gguf");
  InputStream broken = new InputStream() { public int read() throws IOException { throw new IOException("interrupted"); } };
  assertThrows(IOException.class, () -> ModelFileInstaller.install(broken, destination, 100, "unused", ignored -> {}));
  assertFalse(destination.exists());
  assertFalse(new File(folder.getRoot(), "model.gguf.verified").exists());
  assertFalse(new File(folder.getRoot(), "model.gguf.partial").exists());
 }
}
