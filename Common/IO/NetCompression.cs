﻿using Ionic.Zlib;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace MagicStorage.Common.IO {
	/// <summary>
	/// This helper class contains methods for maximizing compression of data for I/O
	/// </summary>
	public static class NetCompression {
		private class Loadable : ILoadable {
			void ILoadable.Load(Mod mod) { }

			void ILoadable.Unload() => Unload();
		}

		private static readonly List<IDataSizeTracker> _trackers = new();

		internal static int Add(IDataSizeTracker tracker) {
			_trackers.Add(tracker);
			return _trackers.Count - 1;
		}

		private static void Unload() {
			_trackers.Clear();
		}

		public static void SendItem(Item item, BinaryWriter writer, bool writeStack = true, bool writeFavorite = true) {
			ValueWriter valueWriter = new(writer);
			SendItem(item, valueWriter, writeStack, writeFavorite);
		}

		public static void SendItem(Item item, ValueWriter writer, bool writeStack, bool writeFavorite) {
			ModContent.GetInstance<ItemTypeTracker>().Send(item, writer);
			ModContent.GetInstance<ItemPrefixTracker>().Send(item, writer);

			if (writeStack)
				writer.Write(item.stack, BitBuffer128.MAX_INT);

			if (writeFavorite)
				writer.Write(item.favorited);

			using MemoryStream modData = new MemoryStream();
			using (BinaryWriter modWriter = new BinaryWriter(modData))
				ItemIO.SendModData(item, modWriter);

			writer.WriteBytes(modData.ToArray());
		}

		public static void SendItems(List<Item> items, BinaryWriter writer, bool writeStacks = true, bool writeFavorites = true, int? listCountBitSizeOverride = null) {
			ValueWriter valueWriter = new(writer);
			SendItems(items, valueWriter, writeStacks, writeFavorites, listCountBitSizeOverride);
		}

		public static void SendItems(List<Item> items, ValueWriter writer, bool writeStacks = true, bool writeFavorites = true, int? listCountBitSizeOverride = null) {
			writer.Write(items.Count, listCountBitSizeOverride ?? BitBuffer128.MAX_INT);
			foreach (Item item in items)
				SendItem(item, writer, writeStacks, writeFavorites);
		}

		public static Item ReceiveItem(BinaryReader reader, bool readStack = true, bool readFavorite = true) {
			ValueReader valueReader = new(reader);
			return ReceiveItem(valueReader, readStack, readFavorite);
		}

		public static Item ReceiveItem(ValueReader reader, bool readStack, bool readFavorite) {
			Item item = new Item();

			ModContent.GetInstance<ItemTypeTracker>().Receive(ref item, reader);
			ModContent.GetInstance<ItemPrefixTracker>().Receive(ref item, reader);

			if (readStack)
				item.stack = reader.ReadInt32(BitBuffer128.MAX_INT);

			if (readFavorite)
				item.favorited = reader.ReadBoolean();

			using MemoryStream modData = new MemoryStream(reader.ReadBytes());
			using (BinaryReader modReader = new BinaryReader(modData))
				ItemIO.ReceiveModData(item, modReader);

			return item;
		}

		public static List<Item> ReceiveItems(BinaryReader reader, bool readStacks = true, bool readFavorites = true, int? listCountBitSizeOverride = null) {
			ValueReader valueReader = new(reader);
			int count = valueReader.ReadInt32(listCountBitSizeOverride ?? BitBuffer128.MAX_INT);
			List<Item> items = new(count);
			for (int k = 0; k < count; k++)
				items.Add(ReceiveItem(valueReader, readStacks, readFavorites));
			return items;
		}

		public static List<Item> ReceiveItems(ValueReader reader, bool readStacks = true, bool readFavorites = true, int? listCountBitSizeOverride = null) {
			int count = reader.ReadInt32(listCountBitSizeOverride ?? BitBuffer128.MAX_INT);
			List<Item> items = new(count);
			for (int k = 0; k < count; k++)
				items.Add(ReceiveItem(reader, readStacks, readFavorites));
			return items;
		}

		/// <summary>
		/// Compresses <paramref name="data"/> at the provided <paramref name="level"/>
		/// </summary>
		/// <param name="data">The decompressed byte array</param>
		/// <param name="level">The compression level</param>
		/// <returns>The compressed byte array</returns>
		public static byte[] Compress(byte[] data, CompressionLevel level) {
			using MemoryStream decompressed = new(data);
			using DeflateStream compression = new(decompressed, CompressionMode.Compress, level);
			using MemoryStream compressed = new();
			compression.CopyTo(compressed);
			return compressed.ToArray();
		}

		/// <summary>
		/// Decompresses <paramref name="data"/> at the provided <paramref name="level"/>
		/// </summary>
		/// <param name="data">The compressed byte array</param>
		/// <param name="level">The compression level</param>
		/// <returns>The decompressed byte array</returns>
		public static byte[] Decompress(byte[] data, CompressionLevel level) {
			using MemoryStream compressed = new(data);
			using DeflateStream decompression = new(compressed, CompressionMode.Decompress, level);
			using MemoryStream decompressed = new();
			decompression.CopyTo(decompressed);
			return decompressed.ToArray();
		}

		public static int GetBitSize(byte value) {
			if (value == 0)
				return 0;

			int bits = value;
			int size = 1;
			while (bits != 0) {
				bits >>= 1;
				size++;
			}

			return size;
		}

		public static int GetBitSize(sbyte value) {
			if (value == 0 || value == sbyte.MinValue)
				return 0;

			int bits = Math.Abs(value);
			int size = 1;
			while (bits != 0) {
				bits >>= 1;
				size++;
			}

			return size;
		}

		public static int GetBitSize(ushort value) {
			if (value == 0)
				return 0;

			int bits = value;
			int size = 1;
			while (bits != 0) {
				bits >>= 1;
				size++;
			}

			return size;
		}

		public static int GetBitSize(short value) {
			if (value == 0 || value == short.MinValue)
				return 0;

			int bits = Math.Abs(value);
			int size = 1;
			while (bits != 0) {
				bits >>= 1;
				size++;
			}

			return size;
		}

		public static int GetBitSize(uint value) {
			if (value == 0)
				return 0;

			uint bits = value;
			int size = 1;
			while (bits != 0) {
				bits >>= 1;
				size++;
			}

			return size;
		}

		public static int GetBitSize(int value) {
			if (value == 0 || value == int.MinValue)
				return 0;

			int bits = Math.Abs(value);
			int size = 1;
			while (bits != 0) {
				bits >>= 1;
				size++;
			}

			return size;
		}

		public static int GetBitSize(ulong value) {
			if (value == 0)
				return 0;

			ulong bits = value;
			int size = 1;
			while (bits != 0) {
				bits >>= 1;
				size++;
			}

			return size;
		}

		public static int GetBitSize(long value) {
			if (value == 0 || value == long.MinValue)
				return 0;

			long bits = Math.Abs(value);
			int size = 1;
			while (bits != 0) {
				bits >>= 1;
				size++;
			}

			return size;
		}
	}
}