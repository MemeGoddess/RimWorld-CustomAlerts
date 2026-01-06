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
using System.Xml.Serialization;

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
		private List<SearchGroup> _library;
		public List<SearchGroup> ProvideLibrary() => _library ??=
			GenFilePaths.AllSavedGameFiles
				//.AsParallel()
				.Select(x =>
				{
					var path = Path.GetFileNameWithoutExtension(x.FullName);
					var pathWithFile = x.FullName;
					if (saves.TryGetValue(path, out var savedComp))
						return savedComp;

					//Scribe.loader.InitLoading(pathWithFile);
					//var game = new Game()
					//{
					//	initData = new GameInitData()
					//	{
					//		gameToLoad = pathWithFile
					//	}
					//};
					//game.LoadGame();
					//var doc = SaveInspector.LoadSaveDocument(path);
					//Scribe_Deep.Look(ref game, "game");
					//List<GameComponent> list = new List<GameComponent>();
					//Scribe_Collections.Look<GameComponent>(ref list, "components");

					//game.ExposeSmallComponents();
					//game.components.ForEach(x =>
					//{
					//	x.FinalizeInit();
					//	x.StartedNewGame();
					//	x.ExposeData();
					//});
					//var group = game.GetComponent<CustomAlertsGameComp>().alerts.AsSearchGroup();
					var comp = SaveInspector.GetCompFromSave(path);
					if (comp == null)
						return null;
					var group = new SearchGroup(Path.GetFileNameWithoutExtension(x.Name), null);
					group.AddRange(comp.Select(x => x.Search));
					saves[path] = group;
					return group;
				}).Where(x => x != null).ToList();

		public string Source => null;
		public string ProvideName => "From Saves";
	}

	public static class SaveInspector
	{
		internal static SearchAlertGroup GetCompFromSave(string path)
		{
			Log.Message(path);
			var stopwatch = Stopwatch.StartNew();
			var doc = LoadSaveDocument(path);

			stopwatch.Stop();
			Log.Message($"LoadDoc: {stopwatch.ElapsedTicks}");
			stopwatch.Restart();

			var element = FindGameComponentNode(doc, typeof(CustomAlertsGameComp));
			stopwatch.Stop();
			Log.Message($"LoadElement: {stopwatch.ElapsedTicks}");
			stopwatch.Restart();
			if (element == null)
				return null;

			var searches = new List<QuerySearch>();
			foreach (XmlNode search in element.SelectSingleNode("alerts").SelectSingleNode("searches").SelectNodes("li"))
			{
				var inner = search.SelectSingleNode("search");
				inner.RemoveChild(inner.SelectSingleNode("searchMaps"));

				var tempDoc = $@"<TD_Find_Lib.QuerySearch>
<saveable Class=""TD_Find_Lib.QuerySearch"">
{inner.InnerXml}
</saveable>
</TD_Find_Lib.QuerySearch>";
				var query = ScribeXmlFromString.LoadFromString<QuerySearch>(tempDoc);
				query.AllMaps();
				searches.Add(query);
			}
			stopwatch.Stop();
			Log.Message($"MapSearch: {stopwatch.ElapsedTicks}");
			stopwatch.Restart();


			//foreach (var xElement in element.Element("alerts").Element("searches").Elements("li"))
			//{
			//	xElement.Element("search").Element("searchMaps").Remove();
			//}

			//ScribeExtractor.ValueFromNode<CustomAlertsGameComp>(element.node, new CustomAlertsGameComp());
			//var val = ScribeXmlFromString.LoadFromString<CustomAlertsGameComp>($"<saveable>{element.OuterXml}</saveable>");
			////var val = ScribeExtractor.ValueFromNode<CustomAlertsGameComp>(element, new CustomAlertsGameComp());
			//return val?.alerts ?? new SearchAlertGroup();
			var newGroup = new SearchAlertGroup();
			newGroup.AddRange(searches.Select(x => new QuerySearchAlert(x, true)));
			return newGroup;
		}

		public static XmlDocument LoadSaveDocument(string saveNameNoExt)
		{
			var stopwatch = Stopwatch.StartNew();
			string path = GenFilePaths.FilePathForSavedGame(saveNameNoExt);

			using var fs = File.OpenRead(path);
			stopwatch.Stop();
			Log.Message($"OpenRead: {stopwatch.ElapsedTicks}");
			stopwatch.Restart();
			// Detect gzip by magic bytes 1F 8B
			bool isGzip = fs.ReadByte() == 0x1F && fs.ReadByte() == 0x8B;
			fs.Position = 0;

			Stream xmlStream = isGzip ? new GZipStream(fs, CompressionMode.Decompress) : fs;

			stopwatch.Stop();
			Log.Message($"DetectCompress: {stopwatch.ElapsedTicks}");
			stopwatch.Restart();

			var settings = new XmlReaderSettings
			{
				DtdProcessing = DtdProcessing.Prohibit,
				IgnoreComments = true,
				IgnoreWhitespace = true,
			};
			var doc = new XmlDocument();
			doc.Load(xmlStream);
			stopwatch.Stop();
			Log.Message($"XmlLoad: {stopwatch.ElapsedTicks}");
			stopwatch.Restart();
			return doc;
		}

		public static XmlNode FindGameComponentNode(XmlDocument doc, Type compType)
		{
			var test =  doc?
				.GetElementsByTagName("game")[0]?
				.SelectSingleNode("components")?
				.SelectNodes("li");
			return test.Cast<XmlNode>().FirstOrDefault(node =>
			{
				var att = node.Attributes["Class"].Value;
				return string.Equals(att, compType.FullName, StringComparison.Ordinal);
			});
		}

		private static bool TryLoadDeep<T>(XElement containerRoot, string label, out T value, params object[] ctorArgs)
				where T : class, IExposable
		{
			value = null;

			// Find the element we're interested in (e.g. <myThing>...</myThing>)
			XElement el = containerRoot.Name.LocalName == label
					? containerRoot
					: containerRoot.Descendants(label).FirstOrDefault();

			if (el == null)
				return false;

			// Wrap just that element in a root node for XmlDocument/Scribe
			// IMPORTANT: keep it as RimWorld-style XML, not XmlSerializer style.
			string xml = "<root>" + el.ToString(SaveOptions.DisableFormatting) + "</root>";

			var doc = new XmlDocument();
			doc.LoadXml(xml);

			// Get internal ScribeLoader methods via reflection (works across many RW versions)
			var scribeLoaderType = typeof(Scribe).Assembly.GetType("Verse.ScribeLoader");
			if (scribeLoaderType == null) return false;

			MethodInfo miInitLoading =
					scribeLoaderType.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
							.FirstOrDefault(m =>
									m.Name == "InitLoading" &&
									m.GetParameters().Length == 1 &&
									typeof(XmlNode).IsAssignableFrom(m.GetParameters()[0].ParameterType));

			MethodInfo miFinalizeLoading =
					scribeLoaderType.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
							.FirstOrDefault(m => m.Name == "FinalizeLoading" && m.GetParameters().Length == 0);

			if (miInitLoading == null || miFinalizeLoading == null)
				throw new MissingMethodException("Could not find ScribeLoader.InitLoading(XmlNode) / FinalizeLoading().");

			try
			{
				// Init Scribe to read from our <root>...
				miInitLoading.Invoke(null, new object[] { doc.DocumentElement });

				Scribe.mode = LoadSaveMode.LoadingVars;

				T tmp = null;

				// This expects the XML to be structured exactly like Scribe wrote it.
				// If the original save used ctorArgs, pass them here too. :contentReference[oaicite:1]{index=1}
				Scribe_Deep.Look(ref tmp, label, ctorArgs);

				value = tmp;
				return value != null;
			}
			finally
			{
				// Clean up Scribe state
				try { miFinalizeLoading.Invoke(null, null); } catch { /* ignore */ }
				Scribe.mode = LoadSaveMode.Inactive;
			}
		}
	}
}
