using System;
using System.IO;
using System.Text;

namespace ExternalSortingAlgorithms
{
    public static class FileGenerator
    {
        private static readonly Random Random = new Random();
        private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
        private const string Alphanumeric = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

        public static void GenerateFile(string filePath, long targetSizeInMb)
        {
            long targetSizeInBytes = targetSizeInMb * 1024 * 1024;
            long currentBytes = 0;

            using (var writer = new StreamWriter(filePath, false, Encoding.UTF8, bufferSize: 1024 * 1024))
            {
                StringBuilder sb = new StringBuilder(70);

                while (currentBytes < targetSizeInBytes)
                {
                    sb.Clear();

                    sb.Append(GenerateLatinKey());
                    sb.Append('-');
                    sb.Append(GenerateString());
                    sb.Append('-');
                    sb.Append(GeneratePhoneNumber());

                    string line = sb.ToString();
                    writer.WriteLine(line);

                    currentBytes += Encoding.UTF8.GetByteCount(line) + Environment.NewLine.Length;
                }
            }
        }

        private static char GenerateLatinKey() => Alphabet[Random.Next(Alphabet.Length)];

        private static string GenerateString(int minLen = 5, int maxLen = 45)
        {
            int length = Random.Next(minLen, maxLen + 1);
            char[] chars = new char[length];

            for (int i = 0; i < length; i++)
            {
                chars[i] = Alphanumeric[Random.Next(Alphanumeric.Length)];
            }

            return new string(chars);
        }

        private static string GeneratePhoneNumber()
        {
            string[] operatorCodes =
            {
                "039", "067", "068", "077", "096", "097", "098", // Київстар
                "050", "066", "075", "095", "099",               // Vodafone
                "063", "073", "093",                             // lifecell
                "091",                                           // ТриМоб
                "089", "094", "020",                             // Інтертелеком
                "092"                                            // PEOPLEnet
            };

            string operatorCode = operatorCodes[Random.Next(operatorCodes.Length)];

            char[] phone = new char[13];

            phone[0] = '+';
            phone[1] = '3';
            phone[2] = '8';
            phone[3] = operatorCode[0];
            phone[4] = operatorCode[1];
            phone[5] = operatorCode[2];

            for (int i = 6; i < 13; i++)
            {
                phone[i] = (char)('0' + Random.Next(0, 10));
            }

            return new string(phone);
        }
    }
}