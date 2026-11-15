IF DB_ID(N'BilliardsDB') IS NULL
    EXEC(N'CREATE DATABASE [BilliardsDB]');
GO
USE [BilliardsDB];
GO
SET XACT_ABORT ON;
IF OBJECT_ID(N'dbo.BanBida', N'U') IS NULL
BEGIN
    BEGIN TRANSACTION;

    CREATE TABLE BanBida (MaBan int IDENTITY PRIMARY KEY, TenBan nvarchar(50) NOT NULL,
        LoaiBan nvarchar(30), GiaTheoGio decimal(18,2), TrangThai nvarchar(20) DEFAULT N'Trống');
    CREATE TABLE MenuDichVu (MaDV int IDENTITY PRIMARY KEY, TenDV nvarchar(100) NOT NULL,
        DonGia decimal(18,2), TonKho int);
    CREATE TABLE HoaDon (MaHD int IDENTITY PRIMARY KEY, MaBan int REFERENCES BanBida,
        GioBatDau datetime NOT NULL, GioKetThuc datetime NULL,
        TienGioChoi decimal(18,2), TienDichVu decimal(18,2), TrangThai bit);
    CREATE TABLE ChiTietHoaDon (MaHD int REFERENCES HoaDon, MaDV int REFERENCES MenuDichVu,
        SoLuong int CHECK (SoLuong > 0), DonGia decimal(18,2), PRIMARY KEY (MaHD, MaDV));

    INSERT BanBida(TenBan,LoaiBan,GiaTheoGio) VALUES
    (N'Bàn Lỗ 01',N'Pool (Lỗ)',80000),(N'Bàn Lỗ 02',N'Pool (Lỗ)',80000),
    (N'Bàn Carom 03',N'Carom (Phăng)',95000),(N'Bàn Snooker 04',N'Snooker',120000);
    INSERT MenuDichVu(TenDV,DonGia,TonKho) VALUES
    (N'Nước tăng lực Sting',18000,24),(N'Mì xào bò',45000,12),
    (N'Thuê gậy Carbon',30000,NULL);

    COMMIT;
END;
GO
