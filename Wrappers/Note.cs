﻿using Jint;
using Jint.Native.Object;

class Note : VanillaWrapper<BeatmapNote>
{ 
    public float _time {
        get { return wrapped._time; }
        set {
            DeleteObject();
            wrapped._time = value;
        }
    }

    public int _lineIndex
    {
        get { return wrapped._lineIndex; }
        set
        {
            DeleteObject();
            wrapped._lineIndex = value;
        }
    }

    public int _lineLayer
    {
        get { return wrapped._lineLayer; }
        set
        {
            DeleteObject();
            wrapped._lineLayer = value;
        }
    }

    public int _cutDirection
    {
        get { return wrapped._cutDirection; }
        set
        {
            DeleteObject();
            wrapped._cutDirection = value;
        }
    }

    public int _type
    {
        get { return wrapped._type; }
        set
        {
            DeleteObject();
            wrapped._type = value;
        }
    }

    public Note(Engine engine, BeatmapNote note) : base(engine, note)
    {
        spawned = true;
    }

    public Note(Engine engine, ObjectInstance o) : base(engine, new BeatmapNote(
        (float) GetJsValue(o, "_time"),
        (int) GetJsValue(o, "_lineIndex"),
        (int) GetJsValue(o, "_lineLayer"),
        (int) GetJsValue(o, "_type"),
        (int) GetJsValue(o, "_cutDirection"),
        GetCustomData(o)
    ), false)
    {
        spawned = false;

        DeleteObject();
    }

    public override bool SpawnObject()
    {
        if (spawned) return false;

        var collection = BeatmapObjectContainerCollection.GetCollectionForType(BeatmapObject.Type.NOTE);
        collection.SpawnObject(wrapped, false, false);

        spawned = true;
        return true;
    }

    protected override bool DeleteObject()
    {
        if (!spawned) return false;

        var collection = BeatmapObjectContainerCollection.GetCollectionForType(BeatmapObject.Type.NOTE);
        collection.DeleteObject(wrapped, false);

        spawned = false;
        return true;
    }
}
