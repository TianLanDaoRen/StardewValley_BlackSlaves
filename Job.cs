using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.TerrainFeatures;
using Object = StardewValley.Object;

namespace FarmHelpers;

public enum JobType
{
    CollectCrop,
    WaterCrop,
    GrabItem,
    MakeMayonnaise,
    MakeCheese,
    MakeFabric
}

public class Job
{
    public delegate bool JobDoneDelegate();

    public readonly GameLocation JobLocation;
    public readonly JobType JobType;

    public Action? DoJob;
    public bool? Finished;
    public JobDoneDelegate? IsJobDone;
    public Point JobPoint;
    public Object? Machine;

    public TerrainFeature? TerrainFeature;

    public Job(JobType type, GameLocation loc, Point endPoint)
    {
        JobType = type;
        JobLocation = loc;
        JobPoint = endPoint;
    }
}

public sealed class JobEqComparer : EqualityComparer<Job>
{
    public override bool Equals(Job? x, Job? y)
    {
        if (x is null && y is null) return true;
        if (x is null || y is null) return false;
        return x.JobType == y.JobType && x.JobLocation.Equals(y.JobLocation) && x.JobPoint == y.JobPoint;
    }

    public override int GetHashCode(Job obj)
    {
        return obj.JobPoint.GetHashCode();
    }
}