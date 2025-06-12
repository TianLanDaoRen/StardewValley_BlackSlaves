using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.GameData.Machines;
using StardewValley.Menus;
using StardewValley.Objects;
using StardewValley.Pathfinding;
using StardewValley.TerrainFeatures;
using Object = StardewValley.Object;

namespace BlackSlaves;

public class BlackSlave : NPC
{
    private readonly ModEntry? Entry;
    private readonly int MAX_RANGE = 1;
    private Job? currentJob;

    public bool IsBusy;
    private int stuckTries;

    public BlackSlave()
    {
        Entry = null;
        IsBusy = false;
        stuckTries = 0;
        controller = null;
    }

    public BlackSlave(int id, AnimatedSprite sprite, Vector2 position, int facingDir, string name, ModEntry entry,
        LocalizedContentManager? content = null) : base(sprite, position, facingDir, name, content)
    {
        Entry = entry;
        IsBusy = false;
        stuckTries = 0;
        controller = null;
    }

    public void UpdateTicked()
    {
        if (Entry is null) return;
        // Called in every frame
        if (IsBusy)
        {
            JobTime();
            return;
        }

        // Slave is now Free
        if (Entry.Jobs.Count == 0)
            FreeTime(Entry.ShouldFollow);
        else
            // Check for jobs
            ReadyForJob(PickJob());
    }

    public void JobTime()
    {
        if (Entry is null) return;
        if (currentJob!.IsJobDone!.Invoke())
        {
            // Job is done
            AfterDoneJob();
            return;
        }

        var delta = currentJob.JobPoint - TilePoint;
        var distance = delta.ToVector2().Length();
        if (currentJob.JobLocation.Equals(currentLocation) && distance <= MAX_RANGE)
        {
            DoingJob();
            return;
        }

        // Move to job if too far away
        MoveTo(currentJob.JobLocation, currentJob.JobPoint, true);
    }

    public void FreeTime(bool followPlayer)
    {
        if (Entry is null) return;
        if (followPlayer)
        {
            // Move to player
            MoveTo(Game1.player.currentLocation, Game1.player.TilePoint, false);
            return;
        }

        // Not follow player, teleport to somewhere else
        foreach (var location in Entry.Helper.Multiplayer.GetActiveLocations())
        {
            if (location.Equals(Game1.player.currentLocation)) continue;
            Game1.warpCharacter(this, location, Vector2.One);
        }
    }

    public void MoveTo(GameLocation loc, Point tilePoint, bool isJob)
    {
        if (Entry is null) return;
        //var TYPE = isJob ? "工作行走" : "空闲行走";
        Speed = Game1.random.Next(4, 9);
        // Slave is currently being stuck
        if (controller is not null && !isMoving()) stuckTries++;
        if (stuckTries >= 100)
        {
            if (isJob)
                //Entry.Monitor.Log($"{TYPE} {ModEntry.SLAVE_NAME}#{ID}[{TilePoint}@{currentLocation.Name}] 被卡住，传送至任务！", StardewModdingAPI.LogLevel.Warn);
                Game1.warpCharacter(this, loc, tilePoint.ToVector2());
            else
                //Entry.Monitor.Log($"{TYPE} {ModEntry.SLAVE_NAME}#{ID}[{TilePoint}@{currentLocation.Name}] 被卡住，传送至玩家方位！", StardewModdingAPI.LogLevel.Warn);
                // Teleport to player's position
                Game1.warpCharacter(this, Game1.player.currentLocation, Game1.player.TilePoint.ToVector2());
            stuckTries = 0;
        }

        if (!currentLocation.Equals(loc)) Game1.warpCharacter(this, loc, tilePoint.ToVector2());
        // Has arrived at target point
        if ((TilePoint - tilePoint).ToVector2().Length() <= MAX_RANGE)
        {
            // When slave is near target, done walking
            stuckTries = 0;
            controller = null;
            return;
        }

        // Walk to target tilepoint when possible
        if (controller is not null) return;
        controller = new PathFindController(this, loc, tilePoint, -1)
        {
            NPCSchedule = true,
            nonDestructivePathing = true
        };
        // Slave can't walk to target
        if (controller?.pathToEndPoint is { Count: > 0 }) return;
        // Teleport to target
        Game1.warpCharacter(this, loc, tilePoint.ToVector2());
        // JobTime, make job done already
        if (isJob && currentJob is not null)
            //Entry.Monitor.Log($"{TYPE} {ModEntry.SLAVE_NAME}#{ID}[{TilePoint}@{currentLocation.Name}] 无法走到[{tilePoint}@{loc.Name}]，直接完成工作！", StardewModdingAPI.LogLevel.Warn);
            DoingJob();
    }

    public Job? PickJob()
    {
        if (Entry is null || Entry.Mutex) return null;
        Entry.Mutex = true;
        // Pick the nearest job possible
        var targetJob = Entry.Jobs.ElementAt(0);
        float miniDistance = 9999999999999999;
        foreach (var job in Entry.Jobs)
        {
            var distance = (job.JobPoint.ToVector2() - TilePoint.ToVector2()).LengthSquared();
            if (!job.JobLocation.Equals(currentLocation))
                // 优先完成同场景任务
                distance += 9999999;
            if (job.JobType is JobType.MakeMayonnaise or JobType.MakeCheese or JobType.MakeFabric)
                // 落后完成农舍任务
                distance += 99999999;
            if (!(distance < miniDistance)) continue;
            miniDistance = distance;
            targetJob = job;
        }

        Entry.Jobs.Remove(targetJob);

        Entry.Mutex = false;
        return targetJob;
    }

    public void ReadyForJob(Job? job)
    {
        if (Entry is null || job is null) return;
        //Entry.Monitor.Log($"{ModEntry.SLAVE_NAME}#{ID}[{TilePoint}@{currentLocation.Name}] 准备去[{job.JobPoint}@{job.JobLocation}]进行{job.JobType}工作！");
        IsBusy = true;
        currentJob = job;
        stuckTries = 0;
        controller = null;
    }

    public void DoingJob()
    {
        if (Entry is null || currentJob is null) return;
        //Entry.Monitor.Log($"{ModEntry.SLAVE_NAME}#{ID}[{TilePoint}@{currentLocation.Name}] 正在[{currentJob.JobPoint}@{currentJob.JobLocation}]进行{currentJob.JobType}工作！");
        switch (currentJob.JobType)
        {
            case JobType.CollectCrop:
                CollectCropToAutoGrabber(currentJob.TerrainFeature);
                break;
            case JobType.GrabItem:
                CollectItemToAutoGrabber(currentJob.Machine);
                break;
            case JobType.WaterCrop:
            case JobType.MakeMayonnaise:
            case JobType.MakeCheese:
            case JobType.MakeFabric:
            default:
                throw new ArgumentOutOfRangeException();
        }

        currentJob.DoJob!.Invoke();
        //Entry.Monitor.Log($"{ModEntry.SLAVE_NAME}#{ID}[{TilePoint}@{currentLocation.Name}] 做了[{currentJob.JobPoint}@{currentJob.JobLocation}]的{currentJob.JobType}工作！");
        if (currentJob.IsJobDone!.Invoke())
            //Entry.Monitor.Log($"{ModEntry.SLAVE_NAME}#{ID}[{TilePoint}@{currentLocation.Name}] 完成了[{currentJob.JobPoint}@{currentJob.JobLocation}]的{currentJob.JobType}工作！");
            AfterDoneJob();
    }

    public void AfterDoneJob()
    {
        IsBusy = false;
        currentJob = null;
        stuckTries = 0;
        controller = null;
    }

    public void CollectCropToAutoGrabber(TerrainFeature? terrainFeature)
    {
        if (Entry is null) return;
        var dirt = terrainFeature as HoeDirt;
        var home = Game1.getLocationFromName(Game1.player.homeLocation.Value);
        if (dirt is null || dirt.crop is null || dirt.readyForHarvest() == false || home is null ||
            terrainFeature is null) return;
        var xTile = dirt.crop.tilePosition.X;
        var yTile = dirt.crop.tilePosition.Y;
        var data = dirt.crop.GetData();
        try
        {
            // Try To Add item
            var exp = dirt.crop.forageCrop.Value ? 3 : 0;
            var harvester = new AutoHarvester(home, exp);
            dirt.crop.harvest((int)xTile, (int)yTile, dirt, harvester, true);
            var lastHarvestedItem = harvester.GetLastHarvestedItem();
            if (lastHarvestedItem is null) return;
            // Not Success and lastHarvestedItem is not null
            if (!harvester.GetAddItemSuccess()) Game1.player.addItemToInventory(lastHarvestedItem);
            // Check need destroy crop
            var regrowDays = data?.RegrowDays ?? -1;
            if (regrowDays <= 0)
                // Can't regrow
                dirt.destroyCrop(false);
            // Player exp reward
            if (dirt.crop.indexOfHarvest.Value == "421") dirt.crop.indexOfHarvest.Value = "431";
            var price = 0;
            var harvestedItem = dirt.crop.programColored.Value
                ? new ColoredObject(dirt.crop.indexOfHarvest.Value, 1, dirt.crop.tintColor.Value)
                : ItemRegistry.Create(dirt.crop.indexOfHarvest.Value);
            if (harvestedItem is Object obj) price = obj.Price;
            var experience2 = (float)(16.0 * Math.Log(0.018 * price + 1.0, 2.718281828459045));
            Game1.player.gainExperience(0, (int)Math.Round(experience2));
        }
        catch (Exception ex)
        {
            Entry.Monitor.Log("工作遇到小问题，没问题的。");
        }
    }

    public void CollectItemToAutoGrabber(Object? machine)
    {
        if (Entry is null) return;
        try
        {
            var home = Game1.getLocationFromName(Game1.player.homeLocation.Value);
            if (home is null || machine is null || machine.readyForHarvest.Value == false) return;
            var who = Game1.player;
            var location = currentJob!.JobLocation;
            using var enumerator = home.Objects.GetEnumerator();
            while (enumerator.MoveNext())
            {
                var kvp = enumerator.Current;
                foreach (var item in kvp!.Where(item => item.Value.GetType() == typeof(Chest)))
                {
                    if (item.Value is not Chest chest) continue;
                    var color = chest.playerChoiceColor.Value;
                    if (color != DiscreteColorPicker.getColorFromSelection(ModEntry.Config!.ItemsChestColorIdx))
                        continue;
                    // Try To Add item
                    var machineData = machine.GetMachineData();
                    if (machine.lastOutputRuleId.Value != null)
                    {
                        List<MachineOutputRule> outputRules = machineData.OutputRules;
                        var outputRule = outputRules?.FirstOrDefault(p => p.Id == machine.lastOutputRuleId.Value);
                        if (outputRule is { RecalculateOnCollect: true })
                        {
                            var prevOutput = machine.heldObject.Value;
                            machine.heldObject.Value = null;
                            machine.OutputMachine(machineData, outputRule, machine.lastInputItem.Value, who,
                                location, false);
                            machine.heldObject.Value ??= prevOutput;
                        }
                    }

                    var checkForReload = false;
                    var objectThatWasHeld = machine.heldObject.Value;
                    if (who.IsLocalPlayer && objectThatWasHeld != null)
                    {
                        // Add to chest
                        machine.heldObject.Value = null;
                        if (chest.addItem(objectThatWasHeld) != null)
                        {
                            // Add failed
                            machine.heldObject.Value = objectThatWasHeld;
                            Game1.showRedMessage(
                                Game1.content.LoadString("Strings\\StringsFromCSFiles:Crop.cs.588"));
                            continue;
                        }

                        Game1.playSound("coin");
                        checkForReload = true;
                        MachineDataUtility.UpdateStats(
                            machineData?.StatsToIncrementWhenHarvested,
                            objectThatWasHeld, objectThatWasHeld.Stack);
                    }

                    machine.heldObject.Value = null;
                    machine.readyForHarvest.Value = false;
                    machine.showNextIndex.Value = false;
                    machine.ResetParentSheetIndex();
                    if (MachineDataUtility.TryGetMachineOutputRule(machine, machineData,
                            MachineOutputTrigger.OutputCollected, objectThatWasHeld?.getOne(), who, location,
                            out var outputCollectedRule, out _, out _,
                            out _))
                        machine.OutputMachine(machineData, outputCollectedRule, machine.lastInputItem.Value, who,
                            location, false);
                    if (machine.IsTapper() &&
                        location.terrainFeatures.TryGetValue(machine.TileLocation, out var terrainFeature))
                        if (terrainFeature is Tree tree)
                            tree.UpdateTapperProduct(machine, objectThatWasHeld);

                    if (machineData is { ExperienceGainOnHarvest: not null })
                    {
                        var expSplit = machineData.ExperienceGainOnHarvest.Split(' ');
                        for (var i = 0; i < expSplit.Length; i += 2)
                        {
                            var skill = Farmer.getSkillNumberFromName(expSplit[i]);
                            if (skill != -1 && ArgUtility.TryGetInt(expSplit, i + 1, out var amount, out _))
                                who.gainExperience(skill, amount);
                        }
                    }

                    if (checkForReload) machine.AttemptAutoLoad(who);
                    // Machine done
                    return;
                }
            }
        }
        catch (Exception)
        {
            // ignored
        }
    }
}