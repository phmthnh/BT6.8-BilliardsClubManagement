using System.Data;
using BilliardsClubManagement.DAL;
using BilliardsClubManagement.DTO;

namespace BilliardsClubManagement.BUS;

internal sealed class BilliardsBus
{
    private readonly BilliardsDal data = new();

    internal IReadOnlyList<TableRecord> Tables() => data.Tables();
    internal DataTable Menu() => data.Menu();
    internal OpenBill? ActiveBill(int table) => data.ActiveBill(table);
    internal DataTable Orders(int bill) => data.Orders(bill);
    internal int Start(int table) => data.Start(table);
    internal Payment Pay(int bill) => data.Pay(bill);

    internal void Order(int bill, int service, int quantity)
    {
        if (quantity <= 0)
            throw new ArgumentException("Số lượng gọi phải lớn hơn 0.");
        data.Order(bill, service, quantity);
    }
}
