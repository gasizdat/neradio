package com.neradio.enginetest;

import com.un4seen.bass.BASS;

// Opens a BASS URL stream on a plain Java thread, one the .NET runtime never suspends for a GC.
// Returns the BASS error code in the high 32 bits and the handle in the low 32 bits, since BASS
// keeps the error code per thread.
public final class Opener {
    public static long open(final String url, final int flags) throws InterruptedException {
        final long[] result = new long[1];
        Thread thread = new Thread(new Runnable() {
            public void run() {
                int handle = BASS.BASS_StreamCreateURL(url, 0, flags, null, null);
                int error = BASS.BASS_ErrorGetCode();
                result[0] = ((long) error << 32) | (handle & 0xffffffffL);
            }
        });
        thread.start();
        thread.join();
        return result[0];
    }
}
