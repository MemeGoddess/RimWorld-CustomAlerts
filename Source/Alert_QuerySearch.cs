using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using RimWorld;
using UnityEngine;
using TD_Find_Lib;

namespace Custom_Alerts
{
	public class QuerySearchAlert: IExposable, IQuerySearch
	{
		public QuerySearch search;
		public QuerySearch Search => search;
		public Alert_QuerySearch alert;

		//For ExposeData
		public QuerySearchAlert() : base()
		{
			alert = new Alert_QuerySearch(this);
		}

		public QuerySearchAlert(QuerySearch search, bool enabled = true) : base()
		{
			this.search = search;
			this.search.active = true;

			alert = new (this);
			alert.enabled = enabled;
		}

		public void ExposeData()
		{
			Scribe_Deep.Look(ref search, "search");

			alert.ExposeData();
		}
		public void SetPriority(AlertPriority p)
		{
			alert.defaultPriority = p;
		}
	}


	public enum CompareType { Greater, Equal, Less }
	public class Alert_QuerySearch : Alert
	{
		public QuerySearchAlert searchAlert;

		public bool enabled = true;
		public int secondsBeforeAlert;
		public int countToAlert;
		public CompareType compareType;

		const int MaxItems = 16;
		int lastTickInactive;

		public static bool enableAll = true;

		public static readonly string transferTags = SearchAlertTransfer.TransferTag + "," + TD_Find_Lib.Settings.StorageTransferTag;

		public Alert_QuerySearch()
		{
			//The vanilla alert added to AllAlerts will be constructed but never be active with null filter
		}

		public Alert_QuerySearch(QuerySearchAlert searchAlert) : this()
		{
			this.searchAlert = searchAlert;
		}

		public void ExposeData()
		{
			Scribe_Values.Look(ref defaultPriority, "alertPriority");
			Scribe_Values.Look(ref secondsBeforeAlert, "ticksToShowAlert");
			Scribe_Values.Look(ref countToAlert, "countToAlert");
			Scribe_Values.Look(ref compareType, "countComp");
			Scribe_Values.Look(ref enabled, "enabled");
		}
		
		//protected but using publicized assembly
		//protected override Color BGColor
		public override Color BGColor
		{
			get
			{
				if (defaultPriority != AlertPriority.Critical) return base.BGColor;
				float i = Pulser.PulseBrightness(Alert_Critical.PulseFreq, Alert_Critical.PulseAmpCritical);
				return new Color(i, i, i) * Color.red;
			}
		}

		public bool HasCountInName => searchAlert?.search?.name?.Contains("%n") == true;

		public override string GetLabel()
		{
			if (searchAlert?.search == null) return string.Empty;
			int count = searchAlert.search.result?.allThingsCount ?? 0;
			return searchAlert.search.name.Replace("%n", count.ToString());
		}

		public override AlertReport GetReport()
		{
			if (!enabled || searchAlert?.search == null || !enableAll)	//Alert_Find auto-added as an Alert subclass, exists but never displays anything
				return AlertReport.Inactive;

			var result = SearchResult();
			if (result?.allThings == null)
				return AlertReport.Inactive;

			int count = result.allThingsCount;
			bool active = compareType switch
			{
				CompareType.Greater => count > countToAlert,
				CompareType.Equal => count == countToAlert,
				CompareType.Less => count < countToAlert,
				_ => false
			};

			if (!active)
				lastTickInactive = Find.TickManager.TicksGame;
			else if (Find.TickManager.TicksGame - lastTickInactive >= secondsBeforeAlert * GenTicks.TicksPerRealSecond)
			{
				if (count == 0)
					return AlertReport.Active;
				return AlertReport.CulpritsAre(result.allThings.Where(t => t != null && !t.Destroyed).Take(MaxItems).ToList());
			}
			return AlertReport.Inactive;
		}

		public override TaggedString GetExplanation()
		{
			if (searchAlert?.search == null) return string.Empty;

			var result = SearchResult();
			if (result?.allThings == null) return string.Empty;

			StringBuilder stringBuilder = new StringBuilder();

			//Name
			stringBuilder.Append(GetLabel() + searchAlert.search.GetMapNameSuffix());

			// - Count
			if (!HasCountInName)
				stringBuilder.AppendLine(" - " + ThingListDrawer.LabelCountThings(result));
			else 
				stringBuilder.AppendLine("");

			stringBuilder.AppendLine("");

			//Item list
			foreach (Thing thing in result.allThings.Take(MaxItems))
				if (thing != null)
					stringBuilder.AppendLine("   " + thing.Label);

			if (result.allThings.Count > MaxItems)
				stringBuilder.AppendLine("TD.Maximum0Displayed".Translate(MaxItems));
			stringBuilder.AppendLine("");

			//Help
			stringBuilder.AppendLine("TD.ClickToOpen".Translate());

			return stringBuilder.ToString().TrimEndNewlines();
		}

		private static HashSet<string> loggedErrors = new();
		private SearchResult SearchResult()
		{
			if (searchAlert?.search == null) return null;

			try
			{
				searchAlert.search.RemakeList();
				return searchAlert.search.result;
			}
			catch (Exception ex)
			{
				string key = searchAlert.search.name ?? "unnamed";
				if (loggedErrors.Add(key))
				{
					Verse.Log.Error($"[Custom Alerts] Exception in alert '{key}': {ex}");
				}
				return searchAlert.search.result;
			}
		}

		public override void OnClick()
		{
			if (searchAlert?.search == null) return;

			if (Event.current.button == 1)
			{
				List<FloatMenuOption> options = SearchStorage.ExportSearchOptions(searchAlert.search, transferTags);

				options.Add(new FloatMenuOption("TD.OpenManager".Translate(), () => MainButtonWorker_ToggleAlertsWindow.Open()));
				options.Add(new FloatMenuOption("TD.Inspect".Translate(), () => Inspect(searchAlert)));

				Find.WindowStack.Add(new FloatMenu(options));
			}
			else if (Event.current.shift)
				Inspect(searchAlert);
			else
				base.OnClick();
		}

		public static void Inspect(QuerySearchAlert searchAlert)
		{
			MainButtonWorker_ToggleAlertsWindow.Open().PopUpEditor(searchAlert);
			Find.WindowStack.Add(new ResultThingListWindow(searchAlert.search));
		}
	}
}
