using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Buffs;
using StardewValley.GameData.BigCraftables;
using StardewValley.GameData.Characters;
using StardewValley.GameData.Shops;
using StardewValley.Menus;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using Object = StardewValley.Object;

namespace FarmHelpers;

public class ModEntry : Mod
{
    public static readonly string HELPER_ID = "FarmHelpers";
    public static readonly string HELPER_NAME = "小帮手";
    public static readonly string HELPER_BELL_ID = "HelperBell";
    public static readonly string HELPER_BELL_NAME = "帮手铃铛";
    public static readonly string SCARECROW_ID = "EZScareCrow";
    public static readonly string SCARECROW_NAME = "强力稻草人";
    public static ModConfig? Config;
    public readonly List<Job> Jobs = new();
    public readonly List<FarmHelper> Helpers = new();
    public readonly Dictionary<Job, bool> VisitedJobs = new(new JobEqComparer());

    public bool Mutex = false, ShouldFollow = true;

    public override void Entry(IModHelper helper)
    {
        Config = helper.ReadConfig<ModConfig>();
        helper.Events.Input.ButtonPressed += OnButtonPressed;
        helper.Events.Content.AssetRequested += OnAssetRequested;
        helper.Events.GameLoop.GameLaunched += OnGameLaunched;
        helper.Events.GameLoop.DayStarted += (s, e) =>
        {
            if (!Game1.IsMasterGame)
            {
                Game1.addHUDMessage(new HUDMessage("您不是农场主持者，农场主功能包已禁用！", HUDMessage.achievement_type));
                return;
            }

            // Scare crows
            if (Config.IsScareCrow)
            {
                var farm = Game1.getFarm();
                foreach (KeyValuePair<Vector2, TerrainFeature> v in farm.terrainFeatures.Pairs)
                    if (v.Value is HoeDirt { crop: not null })
                    {
                        farm.Objects.Remove(v.Key);
                        // add deluxe scarecrows
                        var scarecrow = new Object(v.Key, SCARECROW_ID);
                        farm.Objects.Add(v.Key, scarecrow);
                    }
            }
        };
        helper.Events.GameLoop.DayEnding += (s, e) => ClearHelpers();
        helper.Events.GameLoop.ReturnedToTitle += (s, e) => ClearHelpers();
        helper.Events.GameLoop.OneSecondUpdateTicking += (s, e) =>
        {
            if (!Context.IsWorldReady)
                return;
            // Scare crows
            var farm = Game1.getFarm();
            foreach (var v in farm.Objects.Pairs)
            {
                if (!v.Value.QualifiedItemId.Contains(SCARECROW_ID)) continue;
                if (Game1.IsMasterGame)
                {
                    if (Config.IsScareCrow) v.Value.isTemporarilyInvisible = true;
                    var dirt = farm.terrainFeatures.ContainsKey(v.Key) ? farm.terrainFeatures[v.Key] as HoeDirt : null;
                    if (!Config.IsScareCrow || dirt is { crop: null }) farm.Objects.Remove(v.Key);
                }
                else
                {
                    v.Value.isTemporarilyInvisible = true;
                }
            }

            if (!Game1.IsMasterGame)
                return;
            // Helpers stuff
            CheckHireHelpers();
            if (Helpers.Count == 0) return;
            if (Game1.IsMasterGame)
                foreach (var location in Game1.locations)
                    FindJobs(location);

            foreach (var location in Helper.Multiplayer.GetActiveLocations()) FindJobs(location);
            if (Jobs.Count == 0 && VisitedJobs.Count > 0) VisitedJobs.Clear();
        };
        helper.Events.GameLoop.UpdateTicked += (s, e) =>
        {
            if (!Context.IsWorldReady)
                return;
            foreach (var helper in Helpers) helper.UpdateTicked();
        };
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        var configMenu = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
        if (configMenu is null || Config is null) return;
        // register mod
        configMenu.Register(
            ModManifest,
            () => Config = new ModConfig(),
            () => Helper.WriteConfig(Config)
        );
        configMenu.AddSectionTitle(
            ModManifest,
            () => "收取农作物"
        );
        configMenu.AddBoolOption(
            ModManifest,
            name: () => "是否需要帮忙收取成熟农作物？(总开关）",
            tooltip: () => "关闭后将不会将收取成熟农作物。",
            getValue: () => Config.IsCollectCrop,
            setValue: value => Config.IsCollectCrop = value
        );
        configMenu.AddBoolOption(
            ModManifest,
            name: () => "是否需要帮忙收取在鹈鹕镇农场的成熟农作物？",
            tooltip: () => "关闭后将不会将收取在鹈鹕镇农场的成熟农作物。",
            getValue: () => Config.IsCollectFarm,
            setValue: value => Config.IsCollectFarm = value
        );
        configMenu.AddBoolOption(
            ModManifest,
            name: () => "是否需要帮忙收取在姜岛的成熟农作物？",
            tooltip: () => "关闭后将不会将收取在姜岛的成熟农作物。",
            getValue: () => Config.IsCollectIslandWest,
            setValue: value => Config.IsCollectIslandWest = value
        );
        configMenu.AddBoolOption(
            ModManifest,
            name: () => "是否需要帮忙收取其它地方的成熟农作物？",
            tooltip: () => "关闭后将不会将收取其它地方的成熟农作物。",
            getValue: () => Config.IsCollectOther,
            setValue: value => Config.IsCollectOther = value
        );
        configMenu.AddSectionTitle(
            ModManifest,
            () =>
            {
                var color = DiscreteColorPicker.getColorFromSelection(Config.CropsChestColorIdx);
                return $"当前存放自动收取农作物的箱子颜色为：{color}";
            }
        );
        configMenu.AddNumberOption(
            ModManifest,
            name: () => "家中存放自动收取农作物的箱子颜色",
            tooltip: () => "家中如果有这个颜色的箱子，则将自动收取农作物放入此色箱子中。\n数值表示选择箱子颜色时颜色的序号，从0到20，0是默认色，1是蓝色，20是白色。",
            getValue: () => Config.CropsChestColorIdx,
            setValue: value => Config.CropsChestColorIdx = value,
            min: 0,
            max: 20,
            interval: 1
        );
        configMenu.AddSectionTitle(
            ModManifest,
            () => "浇灌农作物"
        );
        configMenu.AddBoolOption(
            ModManifest,
            name: () => "是否需要帮忙浇灌农作物？(总开关）",
            tooltip: () => "关闭后将不会将浇灌农作物。",
            getValue: () => Config.IsWaterCrop,
            setValue: value => Config.IsWaterCrop = value
        );
        configMenu.AddBoolOption(
            ModManifest,
            name: () => "是否需要帮忙浇灌在鹈鹕镇农场的农作物？",
            tooltip: () => "关闭后将不会将浇灌在鹈鹕镇农场的农作物。",
            getValue: () => Config.IsWaterFarm,
            setValue: value => Config.IsWaterFarm = value
        );
        configMenu.AddBoolOption(
            ModManifest,
            name: () => "是否需要帮忙浇灌在姜岛的农作物？",
            tooltip: () => "关闭后将不会将浇灌在姜岛的农作物。",
            getValue: () => Config.IsWaterIslandWest,
            setValue: value => Config.IsWaterIslandWest = value
        );
        configMenu.AddBoolOption(
            ModManifest,
            name: () => "是否需要帮忙浇灌在其它地方的成熟农作物？",
            tooltip: () => "关闭后将不会将浇灌在其它地方的成熟农作物。",
            getValue: () => Config.IsWaterOther,
            setValue: value => Config.IsWaterOther = value
        );
        configMenu.AddSectionTitle(
            ModManifest,
            () => "拿取物品"
        );
        configMenu.AddBoolOption(
            ModManifest,
            name: () => "是否需要帮忙从机器中拿取物品？",
            tooltip: () => "关闭后将不会将从机器中拿取物品。",
            getValue: () => Config.IsGrabItem,
            setValue: value => Config.IsGrabItem = value
        );
        configMenu.AddSectionTitle(
            ModManifest,
            () =>
            {
                var color = DiscreteColorPicker.getColorFromSelection(Config.ItemsChestColorIdx);
                return $"当前存放自动拿取物品的箱子颜色为：{color}";
            }
        );
        configMenu.AddNumberOption(
            ModManifest,
            name: () => "家中存放自动拿取物品的箱子颜色",
            tooltip: () => "家中如果有对应数值颜色的箱子，则将自动拿取机器的输出物品放入此色箱子中。\n数值表示选择箱子颜色时颜色的序号，从0到20，0是默认色，1是蓝色，20是白色。",
            getValue: () => Config.ItemsChestColorIdx,
            setValue: value => Config.ItemsChestColorIdx = value,
            min: 0,
            max: 20,
            interval: 1
        );
        configMenu.AddSectionTitle(
            ModManifest,
            () => "制作"
        );
        configMenu.AddBoolOption(
            ModManifest,
            name: () => "是否需要帮忙制作蛋黄酱？",
            tooltip: () => "关闭后将不会自动从自动采集器里拿取鸡蛋并放入蛋黄酱机。",
            getValue: () => Config.IsMakeMayonnaise,
            setValue: value => Config.IsMakeMayonnaise = value
        );
        configMenu.AddBoolOption(
            ModManifest,
            name: () => "是否需要帮忙制作奶酪？",
            tooltip: () => "关闭后将不会自动从自动采集器里拿取奶并放入压酪机。",
            getValue: () => Config.IsMakeCheese,
            setValue: value => Config.IsMakeCheese = value
        );
        configMenu.AddBoolOption(
            ModManifest,
            name: () => "是否需要帮忙制作布料？",
            tooltip: () => "关闭后将不会从自动采集器里拿取动物毛并放入织布机。",
            getValue: () => Config.IsMakeFabric,
            setValue: value => Config.IsMakeFabric = value
        );
        configMenu.AddSectionTitle(
            ModManifest,
            () => "杂项"
        );
        configMenu.AddBoolOption(
            ModManifest,
            name: () => "是否需要帮忙驱赶乌鸦？",
            tooltip: () => "开启后农场将不会出现乌鸦（也许吧）。",
            getValue: () => Config.IsScareCrow,
            setValue: value => Config.IsScareCrow = value
        );
    }

    private void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
    {
        if (e.NameWithoutLocale.IsEquivalentTo($"Characters/Dialogue/{HELPER_ID}"))
            e.LoadFromModFile<Dictionary<string, string>>("assets/dialogue.json", AssetLoadPriority.Medium);
        if (e.NameWithoutLocale.IsEquivalentTo("Characters/Dialogue/rainy"))
            e.LoadFromModFile<Dictionary<string, string>>("assets/rainy.json", AssetLoadPriority.Medium);
        if (e.NameWithoutLocale.IsEquivalentTo($"Characters/schedules/{HELPER_ID}")) return;
        if (e.NameWithoutLocale.IsEquivalentTo($"Portraits/{HELPER_ID}"))
            e.LoadFromModFile<Texture2D>("assets/portrait.png", AssetLoadPriority.Medium);
        if (e.NameWithoutLocale.IsEquivalentTo($"Characters/{HELPER_ID}"))
            e.LoadFromModFile<Texture2D>("assets/character.png", AssetLoadPriority.Medium);
        if (e.NameWithoutLocale.IsEquivalentTo("Data/Characters"))
            e.Edit(asset =>
            {
                var editor = asset.AsDictionary<string, CharacterData>();
                editor.Data[HELPER_ID] = new CharacterData
                {
                    DisplayName = HELPER_NAME,
                    BirthSeason = Season.Winter,
                    BirthDay = 6,
                    HomeRegion = "Other",
                    Language = NpcLanguage.Default,
                    Age = NpcAge.Adult,
                    Manner = NpcManner.Polite,
                    SocialAnxiety = NpcSocialAnxiety.Outgoing,
                    Optimism = NpcOptimism.Negative,
                    IsDarkSkinned = true, // Keeping this for character diversity, but removing other stereotypes.
                    CanBeRomanced = false,
                    LoveInterest = null,
                    Calendar = CalendarBehavior.AlwaysShown,
                    SocialTab = SocialTabBehavior.AlwaysShown,
                    CanReceiveGifts = true,
                    CanGreetNearbyCharacters = true,
                    CanCommentOnPurchasedShopItems = true,
                    CanVisitIsland = "!DAY_OF_WEEK Tuesday Thursday",
                    Home = new List<CharacterHomeData>
                    {
                        new()
                        {
                            Id = "Default",
                            Condition = null,
                            Location = "Town", // Changed from Tent
                            Tile = new Point(2, 2),
                            Direction = "down"
                        }
                    }
                };
            });
        // Helper Bell
        if (e.NameWithoutLocale.IsEquivalentTo("Data/BigCraftables"))
            e.Edit(asset =>
            {
                var editor = asset.AsDictionary<string, BigCraftableData>();
                editor.Data[HELPER_BELL_ID] = new BigCraftableData
                {
                    Name = HELPER_BELL_NAME,
                    DisplayName = HELPER_BELL_NAME,
                    Description = "一个古朴的铃铛，摇动它可以召唤一个帮手。",
                    IsLamp = false,
                    Price = 2500,
                    Fragility = 2,
                    Texture = "TileSheets/Craftables",
                    SpriteIndex = 9, // A bell icon
                    ContextTags = new List<string>()
                };
            });
        if (e.NameWithoutLocale.IsEquivalentTo("Data/Shops"))
            e.Edit(asset =>
            {
                var editor = asset.AsDictionary<string, ShopData>();
                var shop = editor.Data["Saloon"];
                shop.Items.Add(new ShopItemData
                {
                    TradeItemAmount = 1,
                    Id = HELPER_BELL_ID,
                    ItemId = HELPER_BELL_ID,
                    Price = 2500
                });
            });
        // Scarecrow
        if (e.NameWithoutLocale.IsEquivalentTo("Data/BigCraftables"))
            e.Edit(asset =>
            {
                var editor = asset.AsDictionary<string, BigCraftableData>();
                editor.Data[SCARECROW_ID] = new BigCraftableData
                {
                    Name = SCARECROW_NAME,
                    DisplayName = SCARECROW_NAME,
                    Description = $"{SCARECROW_NAME}是一种强力的稻草人，乌鸦很惧怕它！",
                    IsLamp = true,
                    Price = 500,
                    Fragility = 2,
                    Texture = "TileSheets/Craftables",
                    SpriteIndex = 136,
                    ContextTags = new List<string>
                    {
                        "light_source",
                        "crow_scare",
                        "crow_scare_radius_99"
                    }
                };
            });
    }

    private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
    {
        if (!Context.IsWorldReady || !Game1.IsMasterGame)
            return;
        switch (e.Button)
        {
            case SButton.L when Game1.activeClickableMenu == null:
            {
                var free = Helpers.Count(helper => !helper.IsBusy);

                Game1.addHUDMessage(new HUDMessage(
                    $"您的{HELPER_NAME}们({free}/{Helpers.Count}空闲)还需要完成({Jobs.Count}/{Jobs.Count + Helpers.Count - free})个工作！",
                    HUDMessage.achievement_type));
                break;
            }
            case SButton.K when Game1.activeClickableMenu == null:
            {
                ShouldFollow = !ShouldFollow;
                var followStr = ShouldFollow ? "跟随" : "不跟随";
                Game1.addHUDMessage(new HUDMessage($"您已命令{HELPER_NAME}们{followStr}！", HUDMessage.achievement_type));
                break;
            }
            case SButton.J when Game1.activeClickableMenu == null:
            {
                if (Game1.player.hasBuff("ultra_speed"))
                {
                    Game1.addHUDMessage(new HUDMessage("您已取消速速之力！", HUDMessage.achievement_type));
                    Game1.player.buffs.Remove("ultra_speed");
                }
                else
                {
                    Game1.addHUDMessage(new HUDMessage("您已开启（+8）的速速之力！", HUDMessage.achievement_type));
                    var effects = new BuffEffects
                    {
                        Speed =
                        {
                            Value = 8
                        }
                    };
                    Game1.player.applyBuff(new Buff("ultra_speed", "速速之力", "速速之力",
                        30 * 60 * Game1.realMilliSecondsPerGameMinute, null, 0, effects, false));
                }

                break;
            }
        }
    }

    private void HireAHelper(GameLocation loc, Vector2 pos)
    {
        if (!Game1.IsMasterGame) return;
        Game1.addHUDMessage(new HUDMessage($"已召唤一个{HELPER_NAME}！", HUDMessage.achievement_type));
        var helperSprite = new AnimatedSprite($"Characters/{HELPER_ID}", 0, 16, 32);
        var helper = new FarmHelper(Helpers.Count + 1, helperSprite, new Vector2(pos.X - 64f, pos.Y), 0, HELPER_ID, this)
        {
            currentLocation = loc
        };
        Helpers.Add(helper);
        Game1.getLocationFromName(Game1.player.currentLocation.Name).addCharacter(helper);
    }

    private void CheckHireHelpers()
    {
        if (!Context.IsWorldReady || Game1.activeClickableMenu != null || !Game1.IsMasterGame)
            return;
        foreach (var location in Helper.Multiplayer.GetActiveLocations())
        foreach (var obj in location.Objects.Values)
        {
            if (!obj.QualifiedItemId.Contains(HELPER_BELL_ID)) continue;
            if (obj.destroyOvernight)
                continue;
            obj.destroyOvernight = true;
            if (Helpers.Count >= 8)
                Game1.addHUDMessage(new HUDMessage($"您最多只能召唤8个{HELPER_NAME}！", HUDMessage.error_type));
            else
                // Hire a helper
                HireAHelper(location, obj.TileLocation * Game1.tileSize);
        }
    }

    private void ClearHelpers()
    {
        if (!Game1.IsMasterGame) return;
        // Delete All Helpers
        foreach (var helper in Helpers) helper.currentLocation.characters.Remove(helper);
        Helpers.Clear();
        // Clear jobs
        Jobs.Clear();
        VisitedJobs.Clear();
    }

    private Object? GetFreeMachine(string ID)
    {
        foreach (var location in Helper.Multiplayer.GetActiveLocations())
        foreach (var obj in location.Objects.Values.Where(obj => obj.QualifiedItemId == ID).Where(obj =>
                     !obj.isTemporarilyInvisible &&
                     !obj.readyForHarvest.Value &&
                     obj.heldObject.Value is null))
        {
            obj.isTemporarilyInvisible = true;
            return obj;
        }

        return null;
    }

    private void FindJobs(GameLocation loc)
    {
        if (!Context.IsWorldReady || Game1.activeClickableMenu != null || !Game1.IsMasterGame)
            return;
        var newJobs = 0;
        var jobsCnt = new int[6] { 0, 0, 0, 0, 0, 0 };
        // 检查机器
        foreach (var obj in loc.Objects.Values)
        {
            var pos = obj.TileLocation.ToPoint();
            if (Config!.IsGrabItem && obj.readyForHarvest.Value)
            {
                // 可收集装置，执行收集任务
                var j1 = new Job(JobType.GrabItem, loc, pos)
                {
                    IsJobDone = () => !obj.readyForHarvest.Value,
                    Machine = obj
                };
                j1.DoJob = () =>
                {
                    var func = Helper.Reflection.GetMethod(obj, "CheckForActionOnMachine");
                    func.Invoke(Game1.player, false);
                    VisitedJobs.Remove(j1);
                };
                if (VisitedJobs.ContainsKey(j1)) continue;
                newJobs++;
                jobsCnt[2]++;
                Jobs.Add(j1);
                VisitedJobs[j1] = true;
            }

            // 是自动采集器
            if (obj.QualifiedItemId != "(BC)165") continue;
            if (obj.heldObject.Value is not Chest chest) continue;
            foreach (var item in chest.GetItemsForPlayer())
            {
                if (item is null || item.Stack <= 0) continue;
                var qid = item.QualifiedItemId;
                // 是否制作蛋黄酱
                if (Config!.IsMakeMayonnaise &&
                    (item.HasContextTag("egg_item") ||
                     qid == "(O)289" ||
                     qid == "(O)442" ||
                     qid == "(O)305" ||
                     qid == "(O)107" ||
                     qid == "(O)928"))
                {
                    var j1 = new Job(JobType.MakeMayonnaise, loc, pos)
                    {
                        Finished = false
                    };
                    j1.IsJobDone = () => j1.Finished is not null && j1.Finished.Value;
                    j1.DoJob = () =>
                    {
                        var machine = GetFreeMachine("(BC)24");
                        if (machine is not null)
                        {
                            // Try to Put one egg in machine
                            chest.GetItemsForPlayer().Remove(item);
                            if (item.Stack > 1)
                            {
                                item.ConsumeStack(1);
                                chest.GetItemsForPlayer().Add(item);
                                chest.clearNulls();
                            }

                            machine.isTemporarilyInvisible = false;
                            machine.performObjectDropInAction(item.getOne(), false, Game1.player);
                            obj.showNextIndex.Value = true;
                        }

                        // Done this job
                        j1.Finished = true;
                        VisitedJobs.Remove(j1);
                    };
                    if (VisitedJobs.ContainsKey(j1)) continue;
                    newJobs++;
                    jobsCnt[3]++;
                    Jobs.Add(j1);
                    VisitedJobs[j1] = true;
                    continue;
                }

                // 是否制作奶酪
                if (Config.IsMakeCheese &&
                    qid is "(O)436" or "(O)438" or "(O)184" or "(O)186")
                {
                    var j1 = new Job(JobType.MakeCheese, loc, pos);
                    j1.IsJobDone = () => j1.Finished is not null && j1.Finished.Value;
                    j1.DoJob = () =>
                    {
                        var machine = GetFreeMachine("(BC)16");
                        if (machine is not null)
                        {
                            // Try to Put one milk in machine
                            chest.GetItemsForPlayer().Remove(item);
                            if (item.Stack > 1)
                            {
                                item.ConsumeStack(1);
                                chest.GetItemsForPlayer().Add(item);
                                chest.clearNulls();
                            }

                            machine.isTemporarilyInvisible = false;
                            machine.performObjectDropInAction(item.getOne(), false, Game1.player);
                            obj.showNextIndex.Value = true;
                        }

                        // Done this job
                        j1.Finished = true;
                        VisitedJobs.Remove(j1);
                    };
                    if (VisitedJobs.ContainsKey(j1)) continue;
                    newJobs++;
                    jobsCnt[4]++;
                    Jobs.Add(j1);
                    VisitedJobs[j1] = true;
                    continue;
                }

                // 是否制作布匹
                if (!Config.IsMakeFabric || qid != "(O)440") continue;
                {
                    var j1 = new Job(JobType.MakeFabric, loc, pos);
                    j1.IsJobDone = () => j1.Finished is not null && j1.Finished.Value;
                    j1.DoJob = () =>
                    {
                        var machine = GetFreeMachine("(BC)17");
                        if (machine is not null)
                        {
                            // Try to Put one wool in machine
                            chest.GetItemsForPlayer().Remove(item);
                            if (item.Stack > 1)
                            {
                                item.ConsumeStack(1);
                                chest.GetItemsForPlayer().Add(item);
                                chest.clearNulls();
                            }

                            machine.isTemporarilyInvisible = false;
                            machine.performObjectDropInAction(item.getOne(), false, Game1.player);
                            obj.showNextIndex.Value = true;
                        }

                        // Done this job
                        j1.Finished = true;
                        VisitedJobs.Remove(j1);
                    };
                    if (VisitedJobs.ContainsKey(j1)) continue;
                    newJobs++;
                    jobsCnt[5]++;
                    Jobs.Add(j1);
                    VisitedJobs[j1] = true;
                }
            }
        }

        // 检查地形
        var collect = (loc.Name == "Farm" && Config!.IsCollectFarm) ||
                      (loc.Name == "IslandWest" && Config!.IsCollectIslandWest) ||
                      (loc.Name != "Farm" && loc.Name != "IslandWest" && Config!.IsCollectOther);
        var water = (loc.Name == "Farm" && Config!.IsWaterFarm) ||
                    (loc.Name == "IslandWest" && Config!.IsWaterIslandWest) ||
                    (loc.Name != "Farm" && loc.Name != "IslandWest" && Config!.IsWaterOther);
        foreach (var terr in loc.terrainFeatures.Values)
        {
            var dirt = terr as HoeDirt;
            if (dirt is null) continue;
            var pos = terr.Tile.ToPoint();
            // 可收获，优先执行收获任务
            if (collect && Config!.IsCollectCrop && dirt.readyForHarvest())
            {
                var j1 = new Job(JobType.CollectCrop, loc, pos)
                {
                    IsJobDone = () => !dirt.readyForHarvest(),
                    TerrainFeature = terr
                };
                j1.DoJob = () => { VisitedJobs.Remove(j1); };
                if (VisitedJobs.ContainsKey(j1)) continue;
                newJobs++;
                jobsCnt[0]++;
                Jobs.Add(j1);
                VisitedJobs[j1] = true;
                continue;
            }

            // 有作物且没浇水，执行浇水任务
            if (!water || !Config!.IsWaterCrop || dirt.crop is null || !dirt.needsWatering() ||
                dirt.isWatered()) continue;
            var j2 = new Job(JobType.WaterCrop, loc, pos)
            {
                IsJobDone = dirt.isWatered,
                TerrainFeature = terr
            };
            j2.DoJob = () =>
            {
                dirt.state.Value = 1;
                VisitedJobs.Remove(j2);
            };
            if (VisitedJobs.ContainsKey(j2)) continue;
            newJobs++;
            jobsCnt[1]++;
            Jobs.Add(j2);
            VisitedJobs[j2] = true;
        }

        if (newJobs > 0 && jobsCnt[0] + jobsCnt[1] + jobsCnt[2] > 0)
            Game1.addHUDMessage(new HUDMessage(
                $"收取{jobsCnt[0]}，浇地{jobsCnt[1]}，拿取{jobsCnt[2]}，制作蛋黄酱{jobsCnt[3]}，制作奶酪{jobsCnt[4]}，制作布料{jobsCnt[5]}\n已在{loc.Name}收集到{newJobs}个新任务!当前总共{Jobs.Count}个任务！",
                HUDMessage.achievement_type));
    }
}