using System;
using System.IO;
using System.Text;

namespace ExternalSortingAlgorithms
{
    public static class StraightMergeSorter
    {
        private const int BufferSize = 65536;

        public static void Sort(string inputFilePath, string outputFilePath)
        {
            File.Copy(inputFilePath, outputFilePath, overwrite: true);

            string tempFileB = "D:\\University\\3 Semester\\PA\\Lab 1\\Program\\files\\sorted files\\temp_b.txt";
            string tempFileC = "D:\\University\\3 Semester\\PA\\Lab 1\\Program\\files\\sorted files\\temp_c.txt";

            try
            {
                long totalRecords = CountLines(outputFilePath);
                if (totalRecords <= 1)
                    return;

                long runLength = 1;
                while (runLength < totalRecords)
                {
                    Split(outputFilePath, tempFileB, tempFileC, runLength);
                    Merge(outputFilePath, tempFileB, tempFileC, runLength);

                    runLength *= 2;
                }
            }
            finally
            {
                if (File.Exists(tempFileB))
                    File.Delete(tempFileB);

                if (File.Exists(tempFileC))
                    File.Delete(tempFileC);
            }
        }

        private static void Split(string sourceFile, string fileB, string fileC, long runLength)
        {
            using var reader = new StreamReader(sourceFile, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: BufferSize);
            using var writerB = new StreamWriter(fileB, false, Encoding.UTF8, bufferSize: BufferSize);
            using var writerC = new StreamWriter(fileC, false, Encoding.UTF8, bufferSize: BufferSize);

            bool writeToB = true;
            while (!reader.EndOfStream)
            {
                StreamWriter currentWriter = writeToB ? writerB : writerC;

                for (long i = 0; i < runLength && !reader.EndOfStream; i++)
                {
                    string? line = reader.ReadLine();
                    if (line != null)
                    {
                        currentWriter.WriteLine(line);
                    }
                }
                writeToB = !writeToB;
            }
        }

        private static void Merge(string targetFile, string fileB, string fileC, long runLength)
        {
            using var writer = new StreamWriter(targetFile, false, Encoding.UTF8, bufferSize: BufferSize);
            using var readerB = new StreamReader(fileB, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: BufferSize);
            using var readerC = new StreamReader(fileC, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: BufferSize);

            string? lineB = readerB.ReadLine();
            string? lineC = readerC.ReadLine();

            while (lineB != null || lineC != null)
            {
                long countB = 0;
                long countC = 0;

                while (countB < runLength && countC < runLength && lineB != null && lineC != null)
                {
                    char keyB = lineB[0];
                    char keyC = lineC[0];

                    if (keyB >= keyC)
                    {
                        writer.WriteLine(lineB);
                        lineB = readerB.ReadLine();
                        countB++;
                    }
                    else
                    {
                        writer.WriteLine(lineC);
                        lineC = readerC.ReadLine();
                        countC++;
                    }
                }

                while (countB < runLength && lineB != null)
                {
                    writer.WriteLine(lineB);
                    lineB = readerB.ReadLine();
                    countB++;
                }

                while (countC < runLength && lineC != null)
                {
                    writer.WriteLine(lineC);
                    lineC = readerC.ReadLine();
                    countC++;
                }
            }
        }

        private static long CountLines(string filePath)
        {
            long count = 0;
            using var reader = new StreamReader(filePath, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: BufferSize);
            while (reader.ReadLine() != null)
            {
                count++;
            }
            return count;
        }
    }
}