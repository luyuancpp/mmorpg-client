using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using Google.Protobuf;
using UnityEngine;

namespace MmorpgClient.Net
{
    /// <summary>
    /// Persistent TCP connection to the Gate node, framed with MuduoCodec.
    /// All protobuf messages are queued and flushed by a writer thread; the
    /// reader thread parses inbound frames into the inbox queue, which the
    /// Unity main thread drains via <see cref="Poll"/> each frame.
    /// </summary>
    public sealed class GateTcpClient : IDisposable
    {
        private readonly MuduoCodec _codec;
        private TcpClient _tcp;
        private NetworkStream _stream;
        private Thread _readerThread;
        private Thread _writerThread;
        private readonly BlockingCollection<byte[]> _outbox = new(new ConcurrentQueue<byte[]>());
        private readonly ConcurrentQueue<IMessage> _inbox = new();
        private readonly ConcurrentQueue<string> _errors = new();
        private volatile bool _running;

        public event Action<IMessage> OnMessage;
        public event Action<string> OnError;
        public event Action OnDisconnected;

        public bool Connected => _tcp != null && _tcp.Connected;

        public GateTcpClient(MuduoCodec codec) { _codec = codec; }

        public void Connect(string host, int port)
        {
            _tcp = new TcpClient { NoDelay = true };
            _tcp.Connect(host, port);
            _stream = _tcp.GetStream();
            _running = true;

            _readerThread = new Thread(ReaderLoop) { IsBackground = true, Name = "GateTcpReader" };
            _readerThread.Start();
            _writerThread = new Thread(WriterLoop) { IsBackground = true, Name = "GateTcpWriter" };
            _writerThread.Start();
        }

        public void Send(IMessage message)
        {
            if (!_running) throw new InvalidOperationException("not connected");
            _outbox.Add(_codec.Encode(message));
        }

        /// <summary>Drain queued events on the Unity main thread.</summary>
        public void Poll()
        {
            while (_errors.TryDequeue(out var err)) OnError?.Invoke(err);
            while (_inbox.TryDequeue(out var msg)) OnMessage?.Invoke(msg);
        }

        private void ReaderLoop()
        {
            byte[] buf = new byte[64 * 1024];
            int filled = 0;
            try
            {
                while (_running)
                {
                    if (filled == buf.Length)
                    {
                        var bigger = new byte[buf.Length * 2];
                        Buffer.BlockCopy(buf, 0, bigger, 0, filled);
                        buf = bigger;
                    }
                    int n = _stream.Read(buf, filled, buf.Length - filled);
                    if (n <= 0) break;
                    filled += n;

                    int offset = 0;
                    while (true)
                    {
                        IMessage msg;
                        int consumed;
                        try
                        {
                            msg = _codec.TryDecode(buf, offset, filled - offset, out consumed);
                        }
                        catch (Exception ex)
                        {
                            _errors.Enqueue($"decode error: {ex.Message}");
                            _running = false;
                            break;
                        }
                        if (consumed == 0) break;
                        offset += consumed;
                        if (msg != null) _inbox.Enqueue(msg);
                    }
                    if (offset > 0)
                    {
                        Buffer.BlockCopy(buf, offset, buf, 0, filled - offset);
                        filled -= offset;
                    }
                }
            }
            catch (Exception ex) when (_running)
            {
                _errors.Enqueue($"reader: {ex.Message}");
            }
            _running = false;
            try { _tcp?.Close(); } catch { }
            _inbox.Enqueue(new DisconnectedSentinel());
        }

        private void WriterLoop()
        {
            try
            {
                foreach (var bytes in _outbox.GetConsumingEnumerable())
                {
                    if (!_running) break;
                    _stream.Write(bytes, 0, bytes.Length);
                }
            }
            catch (Exception ex) when (_running)
            {
                _errors.Enqueue($"writer: {ex.Message}");
                _running = false;
            }
        }

        public void Dispose()
        {
            _running = false;
            try { _outbox.CompleteAdding(); } catch { }
            try { _stream?.Close(); } catch { }
            try { _tcp?.Close(); } catch { }
        }

        /// <summary>
        /// Internal pseudo-message used to signal disconnect on the inbox
        /// queue. The dispatcher in <see cref="GameClient"/> watches for it
        /// and raises <see cref="OnDisconnected"/>.
        /// </summary>
        public sealed class DisconnectedSentinel : IMessage
        {
            public Google.Protobuf.Reflection.MessageDescriptor Descriptor => null;
            public int CalculateSize() => 0;
            public void MergeFrom(CodedInputStream input) { }
            public void WriteTo(CodedOutputStream output) { }
        }
    }
}
