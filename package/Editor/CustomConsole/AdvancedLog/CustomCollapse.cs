using System;
using System.Collections.Generic;
using System.Text;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Needle.Demystify
{
	internal static class CustomCollapse
	{
		private class CustomDrawer : ICustomLogDrawer
		{
			public bool OnDrawEntry(int index, bool isSelected, Rect rect, bool visible, out float height)
			{
				if (index % 3 == 0) rect.height *= 2;
				height = ConsoleList.DrawDefaultRow(index, rect);
				
				GUIUtils.SimpleColored.SetPass(0);
				GL.PushMatrix();
				GL.Begin(GL.LINES);
				GL.Color(Color.red);
				GL.Vertex3(0, rect.y + height, 0);
				GL.Vertex3(rect.width, rect.y, 0);
				GL.End();
				GL.PopMatrix();
				return true;
			}
		}
		
		[InitializeOnLoadMethod]
		private static void Init()
		{
			ConsoleFilter.ClearingCachedData += OnClear;
			ConsoleFilter.CustomAddEntry += CustomAdd;
			ConsoleList.LogEntryContextMenu += OnLogEntryContext;
			ConsoleList.RegisterCustomDrawer(new CustomDrawer());
		}

		private static void OnLogEntryContext(GenericMenu menu, int itemIndex)
		{
			if (itemIndex <= 0) return;
			var item = ConsoleList.CurrentEntries[itemIndex];
			Debug.Log(item.str);
			menu.AddItem(new GUIContent(item.str.SanitizeMenuItemText()), false, () =>{});
		}

		private static void OnClear()
		{
			groupedLogs.Clear();
			collapsed.Clear();
		}

		private static readonly Dictionary<string, int> groupedLogs = new Dictionary<string, int>();
		private static readonly Dictionary<int, CollapseData> collapsed = new Dictionary<int, CollapseData>();

		private class CollapseData
		{
			
		}
		
		private static readonly StringBuilder builder = new StringBuilder();

		// number matcher https://regex101.com/r/D0dFIj/1/
		// non number matcher https://regex101.com/r/VRXwpC/1/
		// private static readonly Regex noNumberMatcher = new Regex(@"[^-\d.]+", RegexOptions.Compiled | RegexOptions.Multiline);

		private static bool CustomAdd(LogEntry entry, int row, string preview, List<CachedConsoleInfo> entries)
		{
			if (!DemystifySettings.instance.DynamicGrouping)
			{
				return true;
			}

			using (new ProfilerMarker("Console Log Grouping").Auto())
			{
				var text = preview;
				const string marker = "<group>";
				var start = text.IndexOf(marker, StringComparison.InvariantCulture);
				if (start <= 0) return false;
				const string timestampEnd = "] ";
				var timestampIndex = text.IndexOf(timestampEnd, StringComparison.Ordinal);
				var timestamp = string.Empty;
				if (timestampIndex < start)
				{
					timestamp = text.Substring(0, timestampIndex + timestampEnd.Length);
				}


				// var match = noNumberMatcher.Match(text);
				text = text.Substring(start + marker.Length).TrimStart();

				builder.Clear();
				var key = builder.Append(entry.file).Append("::").Append(entry.line).Append("::").ToString();
				builder.Clear();


				text = builder.Append(timestamp).Append(text).ToString();

				entry.message += "\n" + UnityDemystify.DemystifyEndMarker;
				var newEntry = new CachedConsoleInfo()
				{
					entry = new LogEntryInfo(entry),
					row = row,
					str = text,
					groupSize = 1
				};

				if (groupedLogs.TryGetValue(key, out var index))
				{
					var ex = entries[index];
					newEntry.row = ex.row;
					newEntry.groupSize = ex.groupSize + 1;
					var history = "\n" + ex.str;
					newEntry.str += history;
					newEntry.entry.message += history;
					entries[index] = newEntry;
				}
				else
				{
					groupedLogs.Add(key, entries.Count);
					collapsed.Add(entries.Count, new CollapseData());
					entries.Add(newEntry);
				}

				return false;
				// if (match.Success)
				// {
				// }
				// return false;
			}
		}
	}
}