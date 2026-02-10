using BlueprintTotalsTooltip.TotalsTipUtilities;
using BlueprintTotalsTooltip.CameraChangeDetection;
using BlueprintTotalsTooltip.SelectorChangeNotifiers;
using BlueprintTotalsTooltip.FrameChangeNotifiers;
using BlueprintTotalsTooltip.LTChangeNotifiers;
using BlueprintTotalsTooltip.TotalsTipSettingsUtilities;
using System.Collections.Generic;
using UnityEngine;
using RimWorld.Planet;
using RimWorld;
using Verse;
using System;
using Verse.Sound;
using static BlueprintTotalsTooltip.ModSettings_BlueprintTotal;

namespace BlueprintTotalsTooltip
{
	public class TotalsTooltipDrawer
	{
		private bool highlightEnabled;

		private RectDimensionPosition tipXPos;
		private RectDimensionPosition tipYPos;

		private readonly float highlightMargin = 5f;
		private readonly float listElementsMargin = 3f;
		private readonly float listElementsHeight = 29f;
		private readonly float xOffsetFromContainer = 10f;

		private Mod_BlueprintTotal modInstance;
		private CameraChangeDetector cameraChangeDetector;

		private bool zoomWasValid = false;
        public static ZoomVisibleTrackingMode zoomForVisibleTracking => ModSettings_BlueprintTotal.zoomForVisibleTracking;
        public bool ZoomIsValid
        {
            get
            {
				return (int)Find.CameraDriver.CurrentZoom <= (int)zoomForVisibleTracking;
            }
        }

        public ConstructibleTotalsTracker Tracker { get; }
		public static bool NoConstructablesSelected
		{
			get
			{
				foreach (var obj in Find.Selector.SelectedObjects)
				{
					if (obj is IConstructible)
					{
						return false;
					}
				}

				return true;
			}
		}
		public static TotalsTooltipDrawer Instance;

		public TotalsTooltipDrawer(Mod_BlueprintTotal mod)
		{
			modInstance = mod;
			Tracker = new ConstructibleTotalsTracker();
			SelectionChangeNotifierData.RegisterMethod(OnSelectionChange);
			FrameChangeNotifierData.RegisterMethod(OnSelectionChange);
			TooltipToggleAdder.RegisterMethod(OnPlaySettingChange);
			LTAddNotifier.RegisterMethod(OnThingAdded);
			LTRemoveNotifier.RegisterMethod(OnThingRemove);
			FrameWorkedOnDetector.RegisterMethod(Tracker.FrameBeingBuilt);
			cameraChangeDetector = new CameraChangeDetector();
			cameraChangeDetector.RegisterMethod(OnCameraChange);
		}

		public void ResolveSettings()
		{
			highlightEnabled = ModSettings_BlueprintTotal.HighlightOpacity != 0f;
			tipXPos = (RectDimensionPosition)ModSettings_BlueprintTotal.TipXPosition;
			tipYPos = (RectDimensionPosition)ModSettings_BlueprintTotal.TipYPosition;
		}

		#region callbacks
		public void OnPlaySettingChange()
		{
			if (ModSettings_BlueprintTotal.ShouldDrawTooltip && WorldRendererUtility.DrawingMap && Find.CurrentMap != null)
			{
				if (NoConstructablesSelected && ZoomIsValid && ModSettings_BlueprintTotal.TrackingVisible)
				{ 
					Tracker.TrackVisibleConstructibles();
				}
				else 
				{
                    Tracker.ClearTracked();
					Tracker.TrackSelectedConstructibles();
				}
			}
		}

		public void OnSelectionChange()
		{
			if (ModSettings_BlueprintTotal.ShouldDrawTooltip && WorldRendererUtility.DrawingMap)
			{
				if (NoConstructablesSelected && ModSettings_BlueprintTotal.TrackingVisible)
					Tracker.TrackVisibleConstructibles();
				else
					Tracker.TrackSelectedConstructibles();
			}
		}

		public void OnCameraChange()
		{
			if (ModSettings_BlueprintTotal.ShouldDrawTooltip && ModSettings_BlueprintTotal.TrackingVisible)
			{
				if (NoConstructablesSelected)
				{
					if (ZoomIsValid)
					{
						Tracker.TrackVisibleConstructibles();
					}
					else if (zoomWasValid)
					{
						Tracker.ClearTracked();
					}
				}
				zoomWasValid = ZoomIsValid;
			}
		}

		public void OnThingAdded(Thing thing)
		{
			if (NoConstructablesSelected && ModSettings_BlueprintTotal.TrackingVisible && ModSettings_BlueprintTotal.ShouldDrawTooltip && WorldRendererUtility.DrawingMap)
				if (thing is IConstructible)
				{
					Tracker.TryTrackConstructible(thing);
				}
		}

		public void OnThingRemove(Thing thing)
		{
			if (NoConstructablesSelected && ModSettings_BlueprintTotal.TrackingVisible && ModSettings_BlueprintTotal.ShouldDrawTooltip && WorldRendererUtility.DrawingMap)
			{
				if (thing is IConstructible)
				{
					Tracker.TryUntrackConstructible(thing);
				}
			}
		}
		#endregion callbacks

		public void OnGUI()
		{
			CheckDrawSettingToggle();
			if (Find.CurrentMap != null && WorldRendererUtility.DrawingMap)
			{
				cameraChangeDetector.OnGUI();
				if (ModSettings_BlueprintTotal.ShouldDrawTooltip && Tracker.NumberTracked > 0)
				{
					if (highlightEnabled && NoConstructablesSelected) Tracker.HighlightTracked(ModSettings_BlueprintTotal.HighlightOpacity, highlightMargin);
					DrawToolTip();
				}
			}
		}
        public static void CheckDrawSettingToggle()
        {
            if (toggleTipDraw != null && toggleTipDraw.KeyDownEvent)
            {
                ModSettings_BlueprintTotal.ShouldDrawTooltip = !ModSettings_BlueprintTotal.ShouldDrawTooltip;
                TooltipToggleAdder.NotifyPlaySettingToggled();
                if (ModSettings_BlueprintTotal.ShouldDrawTooltip)
                    SoundDefOf.Checkbox_TurnedOn.PlayOneShotOnCamera(null);
                else
                    SoundDefOf.Checkbox_TurnedOff.PlayOneShotOnCamera(null);
            }
        }

        public static KeyBindingDef toggleTipDraw;
        public static void ResolveReferences()
        {
            toggleTipDraw = BluePrintTotalDefOf.ToggleTracking;
        }

        public void DrawToolTip()
		{
			List<ThingDefCount> trackedRequirements = Tracker.TotalCosts;
			float maxCountWidth = (trackedRequirements.Count > 0) ? Text.CalcSize(trackedRequirements[0].Count.ToString()).x : 0f;
			float workLeftWidth = Text.CalcSize(Tracker.WorkLeft.ToString()).x;
			float toolTipWidth = Mathf.Max(maxCountWidth, workLeftWidth);
			toolTipWidth += (listElementsMargin * 2 + xOffsetFromContainer * 2) + listElementsHeight;
			float toolTipHeight = (trackedRequirements.Count + 1) * listElementsHeight;
			toolTipHeight += listElementsMargin * 2;
			Rect tooltipRect = new Rect(0f, 0f, toolTipWidth, toolTipHeight);
			PositionTipRect(ref tooltipRect);
			if (ModSettings_BlueprintTotal.ClampTipToScreen)
			{
				tooltipRect = tooltipRect.ClampRectInRect(new Rect(0, 0, UI.screenWidth, UI.screenHeight).ContractedBy(ModSettings_BlueprintTotal.TooltipClampMargin));
			}
			Rect innerTipRect = tooltipRect.ContractedBy(listElementsMargin).WidthContractedBy(xOffsetFromContainer);
			int indexOffset = 0;
			if (!NoConstructablesSelected)
			{
				DrawTrackingModeIndicator(innerTipRect, indexOffset);
				indexOffset = 1;
			}
			for (int i = 0; i < trackedRequirements.Count; i++)
			{
				ThingDefCount count = trackedRequirements[i];
				DrawRequirementRow(count, innerTipRect, i + indexOffset);
			}
			if (Tracker.WorkLeft != 0)
			{
				DrawWorkLeftRow(innerTipRect, trackedRequirements.Count + indexOffset);
			}
		}

		private void PositionTipRect(ref Rect tooltipRect)
		{
			Rect containingRect = Tracker.ContainingRect;
			float xPos = TipPosSettingsHandler.GetDimensionFromSetting(containingRect.xMin, containingRect.xMax, tooltipRect.width, tipXPos);
			float yPos = TipPosSettingsHandler.GetDimensionFromSetting(containingRect.yMin, containingRect.yMax, tooltipRect.height, tipYPos);
			tooltipRect.position = new Vector2(xPos, yPos);
		}

		private void DrawTrackingModeIndicator(Rect toolTipRect, int posInList)
		{
			Rect rowRect = new Rect(toolTipRect.x, toolTipRect.y + posInList * listElementsHeight, toolTipRect.width, listElementsHeight);
			Rect iconRect = new Rect(rowRect.x, rowRect.y, listElementsHeight, listElementsHeight).ContractedBy(1.5f);
			RectUtility.DrawBracketsAroundRect(iconRect);
		}

		private void DrawRequirementRow(ThingDefCount count, Rect toolTipRect, int posInList)
		{
			Rect rowRect = new Rect(toolTipRect.x, toolTipRect.y + posInList * listElementsHeight, toolTipRect.width, listElementsHeight);
			Rect iconRect = new Rect(rowRect.x, rowRect.y, listElementsHeight, listElementsHeight).ContractedBy(1.5f);
			Widgets.ThingIcon(iconRect, count.ThingDef);
			Rect labelRect = new Rect(rowRect.x + listElementsHeight, rowRect.y, rowRect.width - listElementsHeight, rowRect.height);
			Text.Anchor = TextAnchor.MiddleLeft;
			int difference = (ModSettings_BlueprintTotal.CountInStorage) ? Find.CurrentMap.GetCountInStorageDifference(count) : Find.CurrentMap.GetCountOnMapDifference(count, ModSettings_BlueprintTotal.CountForbidden);
			if (difference > 0) GUI.color = Color.red;
			Widgets.Label(labelRect, count.Count.ToString());
			GUI.color = Color.white;
			Text.Anchor = TextAnchor.UpperLeft;
			if (ModSettings_BlueprintTotal.ShowRowToolTips) DoRowTooltip(rowRect, count, difference);
		}

		private void DoRowTooltip(Rect tooltipRegion, ThingDefCount count, int difference)
		{
			int present = -difference + count.Count;
			string tipLabel = (ModSettings_BlueprintTotal.CountInStorage) ? "ReqRowTip_Storage".Translate(count.Count, present) : "ReqRowTip_All".Translate(count.Count, present);
			TooltipHandler.TipRegion(tooltipRegion, new TipSignal(tipLabel));
		}

		private void DrawWorkLeftRow(Rect toolTipRect, int posInList)
		{
			Rect rowRect = new Rect(toolTipRect.x, toolTipRect.y + posInList * listElementsHeight, toolTipRect.width, listElementsHeight);
			Rect iconRect = new Rect(rowRect.x, rowRect.y, listElementsHeight, listElementsHeight).ContractedBy(1.5f);
			GUI.DrawTexture(iconRect, AssetLoader.workLeftTexture);
			Rect labelRect = new Rect(rowRect.x + listElementsHeight, rowRect.y, rowRect.width - listElementsHeight, rowRect.height);
			Text.Anchor = TextAnchor.MiddleLeft;
			string workLeftAsString = Tracker.WorkLeft.ToString();
			Widgets.Label(labelRect, workLeftAsString);
			DoWorkLeftTooltip(rowRect, workLeftAsString);
			Text.Anchor = TextAnchor.UpperLeft;
		}

		private void DoWorkLeftTooltip(Rect tipRegion, string workLeftAsString)
		{
			TooltipHandler.TipRegion(tipRegion, new TipSignal("WorkLeftTip".Translate(workLeftAsString)));
		}
	}
}