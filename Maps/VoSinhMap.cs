using CsvHelper.Configuration;
using Search_VTF_ID.Models;

namespace Search_VTF_ID.Maps;

public class VoSinhMap : ClassMap<VoSinh>
{
    public VoSinhMap()
    {
        Map(m => m.STT).Index(0);
        Map(m => m.MaHoiVien).Index(1);
        Map(m => m.HoTen).Index(2);
        Map(m => m.NgaySinh).Index(3);
        Map(m => m.GioiTinh).Index(4);
        Map(m => m.ToChucThanhVien).Index(5);
        Map(m => m.CLB).Index(6);
        Map(m => m.CapDang).Index(7);
    }
}