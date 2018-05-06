using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace gcm_nasos_deux
{
    class Program
    {
        struct gcm_info
        {
            public byte[] id;
            public uint disc_number;
        }

        static uint[] buffer = new uint[521];
        static int jrnd;
        static byte[] junk = GenZeroJunk();
        //static int type = 0;

        static void Main(string[] args)
        {
            foreach(string arg in args)
            {
                if (CheckGCM(arg))
                {
                    type.dec = 0;
                    type.enc = 0;

                    gcm_info g_i = GetGcmInfo(arg);

                    using (BinaryReader br = new BinaryReader(new FileStream(arg, FileMode.Open)))
                    {
                        using (BinaryWriter bw = new BinaryWriter(new FileStream("temp_gcm", FileMode.Create)))
                        {
                            uint sector_number = 0;
                            while(br.BaseStream.Position != br.BaseStream.Length)
                            {
                                int read_length = ((int)(br.BaseStream.Length - br.BaseStream.Position) >= 0x40000) ? 0x40000 : (int)(br.BaseStream.Length - br.BaseStream.Position);

                                byte[] array1 = br.ReadBytes(read_length);
                                byte[] array2 = GenerateJunk(sector_number++, g_i);

                                switch (CompareArrays(array1, array2))
                                {
                                    case 0:
                                        bw.Write(array1);
                                        break;
                                    case 1:
                                        bw.Write(junk);
                                        break;
                                    case 2:
                                        bw.Write(array2);
                                        break;
                                }

                                Console.Write(string.Format("\r{0}%", (Math.Round((((double)sector_number / 5570.0) * 100), 0).ToString())));
                            }
                        }
                    }

                    string arg_new = CreateName(arg);
                    File.Move("temp_gcm", arg_new);
                }
            }
        }

        private static byte[] GenZeroJunk()
        {
            byte[] temp_junk = new byte[0x40000];
            byte[] magic_junk = Encoding.ASCII.GetBytes("JUNK");

            using (BinaryWriter bw = new BinaryWriter(new MemoryStream(temp_junk)))
            {
                while(bw.BaseStream.Position != bw.BaseStream.Length)
                {
                    bw.Write(magic_junk);
                }
            }

            return temp_junk;
        }

        struct gcm_type
        {
            public int enc;
            public int dec;
        }

        static gcm_type type = new gcm_type();

        private static int CompareArrays(byte[] array1, byte[] array2)
        {
            int trigger = 3;
            for (int i = 0; i < array1.Length; i++)
            {
                if (array1[i] != array2[i])
                {
                    trigger ^= 1;
                    break;
                }
                if (i == array1.Length - 1)
                {
                    type.enc++;
                }
            }
            for (int i = 0; i < array1.Length; i++)
            {
                if (array1[i] != junk[i])
                {
                    trigger ^= 2;
                    break;
                }
                if (i == array1.Length - 1)
                {
                    type.dec++;
                }
            }
            return trigger;
        }

        private static byte[] GenerateJunk(uint i, gcm_info g_i)
        {
            uint seed = 0;
            uint blockcount = i * 8 * 0x1ef29123;

            byte[] JunkSector = new byte[0x40000];

            using (BinaryWriter mw = new BinaryWriter(new MemoryStream(JunkSector)))
            {
                while (mw.BaseStream.Position != mw.BaseStream.Length)
                {
                    if ((mw.BaseStream.Position & 0x00007fff) == 0)
                    {
                        seed = (((((uint)g_i.id[2] << 8) | g_i.id[1]) << 16) | ((uint)(g_i.id[3] + g_i.id[2]) << 8)) | (uint)(g_i.id[0] + g_i.id[1]);
                        seed = ((seed ^ g_i.disc_number) * 0x260bcd5) ^ blockcount;
                        init_rnd(seed);
                        blockcount += 0x1ef29123;
                    }

                    byte[] result = scramble(irnd());

                    mw.Write(result);
                }
            }
            return JunkSector;
        }

        private static string CreateName(string arg)
        {
            string filename = new FileInfo(arg).Name.Replace(".dec.", ".");
            string filenamewithoutext = Path.GetFileNameWithoutExtension(filename);
            string extension = Path.GetExtension(filename);
            string directory = new DirectoryInfo(arg).Parent.FullName;
            string arg_new = string.Empty;

            switch (type.dec)
            {
                case 0:
                    arg_new = Path.Combine(directory, filenamewithoutext + ".dec" + extension);
                    break;
                default:
                    arg_new = Path.Combine(directory, filenamewithoutext + extension);
                    break;
            }

            return arg_new;
        }

        private static bool CheckGCM(string arg)
        {
            uint magic_word = 0x3d9f33c2;
            using (BinaryReader br = new BinaryReader(new FileStream(arg, FileMode.Open)))
            {
                br.BaseStream.Seek(28, SeekOrigin.Begin);
                uint temp_magic_word = br.ReadUInt32();
                if (temp_magic_word == magic_word) return true;
            }
            return false;
        }

        private static gcm_info GetGcmInfo(string arg)
        {
            gcm_info temp = new gcm_info();
            using (BinaryReader br = new BinaryReader(new FileStream(arg, FileMode.Open)))
            {
                temp.id = br.ReadBytes(6);
                temp.disc_number = br.ReadByte();
            }
            return temp;
        }

        private static byte[] scramble(uint v)
        {
            byte[] temp = new byte[4];

            temp[0] = (byte)(v >> 24);
            temp[1] = (byte)(v >> 18);
            temp[2] = (byte)(v >> 8);
            temp[3] = (byte)(v);

            return temp;
        }

        static uint irnd()
        {
            if (++jrnd >= 521)
            {
                rnd521();
                jrnd = 0;
            }
            return buffer[jrnd];
        }

        static void init_rnd(uint seed)
        {
            uint temp = 0;
            for (int i = 0; i <= 16; i++)
            {
                for (int j = 0; j < 32; j++)
                {
                    seed = seed * 0x5d588b65u + 1;
                    temp = (temp >> 1) | (seed & 0x80000000u);
                }
                buffer[i] = temp;
            }

            buffer[16] = (buffer[16] << 23) ^ (buffer[0] >> 9) ^ buffer[16];

            for (int i = 17; i <= 520; i++)
            {
                buffer[i] = ((buffer[i - 17] << 23) ^ (buffer[i - 16] >> 9)) ^ buffer[i - 1];
            }

            rnd521(); rnd521(); rnd521();
            jrnd = 520;
        }

        static void rnd521()
        {
            for (int i = 0; i < 32; i++)
            {
                buffer[i] ^= buffer[i + 489];
            }

            for (int i = 32; i < 521; i++)
            {
                buffer[i] ^= buffer[i - 32];
            }
        }
    }
}
