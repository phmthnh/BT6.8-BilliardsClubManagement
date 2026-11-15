using System.Data;
using Microsoft.Data.SqlClient;

namespace BilliardsClubManagement.GUI;

internal static class Ui
{
    internal static DataRowView? Row(DataGridView grid) => grid.CurrentRow?.DataBoundItem as DataRowView;
    internal static int Id(DataGridView grid, string column) => Row(grid)?[column] is int id
        ? id : throw new InvalidOperationException("Hãy chọn một dòng.");
    internal static int Id(ComboBox combo) => combo.SelectedValue is int id
        ? id : throw new InvalidOperationException("Hãy chọn một mục.");

    internal static void Bind(ComboBox combo, DataTable table, string display, string key)
    {
        combo.DisplayMember = display;
        combo.ValueMember = key;
        combo.DataSource = table;
    }

    internal static void Run(Form owner, Action action)
    {
        try
        {
            action();
        }
        catch (SqlException ex)
        {
            Warn(owner, ex.Number switch
            {
                547 => "Không thể thực hiện vì dữ liệu đang được sử dụng hoặc không hợp lệ.",
                2601 or 2627 => "Thông tin đã tồn tại.",
                1205 => "Dữ liệu đang được cập nhật ở phiên khác. Vui lòng thử lại.",
                _ => "Không thể thao tác trên SQL Server. Kiểm tra kết nối và thử lại."
            });
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or OverflowException
            or System.Drawing.Printing.InvalidPrinterException or System.ComponentModel.Win32Exception)
        {
            Warn(owner, ex.Message);
        }
    }

    private static void Warn(Form owner, string message) =>
        MessageBox.Show(owner, message, "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
}
