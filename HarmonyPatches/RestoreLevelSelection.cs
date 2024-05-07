﻿using BetterSongList.UI;
using BetterSongList.Util;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using static SelectLevelCategoryViewController;

namespace BetterSongList.HarmonyPatches {
	[HarmonyPatch(typeof(LevelFilteringNavigationController), nameof(LevelFilteringNavigationController.ShowPacksInSecondChildController))]
	static class PackPreselect {
		public static BeatmapLevelPack restoredPack = null;

		public static void LoadPackFromCollectionName() {
			if(restoredPack?.shortPackName == Config.Instance.LastPack)
				return;

			if(Config.Instance.LastPack == null) {
				restoredPack = null;
				return;
			}

			restoredPack = PlaylistsUtil.GetPack(Config.Instance.LastPack);
		}

		[HarmonyPriority(int.MinValue)]
		static void Prefix(ref string ____levelPackIdToBeSelectedAfterPresent) {
			if(____levelPackIdToBeSelectedAfterPresent != null)
				return;

			LoadPackFromCollectionName();
			____levelPackIdToBeSelectedAfterPresent = restoredPack?.packID;
		}
	}

	// Animation might get stuck when switching category if it hasn't finished.
	[HarmonyPatch(typeof(LevelFilteringNavigationController), nameof(LevelFilteringNavigationController.HandleSelectLevelCategoryViewControllerDidSelectLevelCategory))]
	static class PackPreselectAnimationFix {
		static void Postfix(LevelFilteringNavigationController __instance) {
			__instance._annotatedBeatmapLevelCollectionsViewController._annotatedBeatmapLevelCollectionsGridView._animator.DespawnAllActiveTweens();
		}
	}

	[HarmonyPatch(typeof(LevelSelectionFlowCoordinator), nameof(LevelSelectionFlowCoordinator.DidActivate))]
	static class LevelSelectionFlowCoordinator_DidActivate {

		private static BeatmapLevelsModel beatmapLevelsModel = BeatSaberMarkupLanguage.BeatSaberUI.MainFlowCoordinator._beatmapLevelsModel;

		static void Prefix(ref LevelSelectionFlowCoordinator.State ____startState, bool addedToHierarchy) {
			if(!addedToHierarchy)
				return;

			if(____startState != null) {
#if DEBUG
				Plugin.Log.Warn("Not restoring last state because we are starting off from somewhere!");
#endif
				FilterUI.SetFilter(null, false, false);
				return;
			}

			if(!Enum.TryParse(Config.Instance.LastCategory, out LevelCategory restoreCategory))
				restoreCategory = LevelCategory.None;

			if(Config.Instance.LastSong == null || !beatmapLevelsModel._loadedBeatmapLevels.TryGetValue(Config.Instance.LastSong, out var lastSelectedLevel))
				lastSelectedLevel = null;

			PackPreselect.LoadPackFromCollectionName();

			var pack = PackPreselect.restoredPack;

			if(restoreCategory == LevelCategory.All || restoreCategory == LevelCategory.Favorites)
				pack = SongCore.Loader.CustomLevelsPack;

			____startState = new LevelSelectionFlowCoordinator.State(
				restoreCategory, 
				pack, 
				new BeatmapKey(), 
				lastSelectedLevel);
		}
	}
}
