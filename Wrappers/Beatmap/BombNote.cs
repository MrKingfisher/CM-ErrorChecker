using Beatmap.Base;
using Beatmap.Enums;
using Beatmap.Helper;
using Jint;
using Jint.Native.Object;

internal class BombNote : VanillaWrapper<BaseNote>
{
    public BombNote(Engine engine, BaseNote bomb) : base(engine, bomb)
    {
        spawned = true;
    }

    public BombNote(Engine engine, ObjectInstance o) : base(engine, BeatmapFactory.Bomb(
        (float)GetJsValue(o, new[] { "b", "_time" }),
        (int)GetJsValue(o, new[] { "x", "_lineIndex" }),
        (int)GetJsValue(o, new[] { "y", "_lineLayer" }),
        GetCustomData(o, new[] { "customData", "_customData" })
    ), false, GetJsBool(o, "selected"))
    {
        spawned = false;

        DeleteObject();
    }

    public float b
    {
        get => wrapped.JsonTime;
        set
        {
            DeleteObject();
            wrapped.JsonTime = value;
        }
    }

    public int x
    {
        get => wrapped.PosX;
        set
        {
            DeleteObject();
            wrapped.PosX = value;
        }
    }

    public int y
    {
        get => wrapped.PosY;
        set
        {
            DeleteObject();
            wrapped.PosY = value;
        }
    }

    public override bool SpawnObject(BeatmapObjectContainerCollection collection)
    {
        if (spawned) return false;

        collection.SpawnObject(wrapped, false, false, inCollectionOfSpawns: true);

        spawned = true;
        return true;
    }

    internal override bool DeleteObject()
    {
        if (!spawned) return false;

        var collection = BeatmapObjectContainerCollection.GetCollectionForType(ObjectType.Note);
        collection.DeleteObject(wrapped, false, inCollectionOfDeletes: true);

        spawned = false;
        return true;
    }
}
