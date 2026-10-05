using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProjectCrimping
{
    /// <summary>
    /// Global state aplikasi setelah login.
    /// Dipakai untuk passing data antar form (Login → Dashboard → dll).
    /// 
    /// PENTING: Jangan lupa panggil Session.Clear() saat logout.
    /// </summary>
    internal class Session
    {
        // ================================================================
        //  OPERATOR
        // ================================================================
        /// <summary>Nomor registrasi operator (hasil strip prefix "ExcitedCRP").</summary>
        public static string Noreg { get; set; }

        /// <summary>Nama lengkap operator (dari DB_PEGAWAI.nama).</summary>
        public static string Nama { get; set; }

        /// <summary>Extension / info tambahan operator (opsional).</summary>
        public static string ext { get; set; }

        // ================================================================
        //  SHIFT
        // ================================================================
        /// <summary>Shift kerja: 1 atau 2.</summary>
        public static int Shift { get; set; }

        // ================================================================
        //  MESIN
        // ================================================================
        /// <summary>Raw barcode scan mesin, contoh: "CRP15-R2".</summary>
        public static string mesin { get; set; }

        /// <summary>Nama mesin dari DB (tabmch.MchName), contoh: "Crimping".</summary>
        public static string MesinName { get; set; }

        /// <summary>Area mesin, contoh: "R2".</summary>
        public static string MesinArea { get; set; }

        /// <summary>Nomor mesin, contoh: "15".</summary>
        public static string MesinNo { get; set; }

        // ================================================================
        //  STATE
        // ================================================================
        /// <summary>True kalau user sudah login.</summary>
        public static bool IsLoggedIn { get; set; }

        // ================================================================
        //  HELPER
        // ================================================================
        /// <summary>
        /// Format display mesin untuk UI, contoh: "Crimping · R2 No 15".
        /// Return "-" kalau belum ada mesin.
        /// </summary>
        public static string MesinDisplay =>
            string.IsNullOrEmpty(MesinName)
                ? "-"
                : $"{MesinName} · {MesinArea} No {MesinNo}";

        // <summary>
        /// Format display Shift untuk UI , contoh: "Shift 1".
        public static string ShiftDisplay =>
            Shift == 0
                ? "-"
                : $"Shift {Shift}";

        /// <summary>
        /// Bersihkan semua state session (dipanggil saat logout).
        /// </summary>
        public static void Clear()
        {
            Noreg = null;
            Nama = null;
            ext = null;
            Shift = 0;
            IsLoggedIn = false;
            mesin = null;
            MesinName = null;
            MesinArea = null;
            MesinNo = null;
        }
    }
}