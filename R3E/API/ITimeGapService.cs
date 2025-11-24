namespace R3E.API
{
    public interface ITimeGapService
    {
        float GetTimeGapRelative(int subjectSlotId, int targetSlotId);
    }
}
