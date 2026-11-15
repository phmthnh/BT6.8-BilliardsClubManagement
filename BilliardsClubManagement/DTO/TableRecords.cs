namespace BilliardsClubManagement.DTO;

internal sealed record TableRecord(int Id, string Name, string Type, decimal HourlyRate, bool InUse);
internal sealed record OpenBill(int Id, DateTime StartedAt, DateTime ServerNow);
internal sealed record Payment(int BillId, DateTime EndedAt, decimal PlayFee, decimal ServiceFee)
{
    internal decimal Total => PlayFee + ServiceFee;
}
