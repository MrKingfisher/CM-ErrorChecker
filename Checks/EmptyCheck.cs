using System.Collections.Generic;
using Beatmap.Base;
using Beatmap.Base.Customs;

class EmptyCheck : Check
{
    public EmptyCheck() : base("Select a check")
    {
    }

    public override CheckResult PerformCheck(List<BaseNote> notes, List<BaseNote> bombs, List<BaseArc> arcs,
        List<BaseChain> chains, List<BaseEvent> events, List<BaseObstacle> walls, List<BaseCustomEvent> customEvents,
        List<BaseBpmEvent> bpmEvents, params KeyValuePair<string, IParamValue>[] vals)
    {
        result.Clear();
        return result;
    }
}
