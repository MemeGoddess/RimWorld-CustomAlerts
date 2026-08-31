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
	// GameComponent to hold the SearchAlerts
	// SearchAlert holds a Alert_Find
	// Alert_Find have to be inserted into the game's AllAlerts
	class CustomAlertsGameComp : GameComponent
	{
		public SearchAlertGroup alerts = new();

		public CustomAlertsGameComp(Game g):base() { }

		public override void ExposeData()
		{
			Scribe_Deep.Look(ref alerts, "alerts");
			if (Scribe.mode == LoadSaveMode.PostLoadInit)
			{
				if (alerts == null)
					alerts = new();

				foreach (QuerySearchAlert searchAlert in alerts)
				{
					if (searchAlert?.search != null)
					{
						SanitizeQueries(searchAlert.search);
					}
					LiveAlerts.AddAlert(searchAlert);
				}
			}
		}

		private static void SanitizeQueries(QuerySearch search)
		{
			if (search?.Children?.queries == null) return;
			var selNameField = typeof(TD_Find_Lib.ThingQueryDesignation).GetField("selName", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
			if (selNameField == null) return;

			foreach (var query in search.Children.queries)
			{
				if (query is TD_Find_Lib.ThingQueryDesignation designation)
				{
					string val = selNameField.GetValue(designation) as string;
					if (val == "RimWorld.Designator_PlantsHarvestWood")
					{
						selNameField.SetValue(designation, "HarvestPlant");
						Verse.Log.Message($"[Custom Alerts] Auto-repaired invalid query designation selName in '{search.name}' to 'HarvestPlant'.");
					}
				}
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
}
