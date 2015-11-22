using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace Even
{
    [DebuggerDisplay("{DebuggerText}")]
    public class Stream : IEquatable<Stream>
    {
        const int HashLength = 20;

        /// <summary>
        /// Initializes a Stream from a stream hash.
        /// </summary>
        public Stream(byte[] hash)
        {
            CheckHashArgument(hash);

            _hash = hash;
        }

        /// <summary>
        /// Initializes a Stream from a stream name.
        /// </summary>
        public Stream(string name)
        {
            CheckNameArgument(name);

            _name = name;
        }

        /// <summary>
        /// Initializes a Stream from a hash and name.
        /// </summary>
        /// <remarks>
        /// Because the name is treated as informational, there is no check to see if the hash matches the computed hash of the name. 
        /// There might be cases where a very long name could be truncated to be stored in the store.
        /// </remarks>
        public Stream(byte[] hash, string name)
        {
            CheckHashArgument(hash);
            CheckNameArgument(name);

            _hash = hash;
            _name = name;
        }

        private byte[] _hash;
        private string _name;

        /// <summary>
        /// Returns the hash of the stream.
        /// </summary>
        public byte[] Hash => _hash ?? (_hash = ComputeHash(_name.ToLowerInvariant()));

        /// <summary>
        /// Returns the name of the stream or an empty string if no name was set.
        /// </summary>
        public string Name => _name ?? String.Empty;

        /// <summary>
        /// Converts the hash to a hexadecimal encoded string.
        /// </summary>
        public string ToHexString()
        {
            var sb = new StringBuilder(HashLength * 2);

            foreach (var b in Hash)
                sb.Append(b.ToString("x2"));

            return sb.ToString();
        }

        /// <summary>
        /// Creates a stream from a byte array. (Used internally by projection streams)
        /// </summary>
        internal static Stream FromBytes(byte[] input)
        {
            var hash = ComputeHash(input);
            return new Stream(hash);
        }

        #region Equality

        public override int GetHashCode()
        {
            return ((IStructuralEquatable)Hash).GetHashCode(EqualityComparer<byte>.Default);
        }

        public override bool Equals(object obj)
        {
            if (obj is Stream)
                return Equals((Stream)obj);

            return false;
        }

        public bool Equals(Stream other)
        {
            return StructuralComparisons.StructuralEqualityComparer.Equals(Hash, other.Hash);
        }

        #endregion

        #region Operators

        public static implicit operator Stream(string streamName)
        {
            return new Stream(streamName);
        }

        public static implicit operator Stream(byte[] hash)
        {
            return new Stream(hash);
        }

        #endregion

        #region Helpers

        private static void CheckHashArgument(byte[] hash)
        {
            if (hash == null || hash.Length != HashLength)
                throw new ArgumentException($"The hash is invalid - must be exactly {HashLength} bytes.", "hash");
        }

        private static void CheckNameArgument(string name)
        {
            if (String.IsNullOrEmpty(name))
                throw new ArgumentException($"The stream name cannot be null or empty.", "name");
        }

        private static byte[] ComputeHash(string str)
        {
            var bytes = Encoding.UTF8.GetBytes(str ?? String.Empty);
            return ComputeHash(bytes);
        }

        private static byte[] ComputeHash(byte[] input)
        {
            var sha1 = new SHA1Managed();
            return sha1.ComputeHash(input ?? new byte[0]);
        }

        private string DebuggerText => $"{ToHexString()} ({Name})";

        #endregion
    }
}
