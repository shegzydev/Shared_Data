using System;
using System.Runtime.InteropServices;

internal static class OpusCodec
{
    private const int SampleRate = 48000;
    private const int Channels = 1; // mono (use 2 for stereo)
    private const int Application = 2049; // OPUS_APPLICATION_AUDIO

    const string lib = "libopus";
    // ===== Opus Native =====
    [DllImport(lib, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr opus_encoder_create(int Fs, int channels, int application, out int error);

    [DllImport(lib, CallingConvention = CallingConvention.Cdecl)]
    private static extern void opus_encoder_destroy(IntPtr encoder);

    [DllImport(lib, CallingConvention = CallingConvention.Cdecl)]
    private static extern int opus_encode(IntPtr st, short[] pcm, int frame_size,
        byte[] data, int max_data_bytes);

    [DllImport(lib, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr opus_decoder_create(int Fs, int channels, out int error);

    [DllImport(lib, CallingConvention = CallingConvention.Cdecl)]
    private static extern void opus_decoder_destroy(IntPtr decoder);

    [DllImport(lib, CallingConvention = CallingConvention.Cdecl)]
    private static extern int opus_decode(IntPtr st, byte[] data, int len,
        short[] pcm, int frame_size, int decode_fec);

    private static IntPtr encoder;
    private static IntPtr decoder;

    public static void Init()
    {
        int err;
        encoder = opus_encoder_create(SampleRate, Channels, Application, out err);
        if (err != 0 || encoder == IntPtr.Zero)
            throw new Exception("Failed to create Opus encoder, error: " + err);

        decoder = opus_decoder_create(SampleRate, Channels, out err);
        if (err != 0 || decoder == IntPtr.Zero)
            throw new Exception("Failed to create Opus decoder, error: " + err);
    }

    public static void Destroy()
    {
        if (encoder != IntPtr.Zero)
        {
            opus_encoder_destroy(encoder);
            encoder = IntPtr.Zero;
        }
        if (decoder != IntPtr.Zero)
        {
            opus_decoder_destroy(decoder);
            decoder = IntPtr.Zero;
        }
    }

    /// <summary>
    /// Encode float PCM (-1..1) into Opus compressed bytes
    /// </summary>
    public static int Encode(float[] pcmFloat, int frameSize, byte[] output)
    {
        if (encoder == IntPtr.Zero) throw new Exception("Encoder not initialized");

        // convert float [-1,1] to 16-bit PCM
        short[] pcmShort = new short[pcmFloat.Length];
        for (int i = 0; i < pcmFloat.Length; i++)
        {
            float f = pcmFloat[i];
            f = Math.Max(-1f, Math.Min(1f, f));
            pcmShort[i] = (short)(f * short.MaxValue);
        }

        return opus_encode(encoder, pcmShort, frameSize, output, output.Length);
    }

    /// <summary>
    /// Decode Opus compressed bytes into float PCM (-1..1)
    /// </summary>
    public static int Decode(byte[] input, int length, int frameSize, float[] pcmOut)
    {
        if (decoder == IntPtr.Zero) throw new Exception("Decoder not initialized");

        short[] pcmShort = new short[frameSize * Channels];
        int samples = opus_decode(decoder, input, length, pcmShort, frameSize, 0);

        for (int i = 0; i < samples * Channels; i++)
            pcmOut[i] = pcmShort[i] / 32768f;

        return samples; // number of samples per channel
    }
}
