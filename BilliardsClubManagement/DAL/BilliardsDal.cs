using System.Data;
using Microsoft.Data.SqlClient;
using BilliardsClubManagement.DTO;

namespace BilliardsClubManagement.DAL;

internal sealed class BilliardsDal
{
    internal IReadOnlyList<TableRecord> Tables() => Db.Query("""
        SELECT MaBan,TenBan,LoaiBan,GiaTheoGio,TrangThai FROM BanBida ORDER BY MaBan
        """).Rows.Cast<DataRow>().Select(row => new TableRecord(
            row.Field<int>("MaBan"), row.Field<string>("TenBan")!, row.Field<string>("LoaiBan") ?? "",
            row.Field<decimal>("GiaTheoGio"), row.Field<string>("TrangThai") == "Đang chơi")).ToArray();

    internal DataTable Menu() => Db.Query("""
        SELECT MaDV,TenDV,DonGia,TonKho FROM MenuDichVu ORDER BY TenDV,MaDV
        """);

    internal OpenBill? ActiveBill(int table)
    {
        var bills = Db.Query("""
            SELECT MaHD,GioBatDau,GETDATE() AS GioHienTai FROM HoaDon WHERE MaBan=@table AND TrangThai=0
            """, ("@table", table));
        return bills.Rows.Count == 0 ? null : new OpenBill(
            (int)bills.Rows[0]["MaHD"], (DateTime)bills.Rows[0]["GioBatDau"], (DateTime)bills.Rows[0]["GioHienTai"]);
    }

    internal DataTable Orders(int bill)
    {
        using var connection = Db.Open();
        using var command = Db.Command(connection, null, """
            SELECT m.MaDV,m.TenDV,d.SoLuong,d.DonGia,d.SoLuong*d.DonGia AS ThanhTien
            FROM ChiTietHoaDon d JOIN MenuDichVu m ON m.MaDV=d.MaDV WHERE d.MaHD=@bill
            ORDER BY m.TenDV
            """, ("@bill", bill));
        using var adapter = new SqlDataAdapter(command);
        var orders = new DataTable();
        adapter.Fill(orders);
        return orders;
    }

    internal int Start(int table) => Db.InTransaction((connection, transaction) =>
    {
        using var change = Db.Command(connection, transaction, """
            UPDATE BanBida SET TrangThai=N'Đang chơi' WHERE MaBan=@table AND TrangThai=N'Trống'
            """, ("@table", table));
        if (change.ExecuteNonQuery() != 1)
            throw new InvalidOperationException("Chỉ có thể mở bàn đang trống.");

        using var create = Db.Command(connection, transaction, """
            INSERT HoaDon(MaBan,GioBatDau,TienGioChoi,TienDichVu,TrangThai)
            OUTPUT inserted.MaHD VALUES(@table,GETDATE(),0,0,0)
            """, ("@table", table));
        return (int)create.ExecuteScalar()!;
    });

    internal void Order(int bill, int service, int quantity) => Db.InTransaction((connection, transaction) =>
    {
        using var active = Db.Command(connection, transaction, """
            SELECT MaHD FROM HoaDon WITH(UPDLOCK,HOLDLOCK) WHERE MaHD=@bill AND TrangThai=0
            """, ("@bill", bill));
        if (active.ExecuteScalar() is null)
            throw new InvalidOperationException("Bàn đã thanh toán. Hãy làm mới sơ đồ.");

        using var change = Db.Command(connection, transaction, """
            IF EXISTS (SELECT 1 FROM ChiTietHoaDon WHERE MaHD=@bill AND MaDV=@service)
                UPDATE ChiTietHoaDon SET SoLuong=SoLuong+@quantity WHERE MaHD=@bill AND MaDV=@service;
            ELSE
                INSERT ChiTietHoaDon(MaHD,MaDV,SoLuong,DonGia)
                SELECT @bill,MaDV,@quantity,DonGia FROM MenuDichVu WHERE MaDV=@service;
            """, ("@bill", bill), ("@service", service), ("@quantity", quantity));
        if (change.ExecuteNonQuery() != 1)
            throw new InvalidOperationException("Dịch vụ không tồn tại.");
        return true;
    });

    internal Payment Pay(int bill) => Db.InTransaction((connection, transaction) =>
    {
        using var header = Db.Command(connection, transaction, """
            SELECT h.MaBan,h.GioBatDau,GETDATE(),b.GiaTheoGio
            FROM HoaDon h WITH (UPDLOCK,HOLDLOCK) JOIN BanBida b ON b.MaBan=h.MaBan
            WHERE h.MaHD=@bill AND h.TrangThai=0 AND b.TrangThai=N'Đang chơi'
            """, ("@bill", bill));
        int table;
        DateTime end;
        decimal playFee;
        using (var reader = header.ExecuteReader())
        {
            if (!reader.Read())
                throw new InvalidOperationException("Bàn đã thanh toán hoặc hóa đơn không tồn tại.");
            table = reader.GetInt32(0);
            end = reader.GetDateTime(2);
            decimal hours = (end - reader.GetDateTime(1)).Ticks / (decimal)TimeSpan.TicksPerHour;
            if (hours < 0)
                throw new InvalidOperationException("Giờ kết thúc của máy chủ sớm hơn giờ mở bàn.");
            playFee = Math.Round(hours * reader.GetDecimal(3), 2, MidpointRounding.AwayFromZero);
        }

        using var details = Db.Command(connection, transaction, """
            SELECT MaDV,SoLuong,DonGia FROM ChiTietHoaDon WHERE MaHD=@bill ORDER BY MaDV
            """, ("@bill", bill));
        var services = new List<(int Id, int Quantity, decimal Price)>();
        using (var reader = details.ExecuteReader())
        {
            while (reader.Read())
                services.Add((reader.GetInt32(0), reader.GetInt32(1), reader.GetDecimal(2)));
        }
        decimal serviceFee = services.Sum(row => row.Quantity * row.Price);

        using var close = Db.Command(connection, transaction, """
            UPDATE HoaDon SET GioKetThuc=@end,TienGioChoi=@play,TienDichVu=@fee,TrangThai=1 WHERE MaHD=@bill
            """, ("@end", end), ("@play", playFee), ("@fee", serviceFee), ("@bill", bill));
        close.ExecuteNonQuery();

        foreach (var service in services)
        {
            using var stock = Db.Command(connection, transaction, """
                DECLARE @stock int;
                SELECT @stock=TonKho FROM MenuDichVu WITH (UPDLOCK,HOLDLOCK) WHERE MaDV=@id;
                IF @stock IS NULL SELECT 1;
                ELSE IF @stock >= @quantity
                BEGIN
                    UPDATE MenuDichVu SET TonKho=TonKho-@quantity WHERE MaDV=@id;
                    SELECT 1;
                END;
                """, ("@id", service.Id), ("@quantity", service.Quantity));
            if (stock.ExecuteScalar() is not 1)
                throw new InvalidOperationException($"Dịch vụ mã {service.Id} không đủ tồn. Bàn vẫn đang chơi.");
        }

        using var reset = Db.Command(connection, transaction, """
            UPDATE BanBida SET TrangThai=N'Trống' WHERE MaBan=@table
            """, ("@table", table));
        reset.ExecuteNonQuery();
        return new Payment(bill, end, playFee, serviceFee);
    });
}
