using System;
using System.Diagnostics.Contracts;
using System.Security.Cryptography;

namespace Even
{
    public class ProjectionStreamState
    {
        public ProjectionStreamState()
            : this(0, 0, 0)
        { }

        public ProjectionStreamState(long checkpoint, int sequence, int sequenceHash)
        {
            _checkpoint = checkpoint;
            _sequence = sequence;
            _hashBytes = BitConverter.GetBytes(sequenceHash);
        }

        private static HashAlgorithm _ha = Murmur.MurmurHash.Create32();
        private static int _bufferSize = 16 + HashSize;
        public static int HashSize { get; } = _ha.HashSize / 8;

        private int _sequence;
        private byte[] _hashBytes;
        private long _checkpoint;

        public int Sequence => _sequence;
        public int SequenceHash => BitConverter.ToInt32(_hashBytes, 0);
        public long Checkpoint => _checkpoint;

        /// <summary>
        /// Increments the projection stream sequence with the provided checkpoint recalculates the hash.
        /// </summary>
        public void AppendCheckpoint(long checkpoint)
        {
            var buffer = new byte[_bufferSize];

            // hash input = new checkpoint + previous sequence + previous hash
            Buffer.BlockCopy(BitConverter.GetBytes(checkpoint), 0, buffer, 0, 8);
            Buffer.BlockCopy(BitConverter.GetBytes(_sequence), 0, buffer, 8, 8);
            Buffer.BlockCopy(_hashBytes, 0, buffer, 16, HashSize);

            _sequence++;
            _hashBytes = _ha.ComputeHash(buffer);
            _checkpoint = checkpoint;

        }
    }
}
