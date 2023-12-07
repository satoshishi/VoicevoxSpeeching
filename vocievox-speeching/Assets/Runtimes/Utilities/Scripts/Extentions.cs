namespace Speech
{
    using System;
    using System.IO;
    using System.Text;
    using Cysharp.Threading.Tasks;
    using UnityEngine;

    public static class Extentions
    {
        private static readonly int BUFFER_SIZE = 1024 * 32;
        private static readonly int BIT_PER_SAMPLE = 16;
        private static readonly int AUDIO_FORMAT = 1;

        /// <summary>
        /// VoiceVoxからのレスポンwavバイナリをAudioClipに変換する
        /// </summary>
        /// <param name="stream">レスポンスとして受け取ったwavバイナリ</param>
        /// <returns>AudioClip</returns>
        public static async UniTask<AudioClip> ToAudioClip(this Stream stream)
        {
            if (stream == null)
            {
                return null;
            }

            byte[] ftmChank = new byte[44];

            await stream.ReadAsync(ftmChank, 0, 44);
            int channels = BitConverter.ToInt16(ftmChank, 22);
            int bitPerSample = BitConverter.ToInt16(ftmChank, 34);

            if (channels != 1)
            {
                throw new NotSupportedException("AudioClipUtil supports only single channel.");
            }

            if (bitPerSample != 16)
            {
                throw new NotSupportedException("AudioClipUtil supports only 16-bit quantization.");
            }

            int bytePerSample = bitPerSample / 8;
            int frequency = BitConverter.ToInt32(ftmChank, 24);
            int length = BitConverter.ToInt32(ftmChank, 40);

            AudioClip audioClip = null;
            try
            {
                audioClip = AudioClip.Create("AudioClip", length / bytePerSample, channels, frequency, false);

                byte[] readBuffer = new byte[BUFFER_SIZE];
                float[] samplesBuffer = new float[BUFFER_SIZE / bytePerSample];
                int samplesOffset = 0;
                int readBytes;

                while ((readBytes = await stream.ReadAsync(readBuffer, 0, readBuffer.Length)) > 0)
                {
                    if (readBytes % 2 != 0)
                    {
                        // If an odd number of bytes were read, read an additional 1 byte
                        await stream.ReadAsync(readBuffer, readBytes, 1);
                        readBytes++;
                    }

                    int readSamples = readBytes / bytePerSample;

                    // Supports only 16-bit quantization, and single channel.
                    for (int i = 0; i < readSamples; i++)
                    {
                        short value = BitConverter.ToInt16(readBuffer, i * bytePerSample);
                        samplesBuffer[i] = value / 32768f;
                    }

                    if (readBytes == BUFFER_SIZE)
                    {
                        audioClip.SetData(samplesBuffer, samplesOffset);
                    }
                    else
                    {
                        ArraySegment<float> segment = new ArraySegment<float>(samplesBuffer, 0, readSamples);
                        audioClip.SetData(segment.ToArray(), samplesOffset);
                    }

                    samplesOffset += readSamples;
                }
            }
            catch (Exception e)
            {
                if (audioClip != null)
                {
                    UnityEngine.Object.Destroy(audioClip);
                }

                if (e is not OperationCanceledException)
                {
                    throw new IOException("AudioClipUtil: WAV data decode failed.", e);
                }
            }

            return audioClip;
        }

        /// <summary>
        /// AudioClipからWavに変換する
        /// </summary>
        /// <param name="audioClip"></param>
        /// <returns></returns>
        public static byte[] ToWav(this AudioClip audioClip)
        {
            using var stream = new MemoryStream();

            WriteRiffChunk(audioClip, stream);
            WriteFmtChunk(audioClip, stream);
            WriteDataChunk(audioClip, stream);

            return stream.ToArray();
        }

        private static void WriteRiffChunk(AudioClip audioClip, Stream stream)
        {
            // ChunkID RIFF
            stream.Write(Encoding.ASCII.GetBytes("RIFF"));

            // ChunkSize
            const int headerByteSize = 44;
            var chunkSize = BitConverter.GetBytes((UInt32)(headerByteSize + audioClip.samples * audioClip.channels * BIT_PER_SAMPLE / 8));
            stream.Write(chunkSize);

            // Format WAVE
            stream.Write(Encoding.ASCII.GetBytes("WAVE"));
        }

        private static void WriteFmtChunk(AudioClip audioClip, Stream stream)
        {
            // Subchunk1ID fmt
            stream.Write(Encoding.ASCII.GetBytes("fmt "));

            // Subchunk1Size (16 for PCM)
            stream.Write(BitConverter.GetBytes((UInt32)16));

            // AudioFormat (PCM=1)
            stream.Write(BitConverter.GetBytes((UInt16)AUDIO_FORMAT));

            // NumChannels (Mono = 1, Stereo = 2, etc.)
            stream.Write(BitConverter.GetBytes((UInt16)audioClip.channels));

            // SampleRate (audioClip.sampleではなくaudioClip.frequencyのはず)
            stream.Write(BitConverter.GetBytes((UInt32)audioClip.frequency));

            // ByteRate (=SampleRate * NumChannels * BitsPerSample/8)
            stream.Write(BitConverter.GetBytes((UInt32)(audioClip.samples * audioClip.channels * BIT_PER_SAMPLE / 8)));

            // BlockAlign (=NumChannels * BitsPerSample/8)
            stream.Write(BitConverter.GetBytes((UInt16)(audioClip.channels * BIT_PER_SAMPLE / 8)));

            // BitsPerSample
            stream.Write(BitConverter.GetBytes((UInt16)BIT_PER_SAMPLE));
        }

        private static void WriteDataChunk(AudioClip audioClip, Stream stream)
        {
            // Subchunk2ID data
            stream.Write(Encoding.ASCII.GetBytes("data"));

            // Subchuk2Size
            stream.Write(BitConverter.GetBytes((UInt32)(audioClip.samples * audioClip.channels * BIT_PER_SAMPLE / 8)));

            // Data
            var floatData = new float[audioClip.samples * audioClip.channels];
            audioClip.GetData(floatData, 0);

            switch (BIT_PER_SAMPLE)
            {
                case 8:
                    foreach (var f in floatData)stream.Write(BitConverter.GetBytes((sbyte)(f * sbyte.MaxValue)));
                    break;
                case 16:
                    foreach (var f in floatData) stream.Write(BitConverter.GetBytes((short)(f * short.MaxValue)));
                    break;
                case 32:
                    foreach (var f in floatData) stream.Write(BitConverter.GetBytes((int)(f * int.MaxValue)));
                    break;
                case 64:
                    foreach (var f in floatData) stream.Write(BitConverter.GetBytes((float)(f * float.MaxValue)));
                    break;
                default:
                    throw new NotSupportedException(nameof(BIT_PER_SAMPLE));
            }
        }
    }
}
