using System;
using System.Reflection;
using System.Linq;
using Verse;
using RimWorld;
using UnityEngine;
using HarmonyLib;

namespace Custom_Alerts
{
	[StaticConstructorOnStartup]
	public static class CustomAlertsModInit
	{
		static CustomAlertsModInit()
		{
			var harmony = new Harmony("Memegoddess.CustomAlerts");
			harmony.PatchAll();
		}
	}

	public class Mod : Verse.Mod
	{
		public Mod(ModContentPack content) : base(content)
		{
		}
	}

	[HarmonyPatch(typeof(TD_Find_Lib.ThingQueryDesignation), "ResolveRef")]
	public static class Patch_ThingQueryDesignation_ResolveRef
	{
		public static bool Prefix()
		{
			if (!UnityData.IsInMainThread)
			{
				return false;
			}
			return true;
		}
	}
}