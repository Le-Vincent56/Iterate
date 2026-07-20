using System.Collections.Generic;
using System.Text;

namespace Iterate.Domain.Determinism
{
    /// <summary>
    /// The canonical byte encoding of a <see cref="DecisionContextComponents"/>. Required
    /// strings are written as an int32 little-endian byte-length prefix followed by their UTF-8 bytes;
    /// each optional slot is a presence byte (<c>0x00</c> absent / <c>0x01</c> present) followed, when
    /// present, by the same length-prefixed UTF-8; the occurrence ordinal is a fixed 4-byte little-endian
    /// integer. Every optional slot always writes its presence byte, so component omission cannot collide.
    /// The layout is fixed under revision identity <c>iterate-rng-1</c>.
    /// </summary>
    public static class ContextEncoder
    {
        /// <summary>
        /// Encodes <paramref name="components"/> to its canonical little-endian byte form.
        /// </summary>
        /// <param name="components">The decision-context components to encode.</param>
        /// <returns>The canonical encoded bytes, ready to hash.</returns>
        public static byte[] Encode(DecisionContextComponents components)
        {
            List<byte> buffer = new List<byte>();
            WriteRequiredString(buffer, components.SessionSeedIdentity);
            WriteOptionalString(buffer, components.ContentRevision);
            WriteOptionalString(buffer, components.RulesetRevision);
            WriteOptionalString(buffer, components.SystemIdentity);
            WriteOptionalString(buffer, components.ProcessIdentity);
            WriteOptionalString(buffer, components.ScopeIdentity);
            WriteOptionalString(buffer, components.CausingEventIdentity);
            WriteOptionalString(buffer, components.EffectOriginIdentity);
            WriteRequiredString(buffer, components.SelectionPurpose);
            WriteInt32LittleEndian(buffer, components.OccurrenceOrdinal);
            return buffer.ToArray();
        }

        /// <summary>
        /// Writes a required string as an int32 little-endian byte-length prefix plus its UTF-8 bytes.
        /// </summary>
        /// <param name="buffer">The output buffer.</param>
        /// <param name="value">The required, non-empty string.</param>
        private static void WriteRequiredString(List<byte> buffer, string value)
        {
            byte[] utf8 = Encoding.UTF8.GetBytes(value);
            WriteInt32LittleEndian(buffer, utf8.Length);
            buffer.AddRange(utf8);
        }

        /// <summary>
        /// Writes an optional string: a presence byte, then the length-prefixed UTF-8 bytes when present.
        /// </summary>
        /// <param name="buffer">The output buffer.</param>
        /// <param name="value">The optional string, or null when absent.</param>
        private static void WriteOptionalString(List<byte> buffer, string value)
        {
            if (value == null)
            {
                buffer.Add(0x00);
                return;
            }

            buffer.Add(0x01);
            byte[] utf8 = Encoding.UTF8.GetBytes(value);
            WriteInt32LittleEndian(buffer, utf8.Length);
            buffer.AddRange(utf8);
        }

        /// <summary>
        /// Appends <paramref name="value"/> as four little-endian bytes.
        /// </summary>
        /// <param name="buffer">The output buffer.</param>
        /// <param name="value">The 32-bit value to append.</param>
        private static void WriteInt32LittleEndian(List<byte> buffer, int value)
        {
            buffer.Add((byte)(value & 0xFF));
            buffer.Add((byte)((value >> 8) & 0xFF));
            buffer.Add((byte)((value >> 16) & 0xFF));
            buffer.Add((byte)((value >> 24) & 0xFF));
        }
    }
}