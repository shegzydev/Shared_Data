#if !UNITY_WEBGL
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;

internal class GigNetVoice : MonoBehaviour
{
    [Header("Recording Settings")]
    public int recordingLength = 10;
    public int sampleRate = 48000;
    public bool startRecordingOnStart = true;

    [Header("Noise")]
    [SerializeField][Range(0.01f, 0.2f)] float lowPassAlpha = 0.05f;

    [Header("Latency")]
    [Tooltip("in milliseconds")]
    public int latencyBuffer = 5000;

    public bool loopBack = false;

    private string microphoneName;
    private AudioClip recordedClip;
    private bool isRecording = false;
    private float[] rawAudioData;

    private int lastMicPosition = 0;
    private int micPosition = 0;

    // Events for external manipulation
    public Action<byte[]> OnEncoded;

    int frameSize = 960;

    ConcurrentQueue<float> micQueue = new();
    ConcurrentQueue<byte[]>[] encodedData;
    static ConcurrentQueue<float>[] decodedData;

    Thread encodingThread;
    Thread decodingThread;

    private volatile bool running;

    public static GigNetVoice Instance
    {
        get
        {
            if (instance == null)
            {
#pragma warning disable CS0618 // Type or member is obsolete
                instance = FindObjectOfType<GigNetVoice>();
#pragma warning restore CS0618 // Type or member is obsolete

            }
            return instance;
        }
    }
    static GigNetVoice instance;

    RemotePlayer[] remotePlayers;
    public int maxPlayers = 3;

    void Awake()
    {
        instance = this;
        OpusCodec.Init();

        encodedData = new ConcurrentQueue<byte[]>[maxPlayers];
        decodedData = new ConcurrentQueue<float>[maxPlayers];

        for (int i = 0; i < maxPlayers; i++)
        {
            encodedData[i] = new();
            decodedData[i] = new();
        }

        running = true;
    }

    void Start()
    {
        if (Microphone.devices.Length > 0)
        {
            microphoneName = Microphone.devices[0];
            Debug.Log("Using microphone: " + microphoneName);

            if (startRecordingOnStart)
            {
                StartRecording();
            }
        }
        else
        {
            Debug.LogError("No microphone found!");
        }

        decodingThread = new Thread(ProcessDecode) { IsBackground = true };
        decodingThread.Start();

        remotePlayers = new RemotePlayer[maxPlayers];
        for (int i = 0; i < maxPlayers; i++)
        {
            remotePlayers[i] = new RemotePlayer(i, gameObject, recordedClip);
        }
    }

    void Update()
    {
        micPosition = Microphone.GetPosition(microphoneName);

        if (micPosition != lastMicPosition)
        {
            int samplesAvailable = micPosition - lastMicPosition;
            if (samplesAvailable < 0) samplesAvailable += recordedClip.samples; // wrap around

            rawAudioData = new float[samplesAvailable];
            recordedClip.GetData(rawAudioData, lastMicPosition);

            rawAudioData = ProcessAudio(rawAudioData);

            for (int i = 0; i < rawAudioData.Length; i++)
            {
                micQueue.Enqueue(rawAudioData[i]);
            }

            lastMicPosition = micPosition;
        }
    }

    float[] ProcessAudio(float[] data)
    {
        var lowPass = LowPassDenoise(data, lowPassAlpha);
        var bandBass = BandPassFilter(lowPass);
        return bandBass;
    }

    float[] LowPassDenoise(float[] input, float alpha = 0.1f)
    {
        float[] output = new float[input.Length];
        output[0] = input[0];

        for (int i = 1; i < input.Length; i++)
            output[i] = output[i - 1] + alpha * (input[i] - output[i - 1]);

        return output;
    }

    float[] BandPassFilter(float[] input, int sampleRate = 48000, float lowCut = 300f, float highCut = 3400f)
    {
        float[] output = new float[input.Length];

        // High-pass at lowCut
        float rcHigh = 1f / (2f * Mathf.PI * lowCut);
        float dt = 1f / sampleRate;
        float alphaHigh = rcHigh / (rcHigh + dt);

        float prevIn = input[0];
        float prevOut = input[0];
        for (int i = 1; i < input.Length; i++)
        {
            float hp = alphaHigh * (prevOut + input[i] - prevIn);
            prevIn = input[i];
            prevOut = hp;
            output[i] = hp;
        }

        // Low-pass at highCut
        float rcLow = 1f / (2f * Mathf.PI * highCut);
        float alphaLow = dt / (rcLow + dt);

        float prev = output[0];
        for (int i = 1; i < input.Length; i++)
        {
            output[i] = prev + alphaLow * (output[i] - prev);
            prev = output[i];
        }

        return output;
    }

    public void StartRecording()
    {
        if (string.IsNullOrEmpty(microphoneName))
        {
            Debug.LogError("No microphone available!");
            return;
        }

        if (isRecording)
        {
            StopRecording();
        }

        Debug.Log("Starting recording...");
        recordedClip = Microphone.Start(microphoneName, true, recordingLength, sampleRate);
        isRecording = true;
        lastMicPosition = 0;

        encodingThread = new Thread(ProcessEncode) { IsBackground = true };
        encodingThread.Start();
    }

    void ProcessEncode()
    {
        while (running)
        {
            while (micQueue.Count >= frameSize)
            {
                float[] chunk = new float[frameSize];
                byte[] encoded = new byte[4000];

                for (int i = 0; i < frameSize; i++)
                {
                    if (micQueue.TryDequeue(out float data))
                    {
                        chunk[i] = data;
                    }
                    else
                    {
                        chunk[i] = 0;
                    }
                }

                int encodedLen = OpusCodec.Encode(chunk, frameSize, encoded);
                Array.Resize(ref encoded, encodedLen);

                if (loopBack) OnReceiveEncoded(encoded, 0);
                OnEncoded?.Invoke(encoded);
            }
        }
    }

    public void OnReceiveEncoded(byte[] data, int sender)
    {
        encodedData[sender].Enqueue(data);
    }

    void ProcessDecode()
    {
        while (running)
        {
            for (int i = 0; i < maxPlayers; i++)
            {
                if (encodedData[i].Count >= (latencyBuffer / 20))
                {
                    while (encodedData[i].TryDequeue(out byte[] chunk))
                    {
                        float[] decoded = new float[frameSize];

                        int decodedSamples = OpusCodec.Decode(chunk, chunk.Length, frameSize, decoded);

                        for (int j = 0; j < decoded.Length; j++)
                        {
                            decodedData[i].Enqueue(decoded[j]);
                        }
                    }
                }
            }
        }
    }

    public void StopRecording()
    {
        if (isRecording)
        {
            Debug.Log("Stopping recording...");
            Microphone.End(microphoneName);
            isRecording = false;
        }
    }

    void OnApplicationQuit()
    {
        running = false;
        encodingThread?.Join(100);
        decodingThread?.Join(100);
        OpusCodec.Destroy();
    }

    void OnDestroy()
    {
        if (isRecording)
        {
            StopRecording();
        }
        OpusCodec.Destroy();
    }

    class RemotePlayer
    {
        AudioSource audioSource;
        AudioClip playbackClip;
        int id;

        public RemotePlayer(int _id, GameObject root, AudioClip recordedClip)
        {
            id = _id;
            audioSource = root.AddComponent<AudioSource>();

            playbackClip = AudioClip.Create($"FeedBackAudio_{id}", recordedClip.samples, recordedClip.channels,
                recordedClip.frequency, true, OnAudioRead);

            bool wasPlaying = audioSource.isPlaying;
            float currentTime = audioSource.time;

            audioSource.clip = playbackClip;
            audioSource.loop = true;

            audioSource.Play();
        }

        void OnAudioRead(float[] data)
        {
            for (int head = 0; head < data.Length; head++)
            {
                if (decodedData[id].TryDequeue(out float audioData))
                {
                    data[head] = audioData;
                }
                else
                {
                    data[head] = 0;
                }
            }
        }
    }
}
#endif