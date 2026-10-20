using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ExternalSortingAlgorithms
{
    public static class ModifiedMergeSorter
    {
        private const int IoBufferSize = 65536;
        private const long ChunkSizeBytes = 100 * 1024 * 1024;

        public static void Sort(string inputFilePath, string outputFilePath)
        {
            string tempDirectory = Path.Combine(Path.GetDirectoryName(outputFilePath) ?? "", "temp_chunks");
            Directory.CreateDirectory(tempDirectory);

            List<string> chunkFiles = new List<string>();

            try
            {
                chunkFiles = CreateSortedChunks(inputFilePath, tempDirectory);

                if (chunkFiles.Count == 0)
                    return;

                if (chunkFiles.Count == 1)
                {
                    if (File.Exists(outputFilePath))
                        File.Delete(outputFilePath);

                    File.Move(chunkFiles[0], outputFilePath);
                    return;
                }

                MergeChunks(chunkFiles, outputFilePath);
            }
            finally
            {
                foreach (var file in chunkFiles)
                {
                    if (File.Exists(file))
                    {
                        try { File.Delete(file); } catch { }
                    }
                }

                if (Directory.Exists(tempDirectory))
                {
                    try { Directory.Delete(tempDirectory, true); } catch { }
                }
            }
        }

        private static List<string> CreateSortedChunks(string inputFilePath, string tempDir)
        {
            List<string> chunkFiles = new List<string>();
            using var reader = new StreamReader(inputFilePath, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: IoBufferSize);

            List<string> linesBuffer = new List<string>();
            long currentChunkSize = 0;
            int chunkIndex = 0;

            while (!reader.EndOfStream)
            {
                string? line = reader.ReadLine();
                if (line == null)
                    continue;

                linesBuffer.Add(line);
                currentChunkSize += Encoding.UTF8.GetByteCount(line) + Environment.NewLine.Length;

                if (currentChunkSize >= ChunkSizeBytes)
                {
                    string chunkPath = SaveSortedChunk(linesBuffer, tempDir, chunkIndex++);
                    chunkFiles.Add(chunkPath);

                    linesBuffer.Clear();
                    currentChunkSize = 0;

                    GC.Collect();
                }
            }

            if (linesBuffer.Count > 0)
            {
                string chunkPath = SaveSortedChunk(linesBuffer, tempDir, chunkIndex++);
                chunkFiles.Add(chunkPath);
                linesBuffer.Clear();
            }

            return chunkFiles;
        }

        private static string SaveSortedChunk(List<string> lines, string tempDir, int index)
        {
            lines.Sort((a, b) => b[0].CompareTo(a[0]));

            string chunkPath = Path.Combine(tempDir, $"chunk_{index}.tmp");
            using var writer = new StreamWriter(chunkPath, false, Encoding.UTF8, bufferSize: IoBufferSize);

            foreach (var l in lines)
            {
                writer.WriteLine(l);
            }

            return chunkPath;
        }

        private static void MergeChunks(List<string> chunkFiles, string outputFilePath)
        {
            int k = chunkFiles.Count;
            StreamReader[] readers = new StreamReader[k];

            var pq = new PriorityQueue<(string Line, int ReaderIndex), char>(Comparer<char>.Create((a, b) => b.CompareTo(a)));

            try
            {
                for (int i = 0; i < k; i++)
                {
                    readers[i] = new StreamReader(chunkFiles[i], Encoding.UTF8, false, IoBufferSize);
                    if (!readers[i].EndOfStream)
                    {
                        string? line = readers[i].ReadLine();
                        if (line != null)
                        {
                            pq.Enqueue((line, i), line[0]);
                        }
                    }
                }

                using var writer = new StreamWriter(outputFilePath, false, Encoding.UTF8, bufferSize: IoBufferSize);

                while (pq.Count > 0)
                {
                    var (item, readerIdx) = pq.Dequeue();
                    writer.WriteLine(item);

                    if (!readers[readerIdx].EndOfStream)
                    {
                        string? nextLine = readers[readerIdx].ReadLine();
                        if (nextLine != null)
                        {
                            pq.Enqueue((nextLine, readerIdx), nextLine[0]);
                        }
                    }
                }
            }
            finally
            {
                for (int i = 0; i < k; i++)
                {
                    readers[i]?.Dispose();
                }
            }
        }
    }
}