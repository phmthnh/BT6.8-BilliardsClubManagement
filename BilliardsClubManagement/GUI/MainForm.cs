using System.Data;
using System.Diagnostics;
using BilliardsClubManagement.BUS;
using BilliardsClubManagement.DTO;

namespace BilliardsClubManagement.GUI;

public partial class MainForm : Form
{
    private readonly BilliardsBus service = new();
    private readonly System.Windows.Forms.Timer clock = new() { Interval = 1000 };
    private readonly Stopwatch elapsed = new();
    private TableRecord? selected;
    private OpenBill? active;

    public MainForm()
    {
        InitializeComponent();
        clock.Tick += (_, _) => DisplayTime();
        Disposed += (_, _) => clock.Dispose();
        Shown += (_, _) => Ui.Run(this, () =>
        {
            Reload(null);
            clock.Start();
        });
    }

    private void Reload(int? keepTable)
    {
        var tables = service.Tables();
        tableMap.SuspendLayout();
        while (tableMap.Controls.Count > 0)
            tableMap.Controls[0].Dispose();
        foreach (var table in tables)
        {
            var button = new Button
            {
                Name = $"table{table.Id}",
                Text = $"{table.Name}\n{(table.InUse ? "Đang chơi" : "Trống")}",
                BackColor = table.InUse ? Color.PeachPuff : Color.PaleGreen,
                Width = 118,
                Height = 69,
                Margin = new Padding(4)
            };
            button.Click += (_, _) => Ui.Run(this, () =>
            {
                paymentStatus.Text = "";
                SelectTable(table);
            });
            tableMap.Controls.Add(button);
        }
        tableMap.ResumeLayout();
        Ui.Bind(serviceChoice, service.Menu(), "TenDV", "MaDV");
        SelectTable(tables.FirstOrDefault(table => table.Id == keepTable));
    }

    private void SelectTable(TableRecord? table)
    {
        selected = table;
        active = table is { InUse: true } ? service.ActiveBill(table.Id) : null;
        elapsed.Restart();
        selectedTable.Text = table is null
            ? "Chọn bàn trên sơ đồ"
            : $"{table.Name} · {table.Type} · {table.HourlyRate:N2} đ/giờ";
        start.Enabled = table is { InUse: false };
        pay.Enabled = add.Enabled = quantity.Enabled = serviceChoice.Enabled = active is not null;
        ReloadOrders();
        DisplayTime();
    }

    private void ReloadOrders()
    {
        orders.DataSource = active is null ? null : service.Orders(active.Id);
        if (orders.Columns.Count == 0)
            return;
        orders.Columns["MaDV"].HeaderText = "Mã dịch vụ";
        orders.Columns["TenDV"].HeaderText = "Tên dịch vụ";
        orders.Columns["SoLuong"].HeaderText = "Số lượng";
        orders.Columns["DonGia"].HeaderText = "Đơn giá";
        orders.Columns["ThanhTien"].HeaderText = "Thành tiền";
        orders.Columns["DonGia"].DefaultCellStyle.Format = "N2";
        orders.Columns["ThanhTien"].DefaultCellStyle.Format = "N2";
    }

    private void DisplayTime()
    {
        if (active is null)
        {
            sessionStatus.Text = selected is null ? "" : selected.InUse ? "Hãy làm mới sơ đồ." : "Trống";
            return;
        }
        TimeSpan duration = active.ServerNow - active.StartedAt + elapsed.Elapsed;
        sessionStatus.Text = $"Đang chơi · Bắt đầu {active.StartedAt:dd/MM/yyyy HH:mm:ss} · Đã chơi {(int)duration.TotalHours:D2}:{duration.Minutes:D2}:{duration.Seconds:D2}";
    }

    private void refreshClick(object? sender, EventArgs e) => Ui.Run(this, () => Reload(selected?.Id));

    private void startClick(object? sender, EventArgs e) => Ui.Run(this, () =>
    {
        if (selected is not { InUse: false } table)
            return;
        service.Start(table.Id);
        paymentStatus.Text = "";
        Reload(table.Id);
    });

    private void addClick(object? sender, EventArgs e) => Ui.Run(this, () =>
    {
        if (active is not OpenBill bill)
            return;
        service.Order(bill.Id, Ui.Id(serviceChoice), decimal.ToInt32(quantity.Value));
        quantity.Value = 1;
        ReloadOrders();
    });

    private void payClick(object? sender, EventArgs e) => Ui.Run(this, () =>
    {
        if (active is not OpenBill bill)
            return;
        var result = service.Pay(bill.Id);
        paymentStatus.Text = $"Hóa đơn #{result.BillId} · {result.EndedAt:HH:mm:ss} · Tiền giờ: {result.PlayFee:N2} đ"
            + $" · Dịch vụ: {result.ServiceFee:N2} đ · Tổng: {result.Total:N2} đ";
        Reload(selected?.Id);
    });
}
