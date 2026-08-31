using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using UnityEngine;
using RimWorld;
using TD_Find_Lib;

namespace Custom_Alerts
{
	public class AlertsManagerWindow : Window
	{
		SearchAlertListDrawer alertsDrawer;
		CustomAlertsGameComp comp;
		public override Vector2 InitialSize => new Vector2(850, 500f);

		//protected but using publicized assembly
		//protected override void SetInitialSizeAndPosition()
		public override void SetInitialSizeAndPosition()
		{
			base.SetInitialSizeAndPosition();
			windowRect.x = Window.StandardMargin;
			windowRect.y = UI.screenHeight - MainButtonDef.ButtonHeight - this.windowRect.height - Window.StandardMargin;
		}

		public AlertsManagerWindow()
		{
			preventCameraMotion = false;
			draggable = true;
			resizeable = true;
			closeOnAccept = false;
			doCloseX = true;
		}

		public override void PreOpen()
		{
			base.PreOpen();
			comp = Current.Game.GetComponent<CustomAlertsGameComp>();

			alertsDrawer = new(comp.alerts, this);
		}


		private Vector2 scrollPosition = Vector2.zero;
		private float scrollViewHeight;
		private string title = "TD.CustomAlerts".Translate() + ":";
		public override void DoWindowContents(Rect inRect)
		{
			//Title
			Text.Font = GameFont.Medium;
			Rect titleRect = inRect.TopPartPixels(Text.LineHeight).AtZero();
			Widgets.Label(titleRect, title);
			Text.Font = GameFont.Small;

			// Add alert row
			Rect addRect = inRect.LeftHalf().BottomPartPixels(Text.LineHeight);
			WidgetRow addRow = new(addRect.x, addRect.y);


			if (addRow.ButtonIcon(FindTex.GreyPlus))
				PopUpCreateAlert();

			addRow.ButtonChooseImportSearch(comp.AddAlert, SearchAlertTransfer.TransferTag, QuerySearch.CloneArgs.use);

			addRow.ButtonChooseImportSearchGroup(comp.AddAlerts, SearchAlertTransfer.TransferTag, QuerySearch.CloneArgs.use);

			addRow.ButtonChooseExportSearchGroup(comp.alerts.AsSearchGroup(), SearchAlertTransfer.TransferTag);

			addRow.ButtonOpenLibrary();

			//Check off
			Rect enableRect = inRect.RightHalf().BottomPartPixels(Text.LineHeight);
			Widgets.CheckboxLabeled(enableRect, "TD.EnableAlerts".Translate(), ref Alert_QuerySearch.enableAll);



			//Scrolling!
			inRect.yMin = titleRect.yMax + Listing.DefaultGap;
			inRect.yMax = enableRect.yMin - Listing.DefaultGap;

			Listing_StandardIndent listing = new();
			Rect viewRect = new Rect(0f, 0f, inRect.width - 16f, scrollViewHeight);
			listing.BeginScrollView(inRect, ref scrollPosition, viewRect);

			alertsDrawer.DrawQuerySearchList(listing);

			listing.EndScrollView(ref scrollViewHeight);
		}

		public void PopUpCreateAlert()
		{
			Find.WindowStack.Add(new Dialog_Name("TD.NewAlert".Translate(), n =>
			{
				QuerySearch search = new () { name = n };
				search.SetSearchAllMaps();
				QuerySearchAlert searchAlert = new(search, false);
				comp.AddAlert(searchAlert);

				PopUpEditor(searchAlert);
			},
			"TD.NameForNewAlert".Translate(),
			name => comp.HasSavedAlert(name)));
		}

		public void PopUpEditor(QuerySearchAlert searchAlert)
		{
			var editor = new AlertEditorWindow(searchAlert);

			Find.WindowStack.Add(editor);
			editor.windowRect.x = Window.StandardMargin;
			editor.windowRect.y = windowRect.yMin / 3;
			editor.windowRect.yMax = windowRect.yMin;
		}
	}

	public class SearchAlertListDrawer : SearchGroupDrawerBase<SearchAlertGroup, QuerySearchAlert>
	{
		CustomAlertsGameComp comp = Current.Game.GetComponent<CustomAlertsGameComp>();
		AlertsManagerWindow parent;

		public SearchAlertListDrawer(SearchAlertGroup list, AlertsManagerWindow window) : base(list)
		{
			parent = window;
		}

		public override string Name => "TD.ActiveSearches".Translate();

		public override void DrawRowButtons(WidgetRow row, QuerySearchAlert searchAlert, int i)
		{
			if (row.ButtonIcon(FindTex.Edit, "TD.EditThisSearch".Translate()))
				parent.PopUpEditor(searchAlert);

			if (row.ButtonIcon(TexButton.Rename))
				comp.RenameAlert(searchAlert);

			if (row.ButtonIcon(FindTex.Trash))
			{
				if (Event.current.shift)
					comp.RemoveAlert(searchAlert);
				else
					Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
						"TD.Delete0".Translate(searchAlert.search.name), () => comp.RemoveAlert(searchAlert), true));
			}

			SearchStorage.ButtonChooseExportSearch(row, searchAlert.search, SearchAlertTransfer.TransferTag);
		}

		public override void DrawExtraRowRect(Rect rowRect, QuerySearchAlert searchAlert, int i)
		{
			WidgetRow row = new WidgetRow(rowRect.xMax, rowRect.y, UIDirection.LeftThenDown);

			//Check off
			row.Checkbox(ref searchAlert.alert.enabled);

			//Show when (backwards right to left O_o)
			Rect textRect = row.GetRect(36); textRect.height -= 4; textRect.width -= 4;
			string countBuffer = searchAlert.alert.countToAlert.ToString();
			Widgets.TextFieldNumeric(textRect, ref searchAlert.alert.countToAlert, ref countBuffer, 0, 999999);

			if (row.ButtonIcon(TexFor(searchAlert.alert.compareType)))
				searchAlert.alert.compareType = (CompareType)((int)(searchAlert.alert.compareType + 1) % 3);
			row.Label("TD.ShowWhen".Translate());


			//Seconds until
			textRect = row.GetRect(36); textRect.height -= 4; textRect.width -= 4;
			string secondsBuffer = searchAlert.alert.secondsBeforeAlert.ToString();
			Widgets.TextFieldNumeric(textRect, ref searchAlert.alert.secondsBeforeAlert, ref secondsBuffer, 0, 999999);

			TooltipHandler.TipRegion(textRect, "TD.Tip1000SecondsInARimworldDay".Translate());
			row.Label("TD.SecondsUntilShown".Translate());


			// Critical Alert
			bool crit = searchAlert.alert.defaultPriority == AlertPriority.Critical;
			if (row.ToggleableIconChanged(ref crit, CustomAlertTex.PassionMajorIcon, "TD.CriticalAlert".Translate()))
			{
				searchAlert.SetPriority(crit ? AlertPriority.Critical : AlertPriority.Medium);
			}

			if (searchAlert.alert.Active)
				Widgets.DrawHighlightSelected(rowRect);
		}

		public static Texture2D TexFor(CompareType compareType) =>
			compareType == CompareType.Equal ? CustomAlertTex.Equal :
			compareType == CompareType.Greater ? CustomAlertTex.GreaterThan :
			CustomAlertTex.LessThan;
	}


	public class MainButtonWorker_ToggleAlertsWindow : MainButtonWorker
	{
		public static void OpenWith(SearchGroup searches)
		{
			Open();

			Current.Game.GetComponent<CustomAlertsGameComp>().AddAlerts(searches);
		}

		public static void OpenWith(QuerySearch search)
		{
			Open();

			Current.Game.GetComponent<CustomAlertsGameComp>().AddAlert(search);
		}
		public static AlertsManagerWindow Open()
		{
			if (Find.WindowStack.WindowOfType<AlertsManagerWindow>() is AlertsManagerWindow w)
			{
				Find.WindowStack.Notify_ClickedInsideWindow(w);
				return w;
			}
			else
			{
				AlertsManagerWindow window = new AlertsManagerWindow();
				Find.WindowStack.Add(window);
				return window;
			}
		}
		public static void Toggle()
		{
			if (Find.WindowStack.WindowOfType<AlertsManagerWindow>() is AlertsManagerWindow w)
				w.Close();
			else
				Open();
		}

		public override void Activate()
		{
			Toggle();
		}
	}

	[StaticConstructorOnStartup]
	public class SearchAlertTransfer : ISearchReceiver, ISearchGroupReceiver
	{
		static SearchAlertTransfer()
		{
			SearchTransfer.Register(new SearchAlertTransfer());
		}

		public static string TransferTag = "Custom Alert";//notranslate
		public string Source => TransferTag;
		public string ReceiveName => "TD.MakeCustomAlert".Translate();
		public QuerySearch.CloneArgs CloneArgs => QuerySearch.CloneArgs.use;

		public bool CanReceive() => Current.Game?.GetComponent<CustomAlertsGameComp>() != null;
		public void Receive(QuerySearch search) => MainButtonWorker_ToggleAlertsWindow.OpenWith(search);
		public void Receive(SearchGroup searches) => MainButtonWorker_ToggleAlertsWindow.OpenWith(searches);
	}
}
