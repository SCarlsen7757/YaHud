namespace R3E.API.TimeGap
{
    public interface ITimeGapService
    {
        float GetTimeGapRelative(int subjectSlotId, int targetSlotId);
    }
}
