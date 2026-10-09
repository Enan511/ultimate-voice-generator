package com.ultimatevoicegenerator;

import java.io.*;
import java.nio.file.*;
import java.security.*;
import java.util.Locale;
import java.util.function.LongConsumer;

/** Streams bundled models to disk without loading model weights into Java memory. */
final class ModelFileInstaller {
 static void install(InputStream source, File destination, long expectedSize,
                     String expectedHash, LongConsumer progress) throws Exception {
  File partial = new File(destination.getParentFile(), destination.getName() + ".partial");
  try {
   MessageDigest digest = MessageDigest.getInstance("SHA-256");
   long copied = 0;
   try (InputStream in = source; OutputStream out = new FileOutputStream(partial)) {
    byte[] buffer = new byte[1024 * 1024];
    int count;
    while ((count = in.read(buffer)) != -1) {
     copied += count;
     if (copied > expectedSize) throw new IOException("Included model is larger than expected");
     out.write(buffer, 0, count);
     digest.update(buffer, 0, count);
     progress.accept(copied);
    }
   }
   StringBuilder actual = new StringBuilder();
   for (byte b : digest.digest()) actual.append(String.format(Locale.ROOT, "%02x", b & 255));
   if (copied != expectedSize || !actual.toString().equals(expectedHash))
    throw new IOException("Included model failed its integrity check. Re-copy and reinstall the APK.");
   Files.move(partial.toPath(), destination.toPath(), StandardCopyOption.REPLACE_EXISTING,
              StandardCopyOption.ATOMIC_MOVE);
   Files.write(new File(destination.getParentFile(), destination.getName() + ".verified").toPath(),
               expectedHash.getBytes(java.nio.charset.StandardCharsets.US_ASCII));
  } finally {
   Files.deleteIfExists(partial.toPath());
  }
 }
}
