namespace R3E.Features.TimeGap
{
    public interface ITimeGapService
    {
        float GetTimeGapRelative(int subjectSlotId, int targetSlotId);
    }
}
