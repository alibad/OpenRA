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
using System.Globalization;
using System.Text;

namespace OpenRA.Graphics
{
	/// <summary>
	/// Prepares scripts that need contextual glyph selection or right-to-left
	/// ordering for OpenRA's character-at-a-time sprite font renderer.
	/// </summary>
	public static class UnicodeText
	{
		readonly struct ArabicForm(char isolated, char final, char initial = '\0', char medial = '\0')
		{
			public readonly char Isolated = isolated;
			public readonly char Final = final;
			public readonly char Initial = initial;
			public readonly char Medial = medial;
			public bool JoinsPrevious => Final != '\0';
			public bool JoinsNext => Initial != '\0';
		}

		static readonly IReadOnlyDictionary<char, ArabicForm> ArabicForms = new Dictionary<char, ArabicForm>
		{
			['\u0621'] = new('\uFE80', '\0'),
			['\u0622'] = new('\uFE81', '\uFE82'),
			['\u0623'] = new('\uFE83', '\uFE84'),
			['\u0624'] = new('\uFE85', '\uFE86'),
			['\u0625'] = new('\uFE87', '\uFE88'),
			['\u0626'] = new('\uFE89', '\uFE8A', '\uFE8B', '\uFE8C'),
			['\u0627'] = new('\uFE8D', '\uFE8E'),
			['\u0628'] = new('\uFE8F', '\uFE90', '\uFE91', '\uFE92'),
			['\u0629'] = new('\uFE93', '\uFE94'),
			['\u062A'] = new('\uFE95', '\uFE96', '\uFE97', '\uFE98'),
			['\u062B'] = new('\uFE99', '\uFE9A', '\uFE9B', '\uFE9C'),
			['\u062C'] = new('\uFE9D', '\uFE9E', '\uFE9F', '\uFEA0'),
			['\u062D'] = new('\uFEA1', '\uFEA2', '\uFEA3', '\uFEA4'),
			['\u062E'] = new('\uFEA5', '\uFEA6', '\uFEA7', '\uFEA8'),
			['\u062F'] = new('\uFEA9', '\uFEAA'),
			['\u0630'] = new('\uFEAB', '\uFEAC'),
			['\u0631'] = new('\uFEAD', '\uFEAE'),
			['\u0632'] = new('\uFEAF', '\uFEB0'),
			['\u0633'] = new('\uFEB1', '\uFEB2', '\uFEB3', '\uFEB4'),
			['\u0634'] = new('\uFEB5', '\uFEB6', '\uFEB7', '\uFEB8'),
			['\u0635'] = new('\uFEB9', '\uFEBA', '\uFEBB', '\uFEBC'),
			['\u0636'] = new('\uFEBD', '\uFEBE', '\uFEBF', '\uFEC0'),
			['\u0637'] = new('\uFEC1', '\uFEC2', '\uFEC3', '\uFEC4'),
			['\u0638'] = new('\uFEC5', '\uFEC6', '\uFEC7', '\uFEC8'),
			['\u0639'] = new('\uFEC9', '\uFECA', '\uFECB', '\uFECC'),
			['\u063A'] = new('\uFECD', '\uFECE', '\uFECF', '\uFED0'),
			['\u0641'] = new('\uFED1', '\uFED2', '\uFED3', '\uFED4'),
			['\u0642'] = new('\uFED5', '\uFED6', '\uFED7', '\uFED8'),
			['\u0643'] = new('\uFED9', '\uFEDA', '\uFEDB', '\uFEDC'),
			['\u0644'] = new('\uFEDD', '\uFEDE', '\uFEDF', '\uFEE0'),
			['\u0645'] = new('\uFEE1', '\uFEE2', '\uFEE3', '\uFEE4'),
			['\u0646'] = new('\uFEE5', '\uFEE6', '\uFEE7', '\uFEE8'),
			['\u0647'] = new('\uFEE9', '\uFEEA', '\uFEEB', '\uFEEC'),
			['\u0648'] = new('\uFEED', '\uFEEE'),
			['\u0649'] = new('\uFEEF', '\uFEF0'),
			['\u064A'] = new('\uFEF1', '\uFEF2', '\uFEF3', '\uFEF4'),
			['\u0671'] = new('\uFB50', '\uFB51'),
			['\u067E'] = new('\uFB56', '\uFB57', '\uFB58', '\uFB59'),
			['\u0686'] = new('\uFB7A', '\uFB7B', '\uFB7C', '\uFB7D'),
			['\u0698'] = new('\uFB8A', '\uFB8B'),
			['\u06A4'] = new('\uFB6A', '\uFB6B', '\uFB6C', '\uFB6D'),
			['\u06A9'] = new('\uFB8E', '\uFB8F', '\uFB90', '\uFB91'),
			['\u06AF'] = new('\uFB92', '\uFB93', '\uFB94', '\uFB95'),
			['\u06CC'] = new('\uFBFC', '\uFBFD', '\uFBFE', '\uFBFF'),
		};

		public static string PrepareForDisplay(string text)
		{
			if (string.IsNullOrEmpty(text))
				return text;

			var logical = text.ToCharArray();
			var shaped = text.ToCharArray();
			var containsArabic = false;
			for (var i = 0; i < logical.Length; i++)
			{
				if (!ArabicForms.TryGetValue(logical[i], out var form))
					continue;

				containsArabic = true;
				var previous = PreviousBaseCharacter(logical, i);
				var next = NextBaseCharacter(logical, i);
				var joinsPrevious = previous >= 0 && ArabicForms.TryGetValue(logical[previous], out var previousForm) &&
					previousForm.JoinsNext && form.JoinsPrevious;
				var joinsNext = next >= 0 && ArabicForms.TryGetValue(logical[next], out var nextForm) &&
					form.JoinsNext && nextForm.JoinsPrevious;

				shaped[i] = joinsPrevious && joinsNext ? form.Medial :
					joinsPrevious ? form.Final : joinsNext ? form.Initial : form.Isolated;
			}

			return containsArabic ? ReorderArabicRuns(new string(shaped)) : text;
		}

		static int PreviousBaseCharacter(char[] text, int index)
		{
			for (var i = index - 1; i >= 0; i--)
			{
				if (!IsCombiningMark(text[i]))
					return i;
			}

			return -1;
		}

		static int NextBaseCharacter(char[] text, int index)
		{
			for (var i = index + 1; i < text.Length; i++)
			{
				if (!IsCombiningMark(text[i]))
					return i;
			}

			return -1;
		}

		static bool IsCombiningMark(char c)
		{
			var category = char.GetUnicodeCategory(c);
			return category == UnicodeCategory.NonSpacingMark || category == UnicodeCategory.SpacingCombiningMark;
		}

		static bool IsArabic(char c)
		{
			return ArabicForms.ContainsKey(c) ||
				(c >= '\uFB50' && c <= '\uFDFF') ||
				(c >= '\uFE70' && c <= '\uFEFF');
		}

		static bool IsArabicRunCharacter(char c)
		{
			return IsArabic(c) || IsCombiningMark(c) || char.IsWhiteSpace(c) || char.IsDigit(c) ||
				c is '\u060C' or '\u061B' or '\u061F';
		}

		static string ReorderArabicRuns(string text)
		{
			var output = new StringBuilder(text.Length);
			for (var i = 0; i < text.Length;)
			{
				if (!IsArabic(text[i]))
				{
					output.Append(text[i++]);
					continue;
				}

				var end = i + 1;
				while (end < text.Length && IsArabicRunCharacter(text[end]))
					end++;

				// Include terminal sentence punctuation so it moves to the visual
				// left edge of the right-to-left run.
				if (end < text.Length && text[end] is '.' or '!' or '?' &&
					(end + 1 == text.Length || text[end + 1] == '\n'))
					end++;

				AppendReversedRun(output, text.AsSpan(i, end - i));
				i = end;
			}

			return output.ToString();
		}

		static void AppendReversedRun(StringBuilder output, System.ReadOnlySpan<char> run)
		{
			var clusters = new List<string>();
			for (var i = 0; i < run.Length;)
			{
				var start = i++;
				while (i < run.Length && IsCombiningMark(run[i]))
					i++;
				clusters.Add(run[start..i].ToString());
			}

			clusters.Reverse();
			for (var i = 0; i < clusters.Count;)
			{
				if (!char.IsDigit(clusters[i][0]))
				{
					i++;
					continue;
				}

				var end = i + 1;
				while (end < clusters.Count && char.IsDigit(clusters[end][0]))
					end++;
				clusters.Reverse(i, end - i);
				i = end;
			}

			foreach (var cluster in clusters)
				output.Append(cluster);
		}
	}
}
