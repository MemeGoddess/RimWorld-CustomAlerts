using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Verse;
using RimWorld;
using UnityEngine;
using TD_Find_Lib;
using System.IO;
using System.IO.Compression;
using System.Xml.Linq;
using System.Xml;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Serialization;
using HarmonyLib;

namespace Custom_Alerts
{
	// GameComponent to hold the SearchAlerts
	// SearchAlert holds a Alert_Find
	// Alert_Find have to be inserted into the game's AllAlerts
	public class CustomAlertsGameComp : GameComponent
	{
		public SearchAlertGroup alerts = new();

		public CustomAlertsGameComp()
		{
			
		}
		public CustomAlertsGameComp(Game g):base() { }

		public override void ExposeData()
		{
			Scribe_Deep.Look(ref alerts, "alerts");
			if (Scribe.mode == LoadSaveMode.PostLoadInit)
			{
				if (alerts == null)
					alerts = new();

				foreach (QuerySearchAlert searchAlert in alerts)
					LiveAlerts.AddAlert(searchAlert);
			}
		}

		public bool HasSavedAlert(string name) =>
			alerts.Any(sa => name == sa.search.name);

		public void AddAlert(QuerySearch search) =>
			AddAlert(new QuerySearchAlert(search));

		public void AddAlert(QuerySearchAlert newSearchAlert)
			=> alerts.TryAdd(newSearchAlert);

		public void AddAlerts(SearchGroup searches)
		{
			foreach (QuerySearch search in searches)
			{
				QuerySearchAlert newSearchAlert = new(search);

				if (HasSavedAlert(newSearchAlert.search.name))
					newSearchAlert.search.name += "TD.CopyNameSuffix".Translate();

				LiveAlerts.AddAlert(newSearchAlert);
				alerts.Add(newSearchAlert);
			}
		}

		public void RenameAlert(QuerySearchAlert searchAlert)
		{
			Find.WindowStack.Add(new Dialog_Name(searchAlert.search.name,
				name => searchAlert.search.name = name,
				rejector: name => alerts.Any(sa => sa.search.name == name)));
		}

		public void RemoveAlert(QuerySearchAlert searchAlert)
		{
			alerts.Remove(searchAlert);
			LiveAlerts.RemoveAlert(searchAlert);
		}
	}

	public class SearchAlertGroup : SearchGroupBase<QuerySearchAlert>
	{
		public override void Replace(QuerySearchAlert newSearchAlert, int i)
		{
			LiveAlerts.RemoveAlert(this[i]);
			base.Replace(newSearchAlert, i);
			LiveAlerts.AddAlert(newSearchAlert);
		}

		public override void Copy(QuerySearchAlert newSearchAlert, int i)
		{
			base.Copy(newSearchAlert, i);
			LiveAlerts.AddAlert(newSearchAlert);
		}

		public override void DoAdd(QuerySearchAlert newSearchAlert)
		{
			base.DoAdd(newSearchAlert);
			LiveAlerts.AddAlert(newSearchAlert);
		}

		public SearchGroup AsSearchGroup()
		{
			SearchGroup clone = new("TD.CustomAlerts".Translate(), null);
			clone.AddRange(this.Select(sa => sa.Search));
			return clone;
		}
	}

	[StaticConstructorOnStartup]
	public class AlertsFromSaveFiles : ISearchProvider
	{
		static AlertsFromSaveFiles()
		{
			SearchTransfer.Register(new AlertsFromSaveFiles());
		}
		public ISearchProvider.Method ProvideMethod() => ISearchProvider.Method.Library;

		public QuerySearch ProvideSingle() => null;

		public SearchGroup ProvideGroup() => null;
		private Dictionary<string, SearchGroup> saves = new();
		public List<SearchGroup> ProvideLibrary() => SaveInspector.CustomAlertsList;

		public string Source => null;
		public string ProvideName => "From Saves";
	}

	[HarmonyPatch(typeof(GameDataSaveLoader), nameof(GameDataSaveLoader.LoadGame), [typeof(string)])]
	public static class SaveInspector
	{
		public static List<SearchGroup> CustomAlertsList = new List<SearchGroup>();

		public static void Postfix(string saveFileName)
		{
			LongEventHandler.QueueLongEvent(() =>
			{
				var stopwatch = Stopwatch.StartNew();
				CustomAlertsList = GenFilePaths.AllSavedGameFiles.Where(x =>
					Path.GetFileNameWithoutExtension(x.Name) != saveFileName && !x.Name.StartsWith("Autosave")).Take(50).Select(x =>
				{
					var path = Path.GetFileNameWithoutExtension(x.FullName);

					var comp = SaveInspector.GetCompFromSave(path);
					if (comp == null)
						return null;
					var group = new SearchGroup(Path.GetFileNameWithoutExtension(x.Name), null);
					group.AddRange(comp.Select(x => x.Search));

					return group;
				}).Where(x => x != null).ToList();
				stopwatch.Stop();
				Log.Message($"Took {stopwatch.Elapsed.TotalSeconds} seconds to load saves");
			}, "LoadingSaves", false, null);
		}

		internal static SearchAlertGroup GetCompFromSave(string path)
		{
			var searchXml = GrabNode(path);

			if (searchXml == null)
				return null;

			var searches = new List<QuerySearch>();
			foreach (string search in searchXml)
			{
				var cleanedMap = Regex.Replace(search, "<searchMaps>.*</searchMaps>", "<searchMaps />", RegexOptions.Singleline);
				var cleanedMapType = Regex.Replace(cleanedMap, "<mapType>\\w*</mapType>", "");
				var tempDoc = 
$@"<TD_Find_Lib.QuerySearch>
<saveable Class=""TD_Find_Lib.QuerySearch"">
{cleanedMapType}
</saveable>
</TD_Find_Lib.QuerySearch>";

				var query = ScribeXmlFromString.LoadFromString<QuerySearch>(tempDoc);
				searches.Add(query);
			}

			var newGroup = new SearchAlertGroup();
			newGroup.AddRange(searches.Select(x => new QuerySearchAlert(x, true)));
			return newGroup;
		}

		public static string[] GrabNode(string saveNameNoExt)
		{
			var path = GenFilePaths.FilePathForSavedGame(saveNameNoExt);
			var text = File.OpenText(path);
			var capture = false;

			var stringBuilder = new StringBuilder();
			while (!text.EndOfStream)
			{
				var line = text.ReadLine();
				if (line.Contains("<components>"))
					capture = true;
				else if (capture && line.Contains("</components>"))
					break;
				if(capture)
					stringBuilder.Append(line);
			}

			var filtered = stringBuilder.ToString();
			var regex = Regex.Match(filtered, "<li Class=\"Custom_Alerts.CustomAlertsGameComp\">.*<alerts>.*<searches>(.*)<\\/searches>.*<\\/alerts>.*?<\\/li>",
				RegexOptions.Singleline);

			if (!regex.Success)
				return [];

			var matches = Regex.Matches(regex.Groups[1].Value, "<li>.*?<search>(.*?)</search>.*?</li>", RegexOptions.Singleline);
			return matches.Select(x => x.Groups[1].Value).ToArray();
		}
	}
}
