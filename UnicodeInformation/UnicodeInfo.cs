﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace System.Unicode
{
	public sealed class UnicodeInfo
	{
		private static readonly UnicodeInfo @default = ReadEmbeddedUnicodeData();
		public static UnicodeInfo Default { get { return @default; } }

		private static UnicodeInfo ReadEmbeddedUnicodeData()
		{
			using (var stream = new DeflateStream(typeof(UnicodeInfo).GetTypeInfo().Assembly.GetManifestResourceStream("ucd.dat"), CompressionMode.Decompress, false))
			{
				return FromStream(stream);
			}
		}

		private readonly Version unicodeVersion;
		private readonly UnicodeCharacterData[] characterData;
		private readonly UnicodeBlock[] blockEntries;

		public static UnicodeInfo FromStream(Stream stream)
		{
			using (var reader = new BinaryReader(stream, Encoding.UTF8))
			{
				if (reader.ReadByte() != 'U'
					| reader.ReadByte() != 'C'
					| reader.ReadByte() != 'D')
					throw new InvalidDataException();

				byte formatVersion = reader.ReadByte();

#if !DEBUG
				if (formatVersion != 1) throw new InvalidDataException();
#endif

				var unicodeVersion = new Version(reader.ReadUInt16(), reader.ReadByte());

				var characterDataEntries = new UnicodeCharacterData[ReadCodePoint(reader)];

				for (int i = 0; i < characterDataEntries.Length; ++i)
				{
					characterDataEntries[i] = ReadCharacterDataEntry(reader);
				}

				var blockEntries = new UnicodeBlock[reader.ReadByte()];

				for (int i = 0; i < blockEntries.Length; ++i)
				{
					blockEntries[i] = ReadBlockEntry(reader);
                }

				return new UnicodeInfo(unicodeVersion, characterDataEntries, blockEntries);
			}
		}

		private static UnicodeCharacterData ReadCharacterDataEntry(BinaryReader reader)
		{
			var fields = (UcdFields)reader.ReadUInt16();

			var codePointRange = (fields & UcdFields.CodePointRange) != 0 ? new UnicodeCharacterRange(ReadCodePoint(reader), ReadCodePoint(reader)) : new UnicodeCharacterRange(ReadCodePoint(reader));

            string name = (fields & UcdFields.Name) != 0 ? reader.ReadString() : null;
			var category = (fields & UcdFields.Category) != 0 ? (UnicodeCategory)reader.ReadByte() : UnicodeCategory.OtherNotAssigned;
			var canonicalCombiningClass = (fields & UcdFields.CanonicalCombiningClass) != 0 ? (CanonicalCombiningClass)reader.ReadByte() : CanonicalCombiningClass.NotReordered;
			var bidirectionalClass = (fields & UcdFields.BidirectionalClass) != 0 ? (BidirectionalClass)reader.ReadByte() : 0;
			CompatibilityFormattingTag decompositionType = (fields & UcdFields.DecompositionMapping) != 0 ? (CompatibilityFormattingTag)reader.ReadByte() : CompatibilityFormattingTag.Canonical;
			string decompositionMapping = (fields & UcdFields.DecompositionMapping) != 0 ? reader.ReadString() : null;
			var numericType = (UnicodeNumericType)((int)(fields & UcdFields.NumericNumeric) >> 6);
			UnicodeRationalNumber numericValue = numericType != UnicodeNumericType.None ?
				new UnicodeRationalNumber(reader.ReadInt64(), reader.ReadByte()) :
				default(UnicodeRationalNumber);
			string oldName = (fields & UcdFields.OldName) != 0 ? reader.ReadString() : null;
			string simpleUpperCaseMapping = (fields & UcdFields.SimpleUpperCaseMapping) != 0 ? reader.ReadString() : null;
			string simpleLowerCaseMapping = (fields & UcdFields.SimpleLowerCaseMapping) != 0 ? reader.ReadString() : null;
			string simpleTitleCaseMapping = (fields & UcdFields.SimpleTitleCaseMapping) != 0 ? reader.ReadString() : null;
			ContributoryProperties contributoryProperties = (fields & UcdFields.ContributoryProperties) != 0 ? (ContributoryProperties)reader.ReadInt32() : 0;
			CoreProperties coreProperties = (fields & UcdFields.CoreProperties) != 0 ? (CoreProperties)ReadInt24(reader) : 0;

			return new UnicodeCharacterData
			(
				codePointRange,
				name,
				category,
				canonicalCombiningClass,
				bidirectionalClass,
				decompositionType,
				decompositionMapping,
				numericType,
				numericValue,
				(fields & UcdFields.BidirectionalMirrored) != 0,
				oldName,
				simpleUpperCaseMapping,
				simpleLowerCaseMapping,
				simpleTitleCaseMapping,
				contributoryProperties,
				coreProperties,
                null
			);
        }

		private static UnicodeBlock ReadBlockEntry(BinaryReader reader)
		{
			return new UnicodeBlock(new UnicodeCharacterRange(ReadCodePoint(reader), ReadCodePoint(reader)), reader.ReadString());
		}

		private static int ReadInt24(BinaryReader reader)
		{
			return reader.ReadByte() | ((reader.ReadByte() | (reader.ReadByte() << 8)) << 8);
		}

#if DEBUG
		internal static int ReadCodePoint(BinaryReader reader)
#else
		private static int ReadCodePoint(BinaryReader reader)
#endif
		{
			byte b = reader.ReadByte();

			if (b < 0xA0) return b;
			else if (b < 0xC0)
			{
				return 0xA0 + (((b & 0x1F) << 8) | reader.ReadByte());
			}
			else if (b < 0xE0)
			{
				return 0x20A0 + (((b & 0x1F) << 8) | reader.ReadByte());
			}
			else
			{
				return 0x40A0 + (((((b & 0x1F) << 8) | reader.ReadByte()) << 8) | reader.ReadByte());
            }
		}

		internal UnicodeInfo(Version unicodeVersion, UnicodeCharacterData[] characterData, UnicodeBlock[] blockEntries)
		{
			this.unicodeVersion = unicodeVersion;
			this.characterData = characterData;
			this.blockEntries = blockEntries;
        }

		public Version UnicodeVersion { get { return unicodeVersion; } }

		private UnicodeCharacterData FindCodePoint(int codePoint)
		{
			int minIndex = 0;
			int maxIndex = characterData.Length - 1;

			do
			{
				int index = (minIndex + maxIndex) >> 1;

				int Δ = characterData[index].CodePointRange.CompareCodePoint(codePoint);

				if (Δ == 0) return characterData[index];
				else if (Δ < 0) maxIndex = index - 1;
				else minIndex = index + 1;
			} while (minIndex <= maxIndex);

			return null;
		}

		private int FindBlockIndex(int codePoint)
		{
			int minIndex = 0;
			int maxIndex = blockEntries.Length - 1;

			do
			{
				int index = (minIndex + maxIndex) >> 1;

				int Δ = blockEntries[index].CodePointRange.CompareCodePoint(codePoint);

				if (Δ == 0) return index;
				else if (Δ < 0) maxIndex = index - 1;
				else minIndex = index + 1;
			} while (minIndex <= maxIndex);

			return -1;
		}

		private string GetBlockName(int codePoint)
		{
			int i = FindBlockIndex(codePoint);

			return i >= 0 ? blockEntries[i].Name : null;
        }

		public UnicodeCharInfo GetCharInfo(int codePoint)
		{
			return new UnicodeCharInfo(codePoint, FindCodePoint(codePoint), GetBlockName(codePoint));
		}

		public UnicodeBlock[] GetBlocks()
		{
			return (UnicodeBlock[])blockEntries.Clone();
        }
	}
}