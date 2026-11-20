using System;
using System.Runtime.InteropServices;
using System.Threading;
using PomodoroFocus.Core.Audio;

namespace PomodoroFocus.App.Interop
{
    public sealed class WaveOutPlayer : IDisposable
    {
        private const int SampleRate = AmbientMixer.SampleRate;
        private const int Channels = 2;
        private const int BitsPerSample = 16;
        private const int FramesPerBuffer = 2205; // 50ms
        private const int BufferCount = 4;
        private const int BytesPerBuffer = FramesPerBuffer * Channels * (BitsPerSample / 8);

        private const uint CALLBACK_EVENT = 0x00050000u;
        private const uint WHDR_DONE = 0x00000001u;
        private const int WAVE_MAPPER = -1;

        [StructLayout(LayoutKind.Sequential)]
        private struct WAVEFORMATEX
        {
            public ushort wFormatTag;
            public ushort nChannels;
            public uint nSamplesPerSec;
            public uint nAvgBytesPerSec;
            public ushort nBlockAlign;
            public ushort wBitsPerSample;
            public ushort cbSize;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WAVEHDR
        {
            public IntPtr lpData;
            public uint dwBufferLength;
            public uint dwBytesRecorded;
            public IntPtr dwUser;
            public uint dwFlags;
            public uint dwLoops;
            public IntPtr lpNext;
            public IntPtr reserved;
        }

        [DllImport("winmm.dll")]
        private static extern int waveOutGetNumDevs();

        [DllImport("winmm.dll")]
        private static extern int waveOutOpen(
            out IntPtr hWaveOut,
            int uDeviceID,
            ref WAVEFORMATEX lpFormat,
            IntPtr dwCallback,
            IntPtr dwInstance,
            uint fdwOpen);

        [DllImport("winmm.dll")]
        private static extern int waveOutPrepareHeader(IntPtr hWaveOut, IntPtr lpWaveOutHdr, int uSize);

        [DllImport("winmm.dll")]
        private static extern int waveOutUnprepareHeader(IntPtr hWaveOut, IntPtr lpWaveOutHdr, int uSize);

        [DllImport("winmm.dll")]
        private static extern int waveOutWrite(IntPtr hWaveOut, IntPtr lpWaveOutHdr, int uSize);

        [DllImport("winmm.dll")]
        private static extern int waveOutReset(IntPtr hWaveOut);

        [DllImport("winmm.dll")]
        private static extern int waveOutClose(IntPtr hWaveOut);

        private readonly AmbientMixer _mixer;
        private readonly bool _noSoundMode;
        private readonly AutoResetEvent _wakeEvent = new AutoResetEvent(false);
        private readonly Thread _thread;
        private volatile bool _stopRequested;
        private bool _disposed;

        public bool IsAudioDeviceAvailable { get; private set; }
        public string DeviceErrorMessage { get; private set; }

        public WaveOutPlayer(AmbientMixer mixer, bool noSoundMode)
        {
            _mixer = mixer;
            _noSoundMode = noSoundMode;

            if (_noSoundMode)
            {
                IsAudioDeviceAvailable = true;
                return;
            }

            try
            {
                int devs = waveOutGetNumDevs();
                IsAudioDeviceAvailable = devs > 0;
                if (!IsAudioDeviceAvailable)
                {
                    DeviceErrorMessage = "未检测到可用的音频输出设备";
                }
            }
            catch
            {
                IsAudioDeviceAvailable = false;
                DeviceErrorMessage = "未检测到可用的音频输出设备";
            }

            _thread = new Thread(AudioLoop);
            _thread.IsBackground = true;
            _thread.Priority = ThreadPriority.AboveNormal;
            _thread.Name = "PomodoroFocus.WaveOut";
            _thread.Start();
        }

        public void Wake()
        {
            if (!_disposed && !_noSoundMode)
            {
                _wakeEvent.Set();
            }
        }

        private void AudioLoop()
        {
            var floatBuf = new float[FramesPerBuffer * Channels];
            var pcmBuf = new short[FramesPerBuffer * Channels];
            var byteBuf = new byte[BytesPerBuffer];

            while (!_stopRequested)
            {
                if (!_mixer.HasActiveAudio())
                {
                    _wakeEvent.WaitOne(200);
                    continue;
                }

                if (!IsAudioDeviceAvailable)
                {
                    _wakeEvent.WaitOne(500);
                    continue;
                }

                RunDeviceSession(floatBuf, pcmBuf, byteBuf);
            }
        }

        private void RunDeviceSession(float[] floatBuf, short[] pcmBuf, byte[] byteBuf)
        {
            AutoResetEvent bufferEvent = new AutoResetEvent(false);
            IntPtr hWaveOut = IntPtr.Zero;
            var hdrPtrs = new IntPtr[BufferCount];
            var dataPtrs = new IntPtr[BufferCount];
            int hdrSize = Marshal.SizeOf(typeof(WAVEHDR));

            try
            {
                var fmt = new WAVEFORMATEX
                {
                    wFormatTag = 1, // WAVE_FORMAT_PCM
                    nChannels = Channels,
                    nSamplesPerSec = SampleRate,
                    wBitsPerSample = BitsPerSample,
                    nBlockAlign = (ushort)(Channels * (BitsPerSample / 8)),
                    nAvgBytesPerSec = (uint)(SampleRate * Channels * (BitsPerSample / 8)),
                    cbSize = 0
                };

                int openRes = waveOutOpen(
                    out hWaveOut,
                    WAVE_MAPPER,
                    ref fmt,
                    bufferEvent.SafeWaitHandle.DangerousGetHandle(),
                    IntPtr.Zero,
                    CALLBACK_EVENT);

                if (openRes != 0 || hWaveOut == IntPtr.Zero)
                {
                    IsAudioDeviceAvailable = false;
                    DeviceErrorMessage = "未检测到可用的音频输出设备";
                    return;
                }

                for (int i = 0; i < BufferCount; i++)
                {
                    dataPtrs[i] = Marshal.AllocHGlobal(BytesPerBuffer);
                    hdrPtrs[i] = Marshal.AllocHGlobal(hdrSize);
                    var hdr = new WAVEHDR
                    {
                        lpData = dataPtrs[i],
                        dwBufferLength = BytesPerBuffer,
                        dwFlags = 0
                    };
                    Marshal.StructureToPtr(hdr, hdrPtrs[i], false);
                    waveOutPrepareHeader(hWaveOut, hdrPtrs[i], hdrSize);
                    FillAndQueue(hWaveOut, hdrPtrs[i], dataPtrs[i], hdrSize, floatBuf, pcmBuf, byteBuf);
                }

                int idleBuffers = 0;
                const int idleCloseThreshold = 60; // 60 * 50ms = 3.0s idle -> close device

                while (!_stopRequested)
                {
                    bufferEvent.WaitOne(80);
                    if (_stopRequested) break;

                    bool active = _mixer.HasActiveAudio();
                    if (!active)
                    {
                        idleBuffers++;
                        if (idleBuffers >= idleCloseThreshold)
                        {
                            break;
                        }
                    }
                    else
                    {
                        idleBuffers = 0;
                    }

                    for (int i = 0; i < BufferCount; i++)
                    {
                        WAVEHDR hdr = (WAVEHDR)Marshal.PtrToStructure(hdrPtrs[i], typeof(WAVEHDR));
                        if ((hdr.dwFlags & WHDR_DONE) != 0)
                        {
                            FillAndQueue(hWaveOut, hdrPtrs[i], dataPtrs[i], hdrSize, floatBuf, pcmBuf, byteBuf);
                        }
                    }
                }
            }
            catch
            {
                // Gracefully close session
            }
            finally
            {
                if (hWaveOut != IntPtr.Zero)
                {
                    waveOutReset(hWaveOut);
                    for (int i = 0; i < BufferCount; i++)
                    {
                        if (hdrPtrs[i] != IntPtr.Zero)
                        {
                            waveOutUnprepareHeader(hWaveOut, hdrPtrs[i], hdrSize);
                        }
                    }
                    waveOutClose(hWaveOut);
                }
                for (int i = 0; i < BufferCount; i++)
                {
                    if (hdrPtrs[i] != IntPtr.Zero) Marshal.FreeHGlobal(hdrPtrs[i]);
                    if (dataPtrs[i] != IntPtr.Zero) Marshal.FreeHGlobal(dataPtrs[i]);
                }
                bufferEvent.Dispose();
            }
        }

        private void FillAndQueue(
            IntPtr hWaveOut,
            IntPtr hdrPtr,
            IntPtr dataPtr,
            int hdrSize,
            float[] floatBuf,
            short[] pcmBuf,
            byte[] byteBuf)
        {
            _mixer.Render(floatBuf, FramesPerBuffer);
            for (int i = 0; i < floatBuf.Length; i++)
            {
                float s = floatBuf[i];
                if (s > 1.0f) s = 1.0f;
                else if (s < -1.0f) s = -1.0f;
                pcmBuf[i] = (short)(s * 32767.0f);
            }
            Buffer.BlockCopy(pcmBuf, 0, byteBuf, 0, BytesPerBuffer);
            Marshal.Copy(byteBuf, 0, dataPtr, BytesPerBuffer);
            waveOutWrite(hWaveOut, hdrPtr, hdrSize);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _stopRequested = true;
            _wakeEvent.Set();
            if (_thread != null && _thread.IsAlive)
            {
                _thread.Join(1200);
            }
            _wakeEvent.Dispose();
        }
    }
}
