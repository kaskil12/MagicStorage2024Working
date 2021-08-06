using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MagicStorage.Components;
using MagicStorage.Items;
using MagicStorage.Sorting;
using MagicStorage.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.Localization;
using Terraria.Map;
using Terraria.ModLoader;
using Terraria.UI;

namespace MagicStorage
{
	public static class CraftingGUI
	{
		private const int RecipeButtonsAvailableChoice = 0;
		private const int RecipeButtonsBlacklistChoice = 3;
		private const int RecipeButtonsFavoritesChoice = 2;
		private const int padding = 4;
		private const int numColumns = 10;
		private const int numColumns2 = 7;
		private const float inventoryScale = 0.85f;
		private const float smallScale = 0.7f;
		private const int startMaxCraftTimer = 20;
		private const int startMaxRightClickTimer = 20;

		private static HashSet<int> threadCheckListFoundItems;
		private static Mod _checkListMod;
		private static volatile bool wasItemChecklistRetrieved;

		private static MouseState curMouse;
		private static MouseState oldMouse;

		private static UIPanel basePanel;
		private static float panelTop;
		private static float panelLeft;
		private static float panelWidth;
		private static float panelHeight;

		private static UIElement topBar;
		private static UI.UISearchBar searchBar;
		private static UIButtonChoice sortButtons;
		internal static UIButtonChoice recipeButtons;
		private static UIElement topBar2;
		private static UIButtonChoice filterButtons;

		private static UIText stationText;
		private static readonly UISlotZone stationZone = new(HoverStation, GetStation, inventoryScale / 1.55f);
		private static readonly UISlotZone recipeZone = new(HoverRecipe, GetRecipe, inventoryScale);

		private static readonly UIScrollbar scrollBar = new();
		private static int scrollBarFocus;
		private static int scrollBarFocusMouseStart;
		private static float scrollBarFocusPositionStart;
		private static readonly float scrollBarViewSize = 1f;
		private static float scrollBarMaxViewSize = 2f;

		private static readonly List<Item> items = new();
		private static readonly Dictionary<int, int> itemCounts = new();
		private static List<Recipe> recipes = new();
		private static List<bool> recipeAvailable = new();
		private static Recipe selectedRecipe;
		private static int numRows;
		private static int displayRows;
		private static bool slotFocus;

		private static readonly UIElement bottomBar = new();
		private static UIText capacityText;

		private static UIPanel recipePanel;

		private static float recipeTop;
		private static float recipeLeft;
		private static float recipeWidth;
		private static float recipeHeight;

		private static UIText recipePanelHeader;
		private static UIText ingredientText;
		private static readonly UISlotZone ingredientZone = new(HoverItem, GetIngredient, smallScale);
		private static readonly UISlotZone recipeHeaderZone = new(HoverHeader, GetHeader, smallScale);
		private static UIText reqObjText;
		private static UIText reqObjText2;
		private static UIText storedItemsText;

		private static readonly UISlotZone storageZone = new(HoverStorage, GetStorage, smallScale);
		private static int numRows2;
		private static int displayRows2;
		private static readonly List<Item> storageItems = new();
		private static readonly List<ItemData> blockStorageItems = new();

		private static readonly UIScrollbar scrollBar2 = new();
		private static readonly float scrollBar2ViewSize = 1f;
		private static float scrollBar2MaxViewSize = 2f;

		private static UITextPanel<LocalizedText> craftButton;
		private static readonly ModSearchBox modSearchBox = new(RefreshItems);

		private static Item result;
		private static readonly UISlotZone resultZone = new(HoverResult, GetResult, inventoryScale);
		private static int craftTimer;
		private static int maxCraftTimer = startMaxCraftTimer;
		private static int rightClickTimer;

		private static int maxRightClickTimer = startMaxRightClickTimer;

		private static readonly object threadLock = new();
		private static readonly object recipeLock = new();
		private static bool threadRunning;
		private static bool threadNeedsRestart;
		private static SortMode threadSortMode;
		private static FilterMode threadFilterMode;
		private static readonly List<Recipe> threadRecipes = new();
		private static readonly List<bool> threadRecipeAvailable = new();
		private static List<Recipe> nextRecipes = new();
		private static List<bool> nextRecipeAvailable = new();

		private static Dictionary<int, List<Recipe>> _productToRecipes;
		public static bool compoundCrafting;
		public static List<Item> compoundCraftSurplus = new();

		private static bool[] adjTiles = new bool[TileLoader.TileCount];

		private static bool adjWater;

		private static bool adjLava;

		private static bool adjHoney;

		private static bool zoneSnow;

		private static bool alchemyTable;

		public static bool MouseClicked => curMouse.LeftButton == ButtonState.Pressed && oldMouse.LeftButton == ButtonState.Released;

		public static bool RightMouseClicked => curMouse.RightButton == ButtonState.Pressed && oldMouse.RightButton == ButtonState.Released;

		public static void Initialize()
		{
			lock (recipeLock)
			{
				recipes = nextRecipes;
				recipeAvailable = nextRecipeAvailable;
			}

			InitLangStuff();
			float itemSlotWidth = TextureAssets.InventoryBack.Value.Width * inventoryScale;
			float itemSlotHeight = TextureAssets.InventoryBack.Value.Height * inventoryScale;
			float smallSlotWidth = TextureAssets.InventoryBack.Value.Width * smallScale;
			float smallSlotHeight = TextureAssets.InventoryBack.Value.Height * smallScale;

			panelTop = Main.instance.invBottom + 60;
			panelLeft = 20f;
			basePanel = new UIPanel();
			float innerPanelWidth = numColumns * (itemSlotWidth + padding) + 20f + padding;
			panelWidth = basePanel.PaddingLeft + innerPanelWidth + basePanel.PaddingRight;
			panelHeight = Main.screenHeight - panelTop - 40f;
			basePanel.Left.Set(panelLeft, 0f);
			basePanel.Top.Set(panelTop, 0f);
			basePanel.Width.Set(panelWidth, 0f);
			basePanel.Height.Set(panelHeight, 0f);
			basePanel.Recalculate();

			recipePanel = new UIPanel();
			recipeTop = panelTop;
			recipeLeft = panelLeft + panelWidth;
			recipeWidth = numColumns2 * (smallSlotWidth + padding) + 20f + padding;
			recipeWidth += recipePanel.PaddingLeft + recipePanel.PaddingRight;
			recipeHeight = panelHeight;
			recipePanel.Left.Set(recipeLeft, 0f);
			recipePanel.Top.Set(recipeTop, 0f);
			recipePanel.Width.Set(recipeWidth, 0f);
			recipePanel.Height.Set(recipeHeight, 0f);
			recipePanel.Recalculate();

			topBar = new UIElement();
			topBar.Width.Set(0f, 1f);
			topBar.Height.Set(32f, 0f);
			basePanel.Append(topBar);

			InitSortButtons();
			topBar.Append(sortButtons);
			float sortButtonsRight = sortButtons.GetDimensions().Width + padding;
			InitRecipeButtons();
			// TODO consider shortening the search box to fix the pretty 32f gap
			//float recipeButtonsLeft = sortButtonsRight + 32f + 3 * padding; // Original
			float recipeButtonsLeft = sortButtonsRight + 3 * padding;
			recipeButtons.Left.Set(recipeButtonsLeft, 0f);
			topBar.Append(recipeButtons);
			float recipeButtonsRight = recipeButtonsLeft + recipeButtons.GetDimensions().Width + padding;

			searchBar.Left.Set(recipeButtonsRight + padding, 0f);
			searchBar.Width.Set(-recipeButtonsRight - 2 * padding, 1f);
			searchBar.Height.Set(0f, 1f);
			topBar.Append(searchBar);

			topBar2 = new UIElement();
			topBar2.Width.Set(0f, 1f);
			topBar2.Height.Set(32f, 0f);
			topBar2.Top.Set(36f, 0f);
			basePanel.Append(topBar2);

			InitFilterButtons();
			float filterButtonsRight = filterButtons.GetDimensions().Width + padding;
			topBar2.Append(filterButtons);

			modSearchBox.Button.Left.Set(filterButtonsRight + padding, 0f);
			modSearchBox.Button.Width.Set(-filterButtonsRight - 2 * padding, 1f);
			modSearchBox.Button.Height.Set(0f, 1f);
			modSearchBox.Button.OverflowHidden = true;
			topBar2.Append(modSearchBox.Button);

			stationText.Top.Set(76f, 0f);
			basePanel.Append(stationText);

			stationZone.Width.Set(0f, 1f);
			stationZone.Top.Set(100f, 0f);
			// TODO this should be dynamic so that the number of station slots can be changed in a config
			stationZone.Height.Set(110f, 0f);
			stationZone.SetDimensions(15, 3);
			basePanel.Append(stationZone);

			recipeZone.Width.Set(0f, 1f);
			recipeZone.Top.Set(196f, 0f);
			recipeZone.Height.Set(-196f, 1f);
			basePanel.Append(recipeZone);

			numRows = (recipes.Count + numColumns - 1) / numColumns;
			displayRows = (int)recipeZone.GetDimensions().Height / ((int)itemSlotHeight + padding);
			recipeZone.SetDimensions(numColumns, displayRows);
			int noDisplayRows = numRows - displayRows;
			if (noDisplayRows < 0)
				noDisplayRows = 0;
			scrollBarMaxViewSize = 1 + noDisplayRows;
			scrollBar.Height.Set(displayRows * (itemSlotHeight + padding), 0f);
			scrollBar.Left.Set(-20f, 1f);
			scrollBar.SetView(scrollBarViewSize, scrollBarMaxViewSize);
			recipeZone.Append(scrollBar);

			bottomBar.Width.Set(0f, 1f);
			bottomBar.Height.Set(32f, 0f);
			bottomBar.Top.Set(-32f, 1f);
			basePanel.Append(bottomBar);

			capacityText.Left.Set(6f, 0f);
			capacityText.Top.Set(6f, 0f);
			TEStorageHeart heart = GetHeart();
			int numItems = 0;
			int capacity = 0;
			if (heart != null)
				foreach (TEAbstractStorageUnit abstractStorageUnit in heart.GetStorageUnits())
					if (abstractStorageUnit is TEStorageUnit storageUnit)
					{
						numItems += storageUnit.NumItems;
						capacity += storageUnit.Capacity;
					}

			capacityText.SetText(numItems + "/" + capacity + " Items");
			bottomBar.Append(capacityText);

			recipePanelHeader.Left.Set(60, 0f);
			recipePanel.Append(recipePanelHeader);

			ingredientText.Top.Set(30f, 0f);
			ingredientText.Left.Set(60, 0f);

			recipeHeaderZone.SetDimensions(1, 1);
			recipePanel.Append(recipeHeaderZone);

			recipePanel.Append(ingredientText);

			int itemsNeeded = selectedRecipe?.requiredItem.Count(item => !item.IsAir) ?? numColumns2 * 2;
			int recipeRows = itemsNeeded / numColumns2;
			int extraRow = itemsNeeded % numColumns2 != 0 ? 1 : 0;
			int totalRows = recipeRows + extraRow;
			if (totalRows < 2)
				totalRows = 2;
			const float ingredientZoneTop = 54f;
			float ingredientZoneHeight = 30f * totalRows;

			ingredientZone.SetDimensions(numColumns2, totalRows);
			ingredientZone.Top.Set(ingredientZoneTop, 0f);
			ingredientZone.Width.Set(0f, 1f);
			ingredientZone.Height.Set(ingredientZoneHeight, 0f);
			recipePanel.Append(ingredientZone);

			float reqObjTextTop = ingredientZoneTop + ingredientZoneHeight + 11 * totalRows;
			float reqObjText2Top = reqObjTextTop + 24;

			reqObjText.Top.Set(reqObjTextTop, 0f);
			recipePanel.Append(reqObjText);
			reqObjText2.Top.Set(reqObjText2Top, 0f);
			recipePanel.Append(reqObjText2);

			int reqObjText2Rows = reqObjText2.Text.Count(c => c == '\n') + 1;
			float storedItemsTextTop = reqObjText2Top + 30 * reqObjText2Rows;
			float storageZoneTop = storedItemsTextTop + 24;
			storedItemsText.Top.Set(storedItemsTextTop, 0f);
			recipePanel.Append(storedItemsText);
			storageZone.Top.Set(storageZoneTop, 0f);
			storageZone.Width.Set(0f, 1f);
			storageZone.Height.Set(-storageZoneTop - 36, 1f);
			recipePanel.Append(storageZone);
			numRows2 = (storageItems.Count + numColumns2 - 1) / numColumns2;
			displayRows2 = (int)storageZone.GetDimensions().Height / ((int)smallSlotHeight + padding);
			storageZone.SetDimensions(numColumns2, displayRows2);
			int noDisplayRows2 = numRows2 - displayRows2;
			if (noDisplayRows2 < 0)
				noDisplayRows2 = 0;
			scrollBar2MaxViewSize = 1 + noDisplayRows2;
			scrollBar2.Height.Set(displayRows2 * (smallSlotHeight + padding), 0f);
			scrollBar2.Left.Set(-20f, 1f);
			scrollBar2.SetView(scrollBar2ViewSize, scrollBar2MaxViewSize);
			storageZone.Append(scrollBar2);

			craftButton.Top.Set(-32f, 1f);
			craftButton.Width.Set(100f, 0f);
			craftButton.Height.Set(24f, 0f);
			craftButton.PaddingTop = 8f;
			craftButton.PaddingBottom = 8f;
			recipePanel.Append(craftButton);

			resultZone.SetDimensions(1, 1);
			resultZone.Left.Set(-itemSlotWidth, 1f);
			resultZone.Top.Set(-itemSlotHeight, 1f);
			resultZone.Width.Set(itemSlotWidth, 0f);
			resultZone.Height.Set(itemSlotHeight, 0f);
			recipePanel.Append(resultZone);
		}

		private static void InitLangStuff()
		{
			searchBar ??= new UI.UISearchBar(Language.GetText("Mods.MagicStorage.SearchName"), RefreshItems);
			stationText ??= new UIText(Language.GetText("Mods.MagicStorage.CraftingStations"));
			capacityText ??= new UIText("Items");
			recipePanelHeader ??= new UIText(Language.GetText("Mods.MagicStorage.SelectedRecipe"));
			ingredientText ??= new UIText(Language.GetText("Mods.MagicStorage.Ingredients"));
			reqObjText ??= new UIText(Language.GetText("LegacyInterface.22"));
			reqObjText2 ??= new UIText("");
			storedItemsText ??= new UIText(Language.GetText("Mods.MagicStorage.StoredItems"));
			craftButton ??= new UITextPanel<LocalizedText>(Language.GetText("LegacyMisc.72"));
			modSearchBox.InitLangStuff();
		}

		internal static void Unload()
		{
			sortButtons = null;
			filterButtons = null;
			recipeButtons = null;
			selectedRecipe = null;
		}

		private static void InitSortButtons()
		{
			sortButtons ??= GUIHelpers.MakeSortButtons(RefreshItems);
		}

		private static void InitRecipeButtons()
		{
			if (recipeButtons == null)
			{
				recipeButtons = new UIButtonChoice(RefreshItems, new[]
				{
					ModContent.Request<Texture2D>("Assets/RecipeAvailable"),
					ModContent.Request<Texture2D>("Assets/RecipeAll"),
					ModContent.Request<Texture2D>("Assets/FilterMisc"),
					ModContent.Request<Texture2D>("Assets/RecipeAll")
				}, new[]
				{
					Language.GetText("Mods.MagicStorage.RecipeAvailable"),
					Language.GetText("Mods.MagicStorage.RecipeAll"),
					Language.GetText("Mods.MagicStorage.ShowOnlyFavorited"),
					Language.GetText("Mods.MagicStorage.RecipeBlacklist")
				});
				if (MagicStorageConfig.UseConfigFilter)
					recipeButtons.Choice = MagicStorageConfig.ShowAllRecipes ? 1 : 0;
			}
		}

		private static void InitFilterButtons()
		{
			filterButtons ??= GUIHelpers.MakeFilterButtons(false, RefreshItems);
		}

		public static void Update(GameTime gameTime)
		{
			try
			{
				// TODO this needs to be in a better place. What's the hook for doing Keybinds?
				if (MagicStorage.IsItemKnownHotKey != null && MagicStorage.IsItemKnownHotKey.GetAssignedKeys().Count > 0 && MagicStorage.IsItemKnownHotKey.JustPressed && Main.HoverItem != null && !Main.HoverItem.IsAir)
				{
					string s = Main.HoverItem.Name + " is ";
					int t = Main.HoverItem.type;
					if (GetKnownItems().Contains(t))
					{
						s += "known";
						int sum = Main.LocalPlayer.GetModPlayer<StoragePlayer>().LatestAccessedStorage?.GetStoredItems().Where(x => x.type == t).Select(x => x.stack).DefaultIfEmpty().Sum() ?? 0;
						if (sum > 0)
							s += $" ({sum} in l.a.s.)";
					}
					else
					{
						s += "new";
					}

					Main.NewTextMultiline(s);
				}
			}
			catch (KeyNotFoundException)
			{
				// ignore
			}

			try
			{
				oldMouse = StorageGUI.oldMouse;
				curMouse = StorageGUI.curMouse;
				if (Main.playerInventory && Main.LocalPlayer.GetModPlayer<StoragePlayer>().ViewingStorage().X >= 0 && StoragePlayer.IsStorageCrafting())
				{
					if (curMouse.RightButton == ButtonState.Released)
						ResetSlotFocus();

					basePanel?.Update(gameTime);
					recipePanel?.Update(gameTime);
					UpdateRecipeText();
					UpdateScrollBar();
					UpdateCraftButton();
					modSearchBox.Update(curMouse, oldMouse);
				}
				else
				{
					scrollBarFocus = 0;
					craftTimer = 0;
					maxCraftTimer = startMaxCraftTimer;
					ResetSlotFocus();
				}
			}
			catch (Exception e)
			{
				Main.NewTextMultiline(e.ToString());
			}
		}

		public static void Draw()
		{
			try
			{
				Player player = Main.LocalPlayer;
				StoragePlayer modPlayer = player.GetModPlayer<StoragePlayer>();
				Initialize();
				if (Main.mouseX > panelLeft && Main.mouseX < recipeLeft + recipeWidth && Main.mouseY > panelTop && Main.mouseY < panelTop + panelHeight)
				{
					player.mouseInterface = true;
					player.cursorItemIconEnabled = false;
					InterfaceHelper.HideItemIconCache();
				}

				basePanel.Draw(Main.spriteBatch);
				recipePanel.Draw(Main.spriteBatch);
				Vector2 pos = recipeZone.GetDimensions().Position();
				if (threadRunning)
					Utils.DrawBorderString(Main.spriteBatch, "Loading", pos + new Vector2(8f, 8f), Color.White);
				stationZone.DrawText();
				recipeZone.DrawText();
				ingredientZone.DrawText();
				recipeHeaderZone.DrawText();
				storageZone.DrawText();
				resultZone.DrawText();
				sortButtons.DrawText();
				recipeButtons.DrawText();
				filterButtons.DrawText();
				DrawCraftButton();
			}
			catch (Exception e)
			{
				Main.NewTextMultiline(e.ToString());
			}
		}

		private static void DrawCraftButton()
		{
			Rectangle dim = InterfaceHelper.GetFullRectangle(craftButton);

			if (Main.netMode == NetmodeID.SinglePlayer)
				if (curMouse.X > dim.X && curMouse.X < dim.X + dim.Width && curMouse.Y > dim.Y && curMouse.Y < dim.Y + dim.Height)
					if (selectedRecipe != null && Main.mouseItem.IsAir && CanItemBeTakenForTest(selectedRecipe.createItem))
						Main.instance.MouseText(Language.GetText("Mods.MagicStorage.CraftTooltip").Value);
		}

		private static Item GetStation(int slot, ref int context)
		{
			Item[] stations = GetCraftingStations();
			if (stations != null && slot < stations.Length)
				return stations[slot];
			return new Item();
		}

		private static Item GetRecipe(int slot, ref int context)
		{
			if (threadRunning)
				return new Item();
			int index = slot + numColumns * (int)Math.Round(scrollBar.ViewPosition);
			Item item = index < recipes.Count ? recipes[index].createItem : new Item();
			if (!item.IsAir)
			{
				// TODO can this be nicer?
				if (recipes[index] == selectedRecipe)
					context = 6;
				if (!recipeAvailable[index])
					context = recipes[index] == selectedRecipe ? 4 : 3;
				if (Main.LocalPlayer.GetModPlayer<StoragePlayer>().FavoritedRecipes.Contains(item))
				{
					item = item.Clone();
					item.favorited = true;
				}

				if (!Main.LocalPlayer.GetModPlayer<StoragePlayer>().SeenRecipes.Contains(item))
				{
					item = item.Clone();
					item.newAndShiny = MagicStorageConfig.GlowNewItems;
				}
			}

			return item;
		}

		private static Item GetHeader(int slot, ref int context)
		{
			if (selectedRecipe == null)
				return new Item();

			Item item = selectedRecipe.createItem;
			if (item.IsAir)
			{
				int t = item.type;
				item = new Item();
				item.SetDefaults(t);
				item.stack = 0;
			}

			return item;
		}

		private static Item GetIngredient(int slot, ref int context)
		{
			if (selectedRecipe == null || slot >= selectedRecipe.requiredItem.Count)
				return new Item();

			Item item = selectedRecipe.requiredItem[slot].Clone();
			if (selectedRecipe.HasRecipeGroup(RecipeGroupID.Wood) && item.type == ItemID.Wood)
				item.SetNameOverride(Language.GetText("LegacyMisc.37").Value + " " + Lang.GetItemNameValue(ItemID.Wood));
			if (selectedRecipe.HasRecipeGroup(RecipeGroupID.Sand) && item.type == ItemID.SandBlock)
				item.SetNameOverride(Language.GetText("LegacyMisc.37").Value + " " + Lang.GetItemNameValue(ItemID.SandBlock));
			if (selectedRecipe.HasRecipeGroup(RecipeGroupID.IronBar) && item.type == ItemID.IronBar)
				item.SetNameOverride(Language.GetText("LegacyMisc.37").Value + " " + Lang.GetItemNameValue(ItemID.IronBar));
			if (selectedRecipe.HasRecipeGroup(RecipeGroupID.Fragment) && item.type == ItemID.FragmentSolar)
				item.SetNameOverride(Language.GetText("LegacyMisc.37").Value + " " + Language.GetText("LegacyMisc.51").Value);
			if (selectedRecipe.HasRecipeGroup(RecipeGroupID.PressurePlate) && item.type == ItemID.GrayPressurePlate)
				item.SetNameOverride(Language.GetText("LegacyMisc.37").Value + " " + Language.GetText("LegacyMisc.38").Value);
			if (ProcessGroupsForText(selectedRecipe, item.type, out string nameOverride))
				item.SetNameOverride(nameOverride);

			Item storageItem;
			int totalGroupStack = 0;
			lock (storageItems)
			{
				storageItem = storageItems.FirstOrDefault(i => i.type == item.type) ?? new Item();

				foreach (RecipeGroup rec in selectedRecipe.acceptedGroups.Select(index => RecipeGroup.recipeGroups[index]))
					if (rec.ValidItems.Contains(item.type))
						foreach (int type in rec.ValidItems)
							totalGroupStack += storageItems.Where(i => i.type == type).Sum(i => i.stack);
			}

			if (!item.IsAir)
			{
				if (storageItem.IsAir && totalGroupStack == 0)
					context = 3; // Unavailable - Red
				else if (storageItem.stack < item.stack && totalGroupStack < item.stack)
					context = 4; // Partially in stock - Pinkish
				// context == 0 - Available - Default Blue
				if (context != 0)
				{
					bool craftable = _productToRecipes.ContainsKey(item.type) && _productToRecipes[item.type].Any(recipe => IsAvailable(recipe) && AmountCraftable(recipe) > 0);
					if (craftable)
						context = 6; // Craftable - Light green
				}
			}

			return item;
		}

		private static bool ProcessGroupsForText(Recipe recipe, int type, out string theText)
		{
			foreach (int num in recipe.acceptedGroups)
				if (RecipeGroup.recipeGroups[num].ContainsItem(type))
				{
					theText = RecipeGroup.recipeGroups[num].GetText();
					return true;
				}

			theText = "";
			return false;
		}

		// Calculates how many times a recipe can be crafted using available items
		// TODO is this correct?
		private static int AmountCraftable(Recipe recipe)
		{
			if (!IsAvailable(recipe))
				return 0;
			int maxCraftable = int.MaxValue;

			if (RecursiveCraftIntegration.Enabled)
				recipe = RecursiveCraftIntegration.ApplyThreadCompoundRecipe(recipe);

			lock (items)
			{
				foreach (Item reqItem in recipe.requiredItem)
				{
					int total = 0;
					if (reqItem.type == ItemID.None)
						break;
					foreach (Item invItem in items)
						if (invItem.type == reqItem.type || RecipeGroupMatch(recipe, invItem.type, reqItem.type))
							total += invItem.stack;
					int craftable = total / reqItem.stack;
					if (craftable < maxCraftable)
						maxCraftable = craftable;
				}
			}

			return maxCraftable;
		}

		private static Item GetStorage(int slot, ref int context)
		{
			int index = slot + numColumns2 * (int)Math.Round(scrollBar2.ViewPosition);
			Item item = index < storageItems.Count ? storageItems[index] : new Item();
			lock (blockStorageItems)
			{
				if (blockStorageItems.Contains(new ItemData(item)))
					context = 3;
			}

			return item;
		}

		private static Item GetResult(int slot, ref int context) => slot == 0 && result != null ? result : new Item();

		private static void UpdateRecipeText()
		{
			if (selectedRecipe == null)
			{
				reqObjText2.SetText("");
				recipePanelHeader.SetText(Language.GetText("Mods.MagicStorageExtra.SelectedRecipe").Value);
			}
			else
			{
				bool isEmpty = true;
				string text = "";
				int rows = 0;

				void AddText(string s)
				{
					if (!isEmpty)
						text += ", ";

					if ((text.Length + s.Length) / 35 > rows)
					{
						text += "\n";
						++rows;
					}

					text += s;
					isEmpty = false;
				}

				foreach (int tile in selectedRecipe.requiredTile.TakeWhile(tile => tile != -1))
					AddText(Lang.GetMapObjectName(MapHelper.TileToLookup(tile, 0)));

				if (selectedRecipe.HasCondition(Recipe.Condition.NearWater))
					AddText(Language.GetTextValue("LegacyInterface.53"));

				if (selectedRecipe.HasCondition(Recipe.Condition.NearHoney))
					AddText(Language.GetTextValue("LegacyInterface.58"));

				if (selectedRecipe.HasCondition(Recipe.Condition.NearLava))
					AddText(Language.GetTextValue("LegacyInterface.56"));

				if (selectedRecipe.HasCondition(Recipe.Condition.InSnow))
					AddText(Language.GetTextValue("LegacyInterface.123"));

				if (isEmpty)
					text = Language.GetTextValue("LegacyInterface.23");

				reqObjText2.SetText(text);

				double dps = CompareDps.GetDps(selectedRecipe.createItem);
				string dpsText = dps >= 1d ? $"DPS = {dps:F}" : string.Empty;

				recipePanelHeader.SetText(dpsText);
			}
		}

		private static void UpdateScrollBar()
		{
			if (slotFocus)
			{
				scrollBarFocus = 0;
				return;
			}

			Rectangle dim = scrollBar.GetClippingRectangle(Main.spriteBatch);
			Vector2 boxPos = new(dim.X, dim.Y + dim.Height * (scrollBar.ViewPosition / scrollBarMaxViewSize));
			float boxWidth = 20f * Main.UIScale;
			float boxHeight = dim.Height * (scrollBarViewSize / scrollBarMaxViewSize);
			Rectangle dim2 = scrollBar2.GetClippingRectangle(Main.spriteBatch);
			Vector2 box2Pos = new(dim2.X, dim2.Y + dim2.Height * (scrollBar2.ViewPosition / scrollBar2MaxViewSize));
			float box2Height = dim2.Height * (scrollBar2ViewSize / scrollBar2MaxViewSize);
			if (scrollBarFocus > 0)
			{
				if (curMouse.LeftButton == ButtonState.Released)
				{
					scrollBarFocus = 0;
				}
				else
				{
					int difference = curMouse.Y - scrollBarFocusMouseStart;
					if (scrollBarFocus == 1)
						scrollBar.ViewPosition = scrollBarFocusPositionStart + difference / boxHeight;
					else if (scrollBarFocus == 2)
						scrollBar2.ViewPosition = scrollBarFocusPositionStart + difference / box2Height;
				}
			}
			else if (MouseClicked)
			{
				if (curMouse.X > boxPos.X && curMouse.X < boxPos.X + boxWidth && curMouse.Y > boxPos.Y - 3f && curMouse.Y < boxPos.Y + boxHeight + 4f)
				{
					scrollBarFocus = 1;
					scrollBarFocusMouseStart = curMouse.Y;
					scrollBarFocusPositionStart = scrollBar.ViewPosition;
				}
				else if (curMouse.X > box2Pos.X && curMouse.X < box2Pos.X + boxWidth && curMouse.Y > box2Pos.Y - 3f && curMouse.Y < box2Pos.Y + box2Height + 4f)
				{
					scrollBarFocus = 2;
					scrollBarFocusMouseStart = curMouse.Y;
					scrollBarFocusPositionStart = scrollBar2.ViewPosition;
				}
			}

			if (scrollBarFocus == 0)
			{
				int difference = oldMouse.ScrollWheelValue / 250 - curMouse.ScrollWheelValue / 250;
				scrollBar.ViewPosition += difference;
			}
		}

		private static void UpdateCraftButton()
		{
			Rectangle dim = InterfaceHelper.GetFullRectangle(craftButton);
			bool stillCrafting = false;
			if (curMouse.X > dim.X && curMouse.X < dim.X + dim.Width && curMouse.Y > dim.Y && curMouse.Y < dim.Y + dim.Height)
			{
				craftButton.BackgroundColor = new Color(73, 94, 171);
				if (RightMouseClicked && selectedRecipe != null && Main.mouseItem.IsAir)
				{
					Item item = selectedRecipe.createItem;
					if (CanItemBeTakenForTest(item))
					{
						int type = item.type;
						Item testItem = new();
						testItem.SetDefaults(type, true);
						MarkAsTestItem(testItem);
						Main.mouseItem = testItem;
						Main.LocalPlayer.GetModPlayer<StoragePlayer>().TestedRecipes.Add(selectedRecipe.createItem);
					}
				}
				else if (curMouse.LeftButton == ButtonState.Pressed && selectedRecipe != null && IsAvailable(selectedRecipe, false) && PassesBlock(selectedRecipe))
				{
					if (craftTimer <= 0)
					{
						craftTimer = maxCraftTimer;
						maxCraftTimer = maxCraftTimer * 3 / 4;
						if (maxCraftTimer <= 0)

							maxCraftTimer = 1;

						TryCraft();
						if (RecursiveCraftIntegration.Enabled)
							if (RecursiveCraftIntegration.UpdateRecipe(selectedRecipe))
								SetSelectedRecipe(selectedRecipe);
						RefreshItems();
						SoundEngine.PlaySound(SoundID.Grab);
					}

					craftTimer--;
					stillCrafting = true;
					if (Main.LocalPlayer.GetModPlayer<StoragePlayer>().AddToCraftedRecipes(selectedRecipe.createItem))
						RefreshItems();
				}
			}

			else
			{
				craftButton.BackgroundColor = new Color(63, 82, 151) * 0.7f;
			}

			if (selectedRecipe == null || !IsAvailable(selectedRecipe, false) || !PassesBlock(selectedRecipe))

				craftButton.BackgroundColor = new Color(30, 40, 100) * 0.7f;

			if (!stillCrafting)
			{
				craftTimer = 0;
				maxCraftTimer = startMaxCraftTimer;
			}
		}

		private static bool CanItemBeTakenForTest(Item item) => Main.netMode == NetmodeID.SinglePlayer && !item.consumable && (item.mana > 0 || item.CountsAsClass(DamageClass.Magic) || item.CountsAsClass(DamageClass.Ranged) || item.CountsAsClass(DamageClass.Throwing) || item.CountsAsClass(DamageClass.Melee) || item.headSlot >= 0 || item.bodySlot >= 0 || item.legSlot >= 0 || item.accessory || Main.projHook[item.shoot] || item.pick > 0 || item.axe > 0 || item.hammer > 0) && !item.CountsAsClass(DamageClass.Summon) && item.createTile < TileID.Dirt && item.createWall < 0 && !item.potion && item.fishingPole <= 1 && item.ammo == AmmoID.None && !Main.LocalPlayer.GetModPlayer<StoragePlayer>().TestedRecipes.Contains(item);

		public static void MarkAsTestItem(Item testItem)
		{
			testItem.value = 0;
			testItem.shopCustomPrice = 0;
			testItem.material = false;
			testItem.rare = -11;
			testItem.SetNameOverride(Lang.GetItemNameValue(testItem.type) + Language.GetTextValue("Mods.MagicStorageExtra.TestItemSuffix"));
		}

		public static bool IsTestItem(Item item) => item.Name.EndsWith(Language.GetTextValue("Mods.MagicStorageExtra.TestItemSuffix"));


		private static TEStorageHeart GetHeart() => Main.LocalPlayer.GetModPlayer<StoragePlayer>().GetStorageHeart();

		private static TECraftingAccess GetCraftingEntity() => Main.LocalPlayer.GetModPlayer<StoragePlayer>().GetCraftingAccess();

		private static Item[] GetCraftingStations() => GetCraftingEntity()?.stations;

		public static void RefreshItems()
		{
			StoragePlayer modPlayer = Main.LocalPlayer.GetModPlayer<StoragePlayer>();
			if (modPlayer.SeenRecipes.Count == 0)
				foreach (int item in GetKnownItems())
					modPlayer.SeenRecipes.Add(item);

			lock (items)
			{
				items.Clear();
				TEStorageHeart heart = GetHeart();
				if (heart == null)
					return;

				items.AddRange(ItemSorter.SortAndFilter(heart.GetStoredItems(), SortMode.Id, FilterMode.All, ModSearchBox.ModIndexAll, ""));
			}

			AnalyzeIngredients();
			InitLangStuff();
			InitSortButtons();
			InitRecipeButtons();
			InitFilterButtons();

			RefreshStorageItems();

			GetKnownItems(out HashSet<int> foundItems, out HashSet<int> hiddenRecipes, out HashSet<int> craftedRecipes, out HashSet<int> asKnownRecipes);
			foundItems.UnionWith(asKnownRecipes);

			var favoritesCopy = new HashSet<int>(modPlayer.FavoritedRecipes.Items.Select(x => x.type));

			EnsureProductToRecipesInited();
			threadRecipes.Clear();
			lock (threadLock)
			{
				threadNeedsRestart = true;
				threadSortMode = (SortMode)sortButtons.Choice;
				threadFilterMode = (FilterMode)filterButtons.Choice;
				threadCheckListFoundItems = foundItems;
				if (!threadRunning)
				{
					threadRunning = true;
					Task.Run(() => RefreshRecipes(hiddenRecipes, craftedRecipes, favoritesCopy));
				}
			}
		}

		public static HashSet<int> GetKnownItems()
		{
			GetKnownItems(out HashSet<int> a, out HashSet<int> b, out HashSet<int> c, out HashSet<int> d);
			a.UnionWith(b);
			a.UnionWith(c);
			a.UnionWith(d);
			return a;
		}

		private static void GetKnownItems(out HashSet<int> foundItems, out HashSet<int> hiddenRecipes, out HashSet<int> craftedRecipes, out HashSet<int> asKnownRecipes)
		{
			foundItems = new HashSet<int>(RetrieveFoundItemsCheckList());

			StoragePlayer modPlayer = Main.LocalPlayer.GetModPlayer<StoragePlayer>();
			hiddenRecipes = new HashSet<int>(modPlayer.HiddenRecipes.Select(x => x.type));
			craftedRecipes = new HashSet<int>(modPlayer.CraftedRecipes.Select(x => x.type));
			asKnownRecipes = new HashSet<int>(modPlayer.AsKnownRecipes.Items.Select(x => x.type));
		}

		private static IEnumerable<int> RetrieveFoundItemsCheckList()
		{
			_checkListMod ??= ModLoader.GetMod("ItemChecklist");

			object response = _checkListMod?.Call("RequestFoundItems");
			bool[] foundItems = response is bool[] found ? found : new bool[0];
			if (foundItems.Length > 0)
				wasItemChecklistRetrieved = true;
			return foundItems.Select((v, type) => new { WasFound = v, type }).Where(x => x.WasFound).Select(x => x.type);
		}

		private static void EnsureProductToRecipesInited()
		{
			if (_productToRecipes == null)
			{
				Recipe[] allRecipes = ItemSorter.GetRecipes(SortMode.Id, FilterMode.All, ModSearchBox.ModIndexAll, "").Where(x => x?.createItem != null && x.createItem.type > ItemID.None).ToArray();
				_productToRecipes = allRecipes.GroupBy(x => x.createItem.type).ToDictionary(x => x.Key, x => x.ToList());
			}
		}

		/// <summary>
		///     Checks all crafting tree until it finds already available ingredients
		/// </summary>
		private static bool IsKnownRecursively(Recipe recipe, HashSet<int> availableSet, HashSet<int> recursionTree, Dictionary<Recipe, bool> cache)
		{
			if (cache.TryGetValue(recipe, out bool v))
				return v;

			foreach (int tile in recipe.requiredTile.TakeWhile(tile => tile != -1))
			{
				if (!StorageWorld.TileToCreatingItem.TryGetValue(tile, out List<int> possibleItems))
					continue;

				if (!possibleItems.Any(x => IsKnownRecursively_CheckIngredient(x, availableSet, recursionTree, cache)))
				{
					cache[recipe] = false;
					return false;
				}
			}

			int ingredients = 0;
			foreach (int t in recipe.requiredItem.Select(item => item.type).Where(t => t > 0))
			{
				ingredients++;
				if (IsKnownRecursively_CheckIngredient(t, availableSet, recursionTree, cache))
					continue;
				if (IsKnownRecursively_CheckAcceptedGroupsForIngredient(recipe, availableSet, recursionTree, cache, t))
					continue;
				cache[recipe] = false;
				return false;
			}

			if (ingredients > 0)
			{
				cache[recipe] = true;
				return true;
			}

			cache[recipe] = false;
			return false;
		}

		private static bool IsKnownRecursively_CheckAcceptedGroupsForIngredient(Recipe recipe, HashSet<int> availableSet, HashSet<int> recursionTree, Dictionary<Recipe, bool> cache, int t)
		{
			foreach (RecipeGroup g in recipe.acceptedGroups.Select(j => RecipeGroup.recipeGroups[j]))
				if (g.ContainsItem(t))
					foreach (int groupItemType in g.ValidItems)
						if (groupItemType != t && IsKnownRecursively_CheckIngredient(groupItemType, availableSet, recursionTree, cache))
							return true;

			return false;
		}

		private static bool IsKnownRecursively_CheckIngredient(int t, HashSet<int> availableSet, HashSet<int> recursionTree, Dictionary<Recipe, bool> cache)
		{
			if (availableSet.Contains(t))
				return true;
			if (!recursionTree.Add(t))
				return false;
			try
			{
				if (!_productToRecipes.TryGetValue(t, out List<Recipe> ingredientRecipes))
					return false;
				if (ingredientRecipes.Count == 0 || ingredientRecipes.All(x => !IsKnownRecursively(x, availableSet, recursionTree, cache)))
					return false;
			}
			finally
			{
				recursionTree.Remove(t);
			}

			return true;
		}

		private static void RefreshRecipes(HashSet<int> hiddenRecipes, HashSet<int> craftedRecipes, HashSet<int> favorited)
		{
			while (true)
				try
				{
					SortMode sortMode;
					FilterMode filterMode;
					HashSet<int> foundItems;
					lock (threadLock)
					{
						threadNeedsRestart = false;
						sortMode = threadSortMode;
						filterMode = threadFilterMode;
						foundItems = threadCheckListFoundItems;
					}

					var availableItemsMutable = new HashSet<int>(hiddenRecipes.Concat(craftedRecipes).Concat(foundItems));

					var temp = new HashSet<int>();
					var tempCache = new Dictionary<Recipe, bool>();

					int modFilterIndex = modSearchBox.ModIndex;

					IEnumerable<Recipe> filteredRecipes = null;

					void DoFiltering()
					{
						filteredRecipes = ItemSorter.GetRecipes(sortMode, filterMode, modFilterIndex, searchBar.Text).Where(x => x != null)
							// show only blacklisted recipes only if choice = 2, otherwise show all other
							.Where(x => recipeButtons.Choice == RecipeButtonsBlacklistChoice == hiddenRecipes.Contains(x.createItem.type))
							// show only favorited items if selected
							.Where(x => recipeButtons.Choice != RecipeButtonsFavoritesChoice || favorited.Contains(x.createItem.type))
							// hard check if this item can be crafted from available items and their recursive products
							.Where(x => !wasItemChecklistRetrieved || IsKnownRecursively(x, availableItemsMutable, temp, tempCache))
							// favorites first
							.OrderBy(x => favorited.Contains(x.createItem.type) ? 0 : 1);

						threadRecipes.Clear();
						threadRecipeAvailable.Clear();
						try
						{
							if (recipeButtons.Choice == RecipeButtonsAvailableChoice)
							{
								threadRecipes.AddRange(filteredRecipes.Where(recipe => IsAvailable(recipe)));
								threadRecipeAvailable.AddRange(threadRecipes.Select(recipe => true));
							}
							else
							{
								threadRecipes.AddRange(filteredRecipes);
								threadRecipeAvailable.AddRange(threadRecipes.Select(recipe => IsAvailable(recipe)));
							}
						}
						catch (InvalidOperationException)
						{
						}
						catch (KeyNotFoundException)
						{
						}
					}

					if (RecursiveCraftIntegration.Enabled)
						RecursiveCraftIntegration.RecursiveRecipes();

					DoFiltering();

					// now if nothing found we disable filters one by one
					if (searchBar.Text.Length > 0)
					{
						if (threadRecipes.Count == 0 && hiddenRecipes.Count > 0)
						{
							// search hidden recipes too
							hiddenRecipes = new HashSet<int>();
							DoFiltering();
						}

						//if (threadRecipes.Count == 0 && filterMode != FilterMode.All) {
						//	// any category
						//	filterMode = FilterMode.All;
						//	DoFiltering();
						//}

						if (threadRecipes.Count == 0 && modFilterIndex != ModSearchBox.ModIndexAll)
						{
							// search all mods
							modFilterIndex = ModSearchBox.ModIndexAll;
							DoFiltering();
						}
					}

					// TODO is there a better way?
					void GuttedSetSelectedRecipe(Recipe recipe, int index)
					{
						Recipe compound = RecursiveCraftIntegration.ApplyCompoundRecipe(recipe);
						if (index != -1)
							threadRecipes[index] = compound;

						selectedRecipe = compound;
						RefreshStorageItems();
						lock (blockStorageItems)
						{
							blockStorageItems.Clear();
						}
					}

					lock (recipeLock)
					{
						if (RecursiveCraftIntegration.Enabled)
							if (selectedRecipe != null)
							{
								// If the selected recipe is compound, replace the overridden recipe with the compound one so it shows as selected in the UI
								if (RecursiveCraftIntegration.IsCompoundRecipe(selectedRecipe))
								{
									Recipe overridden = RecursiveCraftIntegration.GetOverriddenRecipe(selectedRecipe);
									int index = threadRecipes.IndexOf(overridden);
									if (index != -1 && threadRecipeAvailable[index])
										GuttedSetSelectedRecipe(overridden, index);
									else
										GuttedSetSelectedRecipe(overridden, index);
								}
								// If the selectedRecipe(which isn't compound) is uncraftable but is in the available list, this means it's compound version is craftable
								else if (!IsAvailable(selectedRecipe, false))
								{
									int index = threadRecipes.IndexOf(selectedRecipe);
									if (index != -1 && threadRecipeAvailable[index])
										GuttedSetSelectedRecipe(selectedRecipe, index);
								}
							}

						nextRecipes = new List<Recipe>();
						nextRecipeAvailable = new List<bool>();
						nextRecipes.AddRange(threadRecipes);
						nextRecipeAvailable.AddRange(threadRecipeAvailable);
					}

					lock (threadLock)
					{
						if (!threadNeedsRestart)
						{
							threadRunning = false;
							return;
						}
					}
				}
				catch (Exception e)
				{
					Main.NewTextMultiline(e.ToString());
				}
		}

		private static void AnalyzeIngredients()
		{
			Player player = Main.LocalPlayer;
			if (adjTiles.Length != player.adjTile.Length)
				Array.Resize(ref adjTiles, player.adjTile.Length);

			Array.Clear(adjTiles, 0, adjTiles.Length);
			adjWater = false;
			adjLava = false;
			adjHoney = false;
			zoneSnow = false;
			alchemyTable = false;

			lock (itemCounts)
			{
				itemCounts.Clear();
				foreach (Item item in items)
					if (itemCounts.ContainsKey(item.type))
						itemCounts[item.type] += item.stack;
					else
						itemCounts[item.type] = item.stack;
			}

			foreach (Item item in GetCraftingStations())
			{
				if (item.createTile >= TileID.Dirt)
				{
					adjTiles[item.createTile] = true;
					switch (item.createTile)
					{
						case TileID.GlassKiln:
						case TileID.Hellforge:
							adjTiles[TileID.Furnaces] = true;
							break;
						case TileID.AdamantiteForge:
							adjTiles[TileID.Furnaces] = true;
							adjTiles[TileID.Hellforge] = true;
							break;
						case TileID.MythrilAnvil:
							adjTiles[TileID.Anvils] = true;
							break;
						case TileID.BewitchingTable:
						case TileID.Tables2:
							adjTiles[TileID.Tables] = true;
							break;
						case TileID.AlchemyTable:
							adjTiles[TileID.Bottles] = true;
							adjTiles[TileID.Tables] = true;
							alchemyTable = true;
							break;
					}

					bool[] oldAdjTile = player.adjTile;
					bool oldAdjWater = adjWater;
					bool oldAdjLava = adjLava;
					bool oldAdjHoney = adjHoney;
					bool oldAlchemyTable = alchemyTable;
					player.adjTile = adjTiles;
					player.adjWater = false;
					player.adjLava = false;
					player.adjHoney = false;
					player.alchemyTable = false;
					TileLoader.AdjTiles(player, item.createTile);
					if (player.adjWater)
						adjWater = true;
					if (player.adjLava)
						adjLava = true;
					if (player.adjHoney)
						adjHoney = true;
					if (player.alchemyTable)
						alchemyTable = true;
					player.adjTile = oldAdjTile;
					player.adjWater = oldAdjWater;
					player.adjLava = oldAdjLava;
					player.adjHoney = oldAdjHoney;
					player.alchemyTable = oldAlchemyTable;
				}

				switch (item.type)
				{
					case ItemID.WaterBucket:
					case ItemID.BottomlessBucket:
						adjWater = true;
						break;
					case ItemID.LavaBucket:
						adjLava = true;
						break;
					case ItemID.HoneyBucket:
						adjHoney = true;
						break;
				}

				if (item.type == ModContent.ItemType<SnowBiomeEmulator>())
					zoneSnow = true;
			}

			adjTiles[ModContent.TileType<Components.CraftingAccess>()] = true;
		}

		public static bool IsAvailable(Recipe recipe, bool compound = true)
		{
			if (RecursiveCraftIntegration.Enabled && compound)
				recipe = RecursiveCraftIntegration.ApplyThreadCompoundRecipe(recipe);

			foreach (int tile in recipe.requiredTile)
			{
				if (tile == -1)
					break;
				if (!adjTiles[tile])
					return false;
			}

			lock (itemCounts)
			{
				foreach (Item ingredient in recipe.requiredItem)
				{
					if (ingredient.type == ItemID.None)
						break;
					int stack = ingredient.stack;
					bool useRecipeGroup = false;
					foreach (int type in itemCounts.Keys)
						if (RecipeGroupMatch(recipe, type, ingredient.type))
						{
							stack -= itemCounts[type];
							useRecipeGroup = true;
						}

					if (!useRecipeGroup && itemCounts.TryGetValue(ingredient.type, out int amount))
						stack -= amount;
					if (stack > 0)
						return false;
				}
			}

			lock (BlockRecipes.activeLock)
			{
				try
				{
					Player player = Main.LocalPlayer;
					for (int i = 0; i < adjTiles.Length; i++)
						if (adjTiles[i])
							player.adjTile[i] = true;
					if (adjWater)
						player.adjWater = true;
					if (adjLava)
						player.adjLava = true;
					if (adjHoney)
						player.adjHoney = true;
					if (alchemyTable)
						player.alchemyTable = true;
					if (zoneSnow)
						player.ZoneSnow = true;

					BlockRecipes.active = false;
					if (!RecipeLoader.RecipeAvailable(recipe))
						return false;
				}
				finally
				{
					BlockRecipes.active = true;
				}
			}

			return true;
		}

		private static bool PassesBlock(Recipe recipe)
		{
			foreach (Item ingredient in recipe.requiredItem)
			{
				if (ingredient.type == ItemID.None)
					break;
				int stack = ingredient.stack;
				bool useRecipeGroup = false;
				lock (storageItems)
				{
					lock (blockStorageItems)
					{
						foreach (Item item in storageItems)
						{
							ItemData data = new(item);
							if (!blockStorageItems.Contains(data) && RecipeGroupMatch(recipe, item.type, ingredient.type))
							{
								stack -= item.stack;
								useRecipeGroup = true;
							}
						}

						if (!useRecipeGroup)
							foreach (Item item in storageItems)
							{
								ItemData data = new(item);
								if (!blockStorageItems.Contains(data) && item.type == ingredient.type)
									stack -= item.stack;
							}
					}
				}

				if (stack > 0)
					return false;
			}


			return true;
		}

		private static void RefreshStorageItems()
		{
			lock (storageItems)
			{
				storageItems.Clear();
				result = null;
				if (selectedRecipe != null)
				{
					lock (items)
					{
						foreach (Item item in items)
						{
							foreach (Item reqItem in selectedRecipe.requiredItem)
							{
								if (reqItem.type == ItemID.None)
									break;
								if (item.type == reqItem.type || RecipeGroupMatch(selectedRecipe, item.type, reqItem.type))
									storageItems.Add(item);
							}

							if (item.type == selectedRecipe.createItem.type)
								result = item;
						}
					}

					if (result == null)
					{
						result = new Item();
						result.SetDefaults(selectedRecipe.createItem.type);
						result.stack = 0;
					}
				}
			}
		}

		private static bool RecipeGroupMatch(Recipe recipe, int inventoryType, int requiredType)
		{
			foreach (int num in recipe.acceptedGroups)
			{
				RecipeGroup recipeGroup = RecipeGroup.recipeGroups[num];
				if (recipeGroup.ContainsItem(inventoryType) && recipeGroup.ContainsItem(requiredType))
					return true;
			}

			return false;

			//return recipe.useWood(type1, type2) || recipe.useSand(type1, type2) || recipe.useIronBar(type1, type2) || recipe.useFragment(type1, type2) || recipe.AcceptedByItemGroups(type1, type2) || recipe.usePressurePlate(type1, type2);
		}

		private static void HoverStation(int slot, ref int hoverSlot)
		{
			TECraftingAccess ent = GetCraftingEntity();
			if (ent == null || slot >= ent.stations.Length)
				return;

			Player player = Main.LocalPlayer;
			if (MouseClicked)
			{
				bool changed = false;
				if (!ent.stations[slot].IsAir && ItemSlot.ShiftInUse)
				{
					Item station = player.GetItem(Main.myPlayer, DoWithdraw(slot), GetItemSettings.InventoryEntityToPlayerInventorySettings);
					if (!station.IsAir && Main.mouseItem.IsAir)
					{
						Main.mouseItem = station;
						station = new Item();
					}

					if (!station.IsAir && Main.mouseItem.type == station.type && Main.mouseItem.stack < Main.mouseItem.maxStack)
					{
						Main.mouseItem.stack += station.stack;
						station = new Item();
					}

					if (!station.IsAir)
						player.QuickSpawnClonedItem(station);
					changed = true;
				}
				else if (player.itemAnimation == 0 && player.itemTime == 0)
				{
					int oldType = Main.mouseItem.type;
					int oldStack = Main.mouseItem.stack;
					Main.mouseItem = DoStationSwap(Main.mouseItem, slot);
					if (oldType != Main.mouseItem.type || oldStack != Main.mouseItem.stack)
						changed = true;
				}

				if (changed)
				{
					RefreshItems();
					SoundEngine.PlaySound(7);
				}
			}

			hoverSlot = slot;
		}

		private static void HoverRecipe(int slot, ref int hoverSlot)
		{
			int visualSlot = slot;
			slot += numColumns * (int)Math.Round(scrollBar.ViewPosition);
			if (slot < recipes.Count)
			{
				Recipe recipe = recipes[slot];
				StoragePlayer storagePlayer = Main.LocalPlayer.GetModPlayer<StoragePlayer>();
				if (MouseClicked)
				{
					if (Main.keyState.IsKeyDown(Keys.LeftAlt))
					{
						if (!storagePlayer.FavoritedRecipes.Add(recipe.createItem))
							storagePlayer.FavoritedRecipes.Remove(recipe.createItem);
					}
					else if (Main.keyState.IsKeyDown(Keys.LeftControl))
					{
						if (recipeButtons.Choice == RecipeButtonsBlacklistChoice)
						{
							if (storagePlayer.RemoveFromHiddenRecipes(recipe.createItem))
								RefreshItems();
						}
						else
						{
							if (storagePlayer.AddToHiddenRecipes(recipe.createItem))
								RefreshItems();
						}
					}
					else
					{
						SetSelectedRecipe(recipe);
					}
				}
				else if (RightMouseClicked)
				{
					if (recipe == selectedRecipe || recipeButtons.Choice != RecipeButtonsAvailableChoice)
					{
						if (recipeButtons.Choice == RecipeButtonsAvailableChoice)
						{
							storagePlayer.AsKnownRecipes.Add(recipe.createItem);
							RefreshItems();
						}
						else
						{
							storagePlayer.AsKnownRecipes.Remove(recipe.createItem);
						}
					}
				}

				hoverSlot = visualSlot;
			}
		}

		private static void SetSelectedRecipe(Recipe recipe)
		{
			if (recipe != null)
				Main.LocalPlayer.GetModPlayer<StoragePlayer>().SeenRecipes.Add(recipe.createItem);

			if (RecursiveCraftIntegration.Enabled)
			{
				int index;
				if (RecursiveCraftIntegration.IsCompoundRecipe(selectedRecipe) && selectedRecipe != recipe)
				{
					Recipe overridden = RecursiveCraftIntegration.GetOverriddenRecipe(selectedRecipe);
					if (overridden != recipe)
					{
						index = recipes.IndexOf(selectedRecipe);
						if (index != -1)
							recipes[index] = overridden;
					}
				}

				index = recipes.IndexOf(recipe);
				if (index != -1)
				{
					recipe = RecursiveCraftIntegration.ApplyCompoundRecipe(recipe);
					recipes[index] = recipe;
				}
			}

			selectedRecipe = recipe;
			RefreshStorageItems();
			lock (blockStorageItems)
			{
				blockStorageItems.Clear();
			}
		}

		private static void HoverHeader(int slot, ref int hoverSlot)
		{
			hoverSlot = slot;
		}

		private static void HoverItem(int slot, ref int hoverSlot)
		{
			if (selectedRecipe == null)
			{
				hoverSlot = slot;
				return;
			}

			int visualSlot = slot;
			slot += numColumns2 * (int)Math.Round(scrollBar2.ViewPosition);
			int count = selectedRecipe.requiredItem.Select((item, i) => new { item, i }).FirstOrDefault(x => x.item.type == ItemID.None)?.i + 1 ?? selectedRecipe.requiredItem.Count;

			if (slot >= count)
				return;

			// select ingredient recipe by right clicking
			if (RightMouseClicked)
			{
				Item item = selectedRecipe.requiredItem[slot];
				EnsureProductToRecipesInited();
				if (_productToRecipes.TryGetValue(item.type, out List<Recipe> itemRecipes))
				{
					HashSet<int> knownItems = GetKnownItems();

					var recursionTree = new HashSet<int>();
					var cache = new Dictionary<Recipe, bool>();

					Recipe selected = null;

					foreach (Recipe r in itemRecipes.Where(x => IsKnownRecursively(x, knownItems, recursionTree, cache)))
					{
						selected ??= r;
						if (IsAvailable(r))
						{
							selected = r;
							break;
						}
					}

					if (selected != null)
						SetSelectedRecipe(selected);
				}
			}

			hoverSlot = visualSlot;
		}

		private static void HoverStorage(int slot, ref int hoverSlot)
		{
			int visualSlot = slot;
			slot += numColumns2 * (int)Math.Round(scrollBar2.ViewPosition);
			if (slot >= storageItems.Count)
				return;

			Item item = storageItems[slot];
			item.newAndShiny = false;
			if (MouseClicked)
			{
				ItemData data = new(item);
				lock (blockStorageItems)
				{
					if (blockStorageItems.Contains(data))
						blockStorageItems.Remove(data);
					else
						blockStorageItems.Add(data);
				}
			}

			hoverSlot = visualSlot;
		}

		private static void HoverResult(int slot, ref int hoverSlot)
		{
			if (slot != 0)
				return;

			if (Main.mouseItem.IsAir && result != null && !result.IsAir)
				result.newAndShiny = false;

			Player player = Main.LocalPlayer;
			if (MouseClicked)
			{
				bool changed = false;
				if (!Main.mouseItem.IsAir && player.itemAnimation == 0 && player.itemTime == 0 && result != null && Main.mouseItem.type == result.type)
				{
					if (TryDepositResult(Main.mouseItem))
						changed = true;
				}
				else if (Main.mouseItem.IsAir && result != null && !result.IsAir)
				{
					if (Main.keyState.IsKeyDown(Keys.LeftAlt))
					{
						result.favorited = !result.favorited;
					}
					else
					{
						Item toWithdraw = result.Clone();
						if (toWithdraw.stack > toWithdraw.maxStack)
							toWithdraw.stack = toWithdraw.maxStack;
						Main.mouseItem = DoWithdrawResult(toWithdraw, ItemSlot.ShiftInUse);
						if (ItemSlot.ShiftInUse)
							Main.mouseItem = player.GetItem(Main.myPlayer, Main.mouseItem, GetItemSettings.InventoryEntityToPlayerInventorySettings);
						changed = true;
					}
				}

				if (changed)
				{
					RefreshItems();
					SoundEngine.PlaySound(SoundID.Grab);
				}
			}

			if (RightMouseClicked && result != null && !result.IsAir && (Main.mouseItem.IsAir || ItemData.Matches(Main.mouseItem, result) && Main.mouseItem.stack < Main.mouseItem.maxStack))
				slotFocus = true;

			hoverSlot = slot;

			if (slotFocus)
				SlotFocusLogic();
		}

		private static void SlotFocusLogic()
		{
			if (result == null || result.IsAir || !Main.mouseItem.IsAir && (!ItemData.Matches(Main.mouseItem, result) || Main.mouseItem.stack >= Main.mouseItem.maxStack))
			{
				ResetSlotFocus();
			}
			else
			{
				if (rightClickTimer <= 0)
				{
					rightClickTimer = maxRightClickTimer;
					maxRightClickTimer = maxRightClickTimer * 3 / 4;
					if (maxRightClickTimer <= 0)
						maxRightClickTimer = 1;
					Item toWithdraw = result.Clone();
					toWithdraw.stack = 1;
					Item withdrawn = DoWithdrawResult(toWithdraw);
					if (Main.mouseItem.IsAir)
						Main.mouseItem = withdrawn;
					else
						Main.mouseItem.stack += withdrawn.stack;
					SoundEngine.PlaySound(SoundID.MenuTick);
					RefreshItems();
				}

				rightClickTimer--;
			}
		}

		private static void ResetSlotFocus()
		{
			slotFocus = false;
			rightClickTimer = 0;
			maxRightClickTimer = startMaxRightClickTimer;
		}

		private static Item DoWithdraw(int slot)
		{
			TECraftingAccess access = GetCraftingEntity();
			if (Main.netMode == NetmodeID.SinglePlayer)
			{
				Item result = access.TryWithdrawStation(slot);
				RefreshItems();
				return result;
			}

			NetHelper.SendWithdrawStation(access.ID, slot);
			return new Item();
		}

		private static Item DoStationSwap(Item item, int slot)
		{
			TECraftingAccess access = GetCraftingEntity();
			if (Main.netMode == NetmodeID.SinglePlayer)
			{
				Item result = access.DoStationSwap(item, slot);
				RefreshItems();
				return result;
			}

			NetHelper.SendStationSlotClick(access.ID, item, slot);
			return new Item();
		}

		private static void TryCraft()
		{
			List<Item> availableItems;
			var toWithdraw = new List<Item>();

			lock (storageItems)
			lock (blockStorageItems)
			{
				availableItems = storageItems.Where(item => !blockStorageItems.Contains(new ItemData(item))).Select(item => item.Clone()).ToList();
			}

			foreach (Item reqItem in selectedRecipe.requiredItem)
			{
				if (reqItem.type == ItemID.None)
					break;
				int stack = reqItem.stack;

				RecipeLoader.ConsumeItem(selectedRecipe, reqItem.type, ref stack);

				if (stack <= 0)
					continue;

				foreach (Item tryItem in availableItems)
					if (reqItem.type == tryItem.type || RecipeGroupMatch(selectedRecipe, tryItem.type, reqItem.type))
					{
						if (tryItem.stack > stack)
						{
							Item temp = tryItem.Clone();
							temp.stack = stack;
							toWithdraw.Add(temp);
							tryItem.stack -= stack;
							stack = 0;
						}
						else
						{
							toWithdraw.Add(tryItem.Clone());
							stack -= tryItem.stack;
							tryItem.stack = 0;
							tryItem.type = ItemID.None;
						}
					}
			}

			Item resultItem = selectedRecipe.createItem.Clone();
			resultItem.Prefix(-1);
			var resultItems = new List<Item> { resultItem };

			bool isCompound = RecursiveCraftIntegration.Enabled && RecursiveCraftIntegration.IsCompoundRecipe(selectedRecipe);
			if (isCompound)
			{
				compoundCrafting = true;
				compoundCraftSurplus.Clear();
			}

			RecipeLoader.OnCraft(resultItem, selectedRecipe);
			ItemLoader.OnCreate(resultItem, new RecipeCreationContext { recipe = selectedRecipe });

			if (isCompound)
			{
				compoundCrafting = false;
				resultItems.AddRange(compoundCraftSurplus);
			}

			if (Main.netMode == NetmodeID.SinglePlayer)
				foreach (Item item in DoCraft(GetHeart(), toWithdraw, resultItems))
					Main.LocalPlayer.QuickSpawnClonedItem(item, item.stack);
			else if (Main.netMode == NetmodeID.MultiplayerClient)
				NetHelper.SendCraftRequest(GetHeart().ID, toWithdraw, resultItems);
		}

		internal static List<Item> DoCraft(TEStorageHeart heart, List<Item> toWithdraw, List<Item> results)
		{
			var items = new List<Item>();
			foreach (Item tryWithdraw in toWithdraw)
			{
				Item withdrawn = heart.TryWithdraw(tryWithdraw, false);
				if (!withdrawn.IsAir)
					items.Add(withdrawn);
				if (withdrawn.stack < tryWithdraw.stack)
				{
					for (int k = 0; k < items.Count; k++)
					{
						heart.DepositItem(items[k]);
						if (items[k].IsAir)
						{
							items.RemoveAt(k);
							k--;
						}
					}

					return items;
				}
			}

			items.Clear();
			foreach (Item result in results)
			{
				heart.DepositItem(result);
				if (!result.IsAir)
					items.Add(result);
			}

			return items;
		}

		private static bool TryDepositResult(Item item)
		{
			int oldStack = item.stack;
			DoDepositResult(item);
			return oldStack != item.stack;
		}

		private static void DoDepositResult(Item item)
		{
			TEStorageHeart heart = GetHeart();
			if (Main.netMode == NetmodeID.SinglePlayer)
			{
				heart.DepositItem(item);
			}
			else
			{
				NetHelper.SendDeposit(heart.ID, item);
				item.SetDefaults(0, true);
			}
		}

		private static Item DoWithdrawResult(Item item, bool toInventory = false)
		{
			TEStorageHeart heart = GetHeart();
			if (Main.netMode == NetmodeID.SinglePlayer)
				return heart.TryWithdraw(item, false);
			NetHelper.SendWithdraw(heart.ID, item, toInventory);
			return new Item();
		}
	}
}
