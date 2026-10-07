#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using OpenRA.FileSystem;

namespace OpenRA.Graphics
{
	public delegate ISpriteFrame AdjustFrame(ISpriteFrame input, int index, int total);

	/// <summary>
	/// Keeps packed sprites between sessions (e.g. the browser host caches them, so a map loads without decoding
	/// and packing every sprite file again). Render-side only: sprites never affect the simulation.
	/// </summary>
	public interface ISpriteCacheStore
	{
		/// <summary>The sprites of every reservation token, if the store holds a packing of exactly this reservation set.</summary>
		IReadOnlyDictionary<int, Sprite[]> TryRestore(SpriteCache cache, string key);

		/// <summary>Called after the reservations were loaded and packed; <paramref name="files"/> maps each token to its file.</summary>
		void Save(SpriteCache cache, string key, IReadOnlyDictionary<int, Sprite[]> sprites, IReadOnlyDictionary<int, string> files);
	}

	public sealed class SpriteCache : IDisposable
	{
		/// <summary>Optional packed-sprite store used by LoadReservations (null on the desktop).</summary>
		public static ISpriteCacheStore Store;

		/// <summary>
		/// Optional (null on the desktop): a group per sprite file, e.g. the faction that uses it. Each group's sprites
		/// are packed into sheets of their own, so a host can load only the groups a game needs. Render-side only.
		/// </summary>
		public static Func<string, string> PackGroup;

		public readonly Dictionary<SheetType, SheetBuilder> SheetBuilders;
		readonly ISpriteLoader[] loaders;
		readonly IReadOnlyFileSystem fileSystem;

		readonly Dictionary<
			int,
			(ImmutableArray<int> Frames, MiniYamlNode.SourceLocation Location, AdjustFrame AdjustFrame, bool Premultiplied)> spriteReservations = [];
		readonly Dictionary<string, List<int>> reservationsByFilename = [];

		readonly Dictionary<int, Sprite[]> resolvedSprites = [];

		readonly Dictionary<int, (string Filename, MiniYamlNode.SourceLocation Location)> missingFiles = [];

		int nextReservationToken = 1;

		public SpriteCache(
			IReadOnlyFileSystem fileSystem, ISpriteLoader[] loaders, int bgraSheetSize, int indexedSheetSize, int bgraSheetMargin = 1, int indexedSheetMargin = 1)
		{
			SheetBuilders = new Dictionary<SheetType, SheetBuilder>
			{
				{ SheetType.Indexed, new SheetBuilder(SheetType.Indexed, indexedSheetSize, indexedSheetMargin) },
				{ SheetType.BGRA, new SheetBuilder(SheetType.BGRA, bgraSheetSize, bgraSheetMargin) }
			};

			this.fileSystem = fileSystem;
			this.loaders = loaders;
		}

		public int ReserveSprites(string filename, ImmutableArray<int> frames, MiniYamlNode.SourceLocation location,
			AdjustFrame adjustFrame = null, bool premultiplied = false)
		{
			var token = nextReservationToken++;
			spriteReservations[token] = (frames, location, adjustFrame, premultiplied);
			reservationsByFilename.GetOrAdd(filename, _ => []).Add(token);
			return token;
		}

		static ISpriteFrame[] GetFrames(IReadOnlyFileSystem fileSystem, string filename, ISpriteLoader[] loaders)
		{
			if (!fileSystem.TryOpen(filename, out var stream))
				return null;

			using (stream)
			{
				foreach (var loader in loaders)
					if (loader.TryParseSprite(stream, filename, out var frames, out _))
						return frames;

				return null;
			}
		}

		/// <summary>Every pending reservation (token, file), before LoadReservations.</summary>
		public IEnumerable<(int Token, string Filename)> ReservedTokens()
		{
			foreach (var (filename, tokens) in reservationsByFilename)
				foreach (var token in tokens)
					yield return (token, filename);
		}

		public ISpriteFrame[] LoadFramesUncached(string filename)
		{
			return GetFrames(fileSystem, filename, loaders);
		}

		/// <summary>Identifies the reservation set (files, frames, frame adjustments, premultiplication), FNV-1a 64.</summary>
		public string ReservationKey()
		{
			var hash = 14695981039346656037UL;
			void Add(string s)
			{
				foreach (var c in s)
				{
					hash ^= c;
					hash *= 1099511628211UL;
				}

				hash ^= 0xFF;
				hash *= 1099511628211UL;
			}

			foreach (var (filename, tokens) in reservationsByFilename)
			{
				Add(filename);
				if (PackGroup != null)
					Add("group:" + PackGroup(filename));

				foreach (var token in tokens)
				{
					var rs = spriteReservations[token];
					Add(token.ToString(CultureInfo.InvariantCulture));
					Add(rs.Frames.IsDefault ? "*" : string.Join(',', rs.Frames));
					Add(rs.Premultiplied ? "p" : "-");
					Add(rs.AdjustFrame == null ? "" : rs.AdjustFrame.Method.DeclaringType?.FullName + "." + rs.AdjustFrame.Method.Name);
				}
			}

			return hash.ToString("x16", CultureInfo.InvariantCulture) + "-" + spriteReservations.Count.ToString(CultureInfo.InvariantCulture);
		}

		bool TryRestore(string key)
		{
			var restored = Store.TryRestore(this, key);
			if (restored == null)
				return false;

			foreach (var (filename, tokens) in reservationsByFilename)
			{
				foreach (var token in tokens)
				{
					if (restored.TryGetValue(token, out var sprites) && sprites != null)
						resolvedSprites[token] = sprites;
					else
					{
						resolvedSprites[token] = null;
						missingFiles[token] = (filename, spriteReservations[token].Location);
					}
				}
			}

			spriteReservations.Clear();
			spriteReservations.TrimExcess();
			reservationsByFilename.Clear();
			reservationsByFilename.TrimExcess();
			return true;
		}

		public void LoadReservations(ModData modData)
		{
			string storeKey = null;
			if (Store != null)
			{
				storeKey = ReservationKey();
				if (TryRestore(storeKey))
					return;
			}

			var tokenFiles = new Dictionary<int, string>();
			var pendingResolve = new List<(
				string Filename,
				int FrameIndex,
				bool Premultiplied,
				AdjustFrame AdjustFrame,
				ISpriteFrame Frame,
				Sprite[] SpritesForToken)>();
			foreach (var (filename, tokens) in reservationsByFilename)
			{
				modData.LoadScreen?.Display();
				var loadedFrames = GetFrames(fileSystem, filename, loaders);
				foreach (var token in tokens)
				{
					tokenFiles[token] = filename;
					if (spriteReservations.TryGetValue(token, out var rs))
					{
						if (loadedFrames != null)
						{
							var resolved = new Sprite[loadedFrames.Length];
							resolvedSprites[token] = resolved;
							if (rs.Frames != null && rs.Frames.Any(i => i >= loadedFrames.Length))
								throw new InvalidOperationException($"{rs.Location}: {filename} does not contain frames: " +
									string.Join(',', rs.Frames.Where(f => f >= loadedFrames.Length)));

							var frames = rs.Frames != null ? rs.Frames : Enumerable.Range(0, loadedFrames.Length);
							var total = rs.Frames != null ? rs.Frames.Length : loadedFrames.Length;

							var j = 0;
							foreach (var i in frames)
							{
								var frame = loadedFrames[i];
								if (rs.AdjustFrame != null)
									frame = rs.AdjustFrame(frame, j++, total);
								pendingResolve.Add((filename, i, rs.Premultiplied, rs.AdjustFrame, frame, resolved));
							}
						}
						else
						{
							resolvedSprites[token] = null;
							missingFiles[token] = (filename, rs.Location);
						}
					}
				}
			}

			spriteReservations.Clear();
			spriteReservations.TrimExcess();
			reservationsByFilename.Clear();
			reservationsByFilename.TrimExcess();

			// When the sheet builder is adding sprites, it reserves height for the tallest sprite seen along the row.
			// We can achieve better sheet packing by keeping sprites with similar heights together.
			var orderedPendingResolve = PackGroup == null
				? pendingResolve.OrderBy(x => x.Frame.Size.Height)
				: pendingResolve.OrderBy(x => PackGroup(x.Filename) ?? "", StringComparer.Ordinal).ThenBy(x => x.Frame.Size.Height);
			string currentGroup = null;

			var spriteCache = new Dictionary<(
				string Filename,
				int FrameIndex,
				bool Premultiplied,
				AdjustFrame AdjustFrame),
				Sprite>(pendingResolve.Count);
			foreach (var (filename, frameIndex, premultiplied, adjustFrame, frame, spritesForToken) in orderedPendingResolve)
			{
				if (PackGroup != null)
				{
					var group = PackGroup(filename) ?? "";
					if (group != currentGroup)
					{
						if (currentGroup != null)
							foreach (var sb in SheetBuilders.Values)
								sb.StartNewSheet();

						currentGroup = group;
					}
				}

				// Premultiplied and non-premultiplied sprites must be cached separately
				// to cover the case where the same image is requested in both versions.
				spritesForToken[frameIndex] = spriteCache.GetOrAdd(
					(filename, frameIndex, premultiplied, adjustFrame),
					_ =>
					{
						var sheetBuilder = SheetBuilders[SheetBuilder.FrameTypeToSheetType(frame.Type)];
						return sheetBuilder.Add(frame, premultiplied);
					});

				modData.LoadScreen?.Display();
			}

			foreach (var sb in SheetBuilders.Values)
				sb.Current?.ReleaseBuffer();

			if (storeKey != null)
				Store.Save(this, storeKey, resolvedSprites, tokenFiles);
		}

		public Sprite[] ResolveSprites(int token)
		{
			if (!resolvedSprites.Remove(token, out var resolved))
				throw new InvalidOperationException($"{nameof(token)} {token} has either already been resolved, or was never reserved via {nameof(ReserveSprites)}");

			resolvedSprites.TrimExcess();

			if (missingFiles.TryGetValue(token, out var r))
				throw new FileNotFoundException($"{r.Location}: {r.Filename} not found", r.Filename);

			return resolved;
		}

		public IEnumerable<(string Filename, MiniYamlNode.SourceLocation Location)> MissingFiles => missingFiles.Values.ToHashSet();

		public void Dispose()
		{
			foreach (var sb in SheetBuilders.Values)
				sb.Dispose();
		}
	}
}
