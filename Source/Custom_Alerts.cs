using System.Reflection;
using System.Linq;
using HarmonyLib;
using Verse;
using RimWorld;
using UnityEngine;

namespace Custom_Alerts
{
	public class Mod : Verse.Mod
	{
		public Mod(ModContentPack content) : base(content)
		{
			var harmony = new Harmony("MemeGoddess.CustomAlerts");
			harmony.PatchAll();
		}
	}
}