using System;
using System.Net;
using System.Threading.Tasks;
using HtmlAgilityPack;

namespace Al_Khayr_Salat.Functions;

public class prayerTime
{
    public string Salat { get; set; }
    public string Salat_Time { get; set; }

    public prayerTime(string name, string time)
    {
        Salat = name;
        Salat_Time = time;
    }
}