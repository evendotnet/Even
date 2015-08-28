using System;
using System.Diagnostics.Contracts;
using System.Security.Cryptography;

namespace Even
{
    public class ProjectionStreamState
    {
        public ProjectionStreamState()
            : this(1, new byte[HashSize])
        { }

        public ProjectionStreamState(int initialSequence, byte[] initialHash)
        {
            Contract.Requires(initialSequence > 0);
            Contract.Requires(initialHash != null && initialHash.Length == HashSize);

            _sequence = initialSequence;
            _sequenceHash = initialHash;
        }

        private static HashAlgorithm _ha = Murmur.MurmurHash.Create32();
        private static int _bufferSize = 16 + HashSize;
        public static int HashSize { get; } = _ha.HashSize / 8;

        private int _sequence;
        private byte[] _sequenceHash;
        private long _lastSeenCheckpoint;

        public int Sequence => _sequence;
        public int SequenceHash => BitConverter.ToInt32(_sequenceHash, 0);
        public string SequenceHashString => BitConverter.ToString(_sequenceHash).Replace("-", "").ToLowerInvariant();
        public long LastSeenCheckpoint => _lastSeenCheckpoint;

        /// <summary>
        /// Incrementa a sequencia e calcula um novo hash para a sequencia.
        /// </summary>
        public void AppendCheckpoint(long checkpoint)
        {
            var buffer = new byte[_bufferSize];

            // hash input = checkpoint + previous sequence + previous hash
            Buffer.BlockCopy(BitConverter.GetBytes(checkpoint), 0, buffer, 0, 8);
            Buffer.BlockCopy(BitConverter.GetBytes(_sequence), 0, buffer, 8, 8);
            Buffer.BlockCopy(_sequenceHash, 0, buffer, 16, HashSize);

            _sequence++;
            _sequenceHash = _ha.ComputeHash(buffer);
            _lastSeenCheckpoint = checkpoint;
        }
    }
}
